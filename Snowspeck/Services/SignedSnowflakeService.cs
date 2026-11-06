// Copyright (c) 2025 Tiago Ferreira Alves. Licensed under the MIT License.
using Microsoft.Extensions.Options;
using Snowspeck.Metadata;

namespace Snowspeck.Services;

/// <summary>
/// Generates Snowflake IDs as signed 64-bit integers (<see cref="long"/>). This class is thread-safe.
/// </summary>
/// <remarks>
/// IDs are positive and increasing for about 69 years after <see cref="SnowflakeOptions.Epoch"/>
/// (until around 2094 with the default epoch), so they fit signed database columns such as <c>BIGINT</c>.
/// </remarks>
public sealed class SignedSnowflakeService : SnowflakeServiceBase<long>
{
    /// <summary>
    /// Initializes a generator from options configured through dependency injection.
    /// </summary>
    /// <param name="options">The generator options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    public SignedSnowflakeService(IOptions<SnowflakeOptions> options)
        : base((options ?? throw new ArgumentNullException(nameof(options))).Value) { }

    /// <summary>
    /// Initializes a generator with the given options.
    /// </summary>
    /// <param name="options">The generator options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    public SignedSnowflakeService(SnowflakeOptions options) : base(options) { }

    /// <summary>
    /// Initializes a generator with the given options that reads the current time from <paramref name="timeProvider"/>.
    /// </summary>
    /// <param name="options">The generator options.</param>
    /// <param name="timeProvider">
    /// The source of the current time. Pass a fake <see cref="TimeProvider"/> to control generated timestamps in tests.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    public SignedSnowflakeService(SnowflakeOptions options, TimeProvider timeProvider) : base(options, timeProvider) { }

    /// <inheritdoc />
    protected override long ComposeId(long timestamp, short datacenterId, short workerId, long sequence)
    {
        return (timestamp << TsShift) | ((long)datacenterId << DcShift)
                                      | ((long)workerId << WorkerShift)
                                      | sequence;
    }

    /// <inheritdoc />
    protected override SnowflakeMetadata ParseId(long id, long epoch)
    {
        long sequence = id & SequenceMask;
        short workerId = (short)((id >> WorkerShift) & ((1L << WorkerBits) - 1));
        short datacenterId = (short)((id >> DcShift) & ((1L << DatacenterBits) - 1));
        long timestamp = (id >> TsShift) + epoch;

        return new SnowflakeMetadata(timestamp, datacenterId, workerId, sequence);
    }
}
