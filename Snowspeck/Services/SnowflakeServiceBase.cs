// Copyright (c) 2025 Tiago Ferreira Alves. Licensed under the MIT License.
using System.Runtime.CompilerServices;
using Snowspeck.Exceptions;
using Snowspeck.Interfaces;
using Snowspeck.Metadata;

namespace Snowspeck.Services;

/// <summary>
/// Base class for Snowflake ID generators. It implements the lock-free, thread-safe generation
/// algorithm and leaves encoding the components into <typeparamref name="TId"/> to derived classes.
/// </summary>
/// <typeparam name="TId">The numeric type of the generated IDs.</typeparam>
/// <remarks>
/// IDs use the layout <c>[41-bit timestamp offset][5-bit datacenter][5-bit worker][12-bit sequence]</c>.
/// A single instance generates at most 4096 IDs per millisecond, and every instance that runs at the
/// same time must use a different <see cref="SnowflakeOptions.DatacenterId"/>/<see cref="SnowflakeOptions.WorkerId"/> pair.
/// </remarks>
public abstract class SnowflakeServiceBase<TId> : ISnowflakeGenerator<TId>
{
    // Protected accessors for extensibility

    /// <summary>Gets the configured epoch, in Unix time milliseconds.</summary>
    protected long Epoch => _epoch;

    /// <summary>Gets the maximum tolerated backward clock movement, in milliseconds.</summary>
    protected int MaxClockSkewMs => _maxClockSkewMs;

    /// <summary>Gets the configured datacenter identifier.</summary>
    protected short DatacenterId => _datacenterId;

    /// <summary>Gets the configured worker identifier.</summary>
    protected short WorkerId => _workerId;

    private long _state;

    private readonly long  _epoch;
    private readonly short _datacenterId;
    private readonly short _workerId;
    private readonly int _maxClockSkewMs;
    private readonly TimeProvider _timeProvider;

    // Bit layout: [41-bit timestamp offset][5-bit DC][5-bit worker][12-bit seq]

    /// <summary>The number of bits used for the per-millisecond sequence.</summary>
    protected const int SequenceBits = 12;

    /// <summary>The number of bits used for the worker identifier.</summary>
    protected const int WorkerBits = 5;

    /// <summary>The number of bits used for the datacenter identifier.</summary>
    protected const int DatacenterBits = 5;

    /// <summary>The mask that extracts the sequence from an ID.</summary>
    protected const long SequenceMask = (1L << SequenceBits) - 1;

    /// <summary>The bit position of the worker identifier.</summary>
    protected const int WorkerShift = SequenceBits;

    /// <summary>The bit position of the datacenter identifier.</summary>
    protected const int DcShift = SequenceBits + WorkerBits;

    /// <summary>The bit position of the timestamp offset.</summary>
    protected const int TsShift = SequenceBits + WorkerBits + DatacenterBits;

    private const long MaxWorkerId = (1L << WorkerBits) - 1;
    private const long MaxDcId = (1L << DatacenterBits) - 1;

    /// <summary>
    /// Initializes the generator with the given options, reading time from the system clock.
    /// </summary>
    /// <param name="options">The generator options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    protected SnowflakeServiceBase(SnowflakeOptions options) : this(options, TimeProvider.System) { }

    /// <summary>
    /// Initializes the generator with the given options, reading time from <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="options">The generator options.</param>
    /// <param name="timeProvider">The source of the current time, typically <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    protected SnowflakeServiceBase(SnowflakeOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _epoch = options.Epoch;
        _datacenterId = options.DatacenterId;
        _workerId = options.WorkerId;
        _maxClockSkewMs = options.MaxClockSkewMs;
        _timeProvider = timeProvider;

        if (_datacenterId < 0 || _datacenterId > MaxDcId)
            throw new ArgumentOutOfRangeException(
                nameof(options.DatacenterId), $"DatacenterId must be between 0 and {MaxDcId}.");

        if (_workerId < 0 || _workerId > MaxWorkerId)
            throw new ArgumentOutOfRangeException(
                nameof(options.WorkerId), $"WorkerId must be between 0 and {MaxWorkerId}.");

        if (_maxClockSkewMs < 0)
            throw new ArgumentOutOfRangeException(
                nameof(options.MaxClockSkewMs), "MaxClockSkewMs must be >= 0.");
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TId NextId()
    {
        while (true)
        {
            long nowMs = GetCurrentTimestamp();
            long nowOffset = nowMs - _epoch;

            long oldState = Volatile.Read(ref _state);
            long lastOff = oldState >> SequenceBits;
            long seq = oldState & SequenceMask;

            if (nowOffset < lastOff)
            {
                nowOffset = HandleClockSkew(nowOffset, lastOff);
            }

            long newOff, newSeq;
            if (nowOffset == lastOff)
            {
                newOff = lastOff;
                newSeq = (seq + 1) & SequenceMask;

                if (newSeq == 0)
                {
                    newOff = WaitForNextMillisOffset(lastOff);
                    newSeq = 0;
                }
            }
            else
            {
                newOff = nowOffset;
                newSeq = 0;
            }

            long newState = (newOff << SequenceBits) | newSeq;

            if (Interlocked.CompareExchange(ref _state, newState, oldState) == oldState)
                return ComposeId(newOff, _datacenterId, _workerId, newSeq);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SnowflakeMetadata Decode(TId id) => ParseId(id, _epoch);

    /// <summary>
    /// Combines the components into an ID.
    /// </summary>
    /// <param name="timestampOffset">Milliseconds elapsed since <see cref="Epoch"/>.</param>
    /// <param name="datacenterId">The datacenter identifier.</param>
    /// <param name="workerId">The worker identifier.</param>
    /// <param name="sequence">The per-millisecond sequence number.</param>
    /// <returns>The composed ID.</returns>
    protected abstract TId ComposeId(long timestampOffset, short datacenterId, short workerId, long sequence);

    /// <summary>
    /// Splits an ID into its components.
    /// </summary>
    /// <param name="id">The ID to decode.</param>
    /// <param name="epoch">The epoch, in Unix time milliseconds, to add back to the timestamp offset.</param>
    /// <returns>The decoded components, with an absolute Unix timestamp in milliseconds.</returns>
    protected abstract SnowflakeMetadata ParseId(TId id, long epoch);

    /// <summary>
    /// Gets the current time as Unix time milliseconds.
    /// </summary>
    /// <returns>The current Unix time, in milliseconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual long GetCurrentTimestamp() => _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

    /// <summary>
    /// Waits until the clock moves past <paramref name="lastOffset"/>.
    /// </summary>
    /// <param name="lastOffset">The timestamp offset of the most recently generated ID.</param>
    /// <returns>The current timestamp offset, which is greater than <paramref name="lastOffset"/>.</returns>
    /// <exception cref="SnowflakeClockException">
    /// The clock moved backwards by more than <see cref="MaxClockSkewMs"/> while waiting.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected virtual long WaitForNextMillisOffset(long lastOffset)
    {
        SpinWait spinner = default;
        long latest = long.MinValue;

        while (true)
        {
            long off = GetCurrentTimestamp() - _epoch;
            if (off > lastOffset)
                return off;

            // If the clock jumps backwards while we wait, give up instead of blocking until it catches up again.
            if (off > latest)
                latest = off;
            else if (latest - off > _maxClockSkewMs)
                throw CreateClockException(off, latest - off);

            if (off == lastOffset)
            {
                // The next millisecond is less than 1 ms away. Never fall back to Thread.Sleep(1) here:
                // on Windows it typically blocks for ~15 ms, which caps throughput far below 4096 IDs/ms.
                spinner.SpinOnce(sleep1Threshold: -1);
            }
            else
            {
                // Waiting out a backwards clock adjustment, which can take several milliseconds.
                spinner.SpinOnce();
            }
        }
    }

    /// <summary>
    /// Called when the clock reads earlier than the timestamp of the most recently generated ID.
    /// </summary>
    /// <param name="nowOffset">The current timestamp offset.</param>
    /// <param name="lastOffset">The timestamp offset of the most recently generated ID.</param>
    /// <returns>A timestamp offset that is greater than or equal to <paramref name="lastOffset"/>.</returns>
    /// <exception cref="SnowflakeClockException">
    /// The clock moved backwards by more than <see cref="MaxClockSkewMs"/>.
    /// </exception>
    protected virtual long HandleClockSkew(long nowOffset, long lastOffset)
    {
        long drift = lastOffset - nowOffset;
        if (drift <= _maxClockSkewMs) return WaitForNextMillisOffset(lastOffset);
        throw CreateClockException(nowOffset, drift);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SnowflakeClockException CreateClockException(long nowOffset, long drift)
    {
        if (nowOffset < 0)
        {
            return new SnowflakeClockException(
                $"The system clock reads {-nowOffset} ms earlier than the configured epoch ({_epoch}). " +
                "SnowflakeOptions.Epoch must be a Unix timestamp in milliseconds that is not in the future.");
        }

        return new SnowflakeClockException(
            $"System clock moved backwards by {drift} ms (max tolerated {_maxClockSkewMs} ms).");
    }
}
