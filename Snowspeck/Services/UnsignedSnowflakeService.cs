// Copyright (c) 2025 Tiago Ferreira Alves. Licensed under the MIT License.
using Microsoft.Extensions.Options;
using Snowspeck.Metadata;

namespace Snowspeck.Services;

/// <summary>
/// Generates Snowflake IDs as unsigned 64-bit integers (<see cref="ulong"/>). This class is thread-safe.
/// </summary>
/// <remarks>
/// IDs are increasing for about 139 years after <see cref="SnowflakeOptions.Epoch"/>, because the
/// timestamp can also use the bit that is reserved for the sign in <see cref="SignedSnowflakeService"/>.
/// </remarks>
public sealed class UnsignedSnowflakeService : SnowflakeServiceBase<ulong>
{
    /// <summary>
    /// Initializes a generator from options configured through dependency injection.
    /// </summary>
    /// <param name="options">The generator options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    public UnsignedSnowflakeService(IOptions<SnowflakeOptions> options)
        : base((options ?? throw new ArgumentNullException(nameof(options))).Value) { }

    /// <summary>
    /// Initializes a generator with the given options.
    /// </summary>
    /// <param name="options">The generator options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    public UnsignedSnowflakeService(SnowflakeOptions options)
        : base(options) { }

    /// <summary>
    /// Initializes a generator with the given options that reads the current time from <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="options">The generator options.</param>
    /// <param name="timeProvider">
    /// The source of the current time. Pass a fake <see cref="TimeProvider"/> to control generated timestamps in tests.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    public UnsignedSnowflakeService(SnowflakeOptions options, TimeProvider timeProvider)
        : base(options, timeProvider) { }

    /// <inheritdoc />
    protected override ulong ComposeId(long timestamp, short datacenterId, short workerId, long sequence)
    {
        return ((ulong)timestamp << TsShift) | ((ulong)datacenterId << DcShift)
                                             | ((ulong)workerId << WorkerShift)
                                             | (ulong)sequence;
    }

    /// <inheritdoc />
    protected override SnowflakeMetadata ParseId(ulong id, long epoch)
    {
        long sequence = (long)(id & SequenceMask);
        short workerId = (short)((id >> WorkerShift) & ((1UL << WorkerBits) - 1));
        short datacenterId = (short)((id >> DcShift) & ((1UL << DatacenterBits) - 1));
        long timestamp = (long)(id >> TsShift) + epoch;

        return new SnowflakeMetadata(timestamp, datacenterId, workerId, sequence);
    }
}
