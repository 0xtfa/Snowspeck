using FluentAssertions;
using Snowspeck.Metadata;
using Snowspeck.Services;

namespace Snowspeck.Tests;

/// <summary>
/// Pins the exact binary layout of generated IDs. The expected values were computed independently
/// (outside this library), so these tests fail if a change would make new IDs differ from, or decode
/// differently than, IDs that existing users have already stored.
/// </summary>
public class IdLayoutCompatibilityTests
{
    private const long DefaultEpoch = 1735689600000L; // 2025-01-01T00:00:00Z
    private const long RealisticNow = 1793795696789L; // 2026-11-04T12:34:56.789Z

    private static SnowflakeOptions Options() => new() { DatacenterId = 3, WorkerId = 7 };

    [Fact]
    public void DefaultOptions_ShouldBeUnchanged()
    {
        SnowflakeOptions options = new();

        options.Epoch.Should().Be(DefaultEpoch);
        options.DatacenterId.Should().Be(0);
        options.WorkerId.Should().Be(0);
        options.MaxClockSkewMs.Should().Be(10);
    }

    [Theory]
    [InlineData(DefaultEpoch + 1000, 4194725888L, 4194725889L)]
    [InlineData(RealisticNow, 243714634186911744L, 243714634186911745L)]
    public void Signed_NextId_ShouldProduceKnownValues(long unixMs, long first, long second)
    {
        SignedSnowflakeService service = new(Options(), ScriptedTimeProvider.Sequence(unixMs));

        service.NextId().Should().Be(first);
        service.NextId().Should().Be(second);
    }

    [Theory]
    [InlineData(DefaultEpoch + 1000, 4194725888UL, 4194725889UL)]
    [InlineData(RealisticNow, 243714634186911744UL, 243714634186911745UL)]
    public void Unsigned_NextId_ShouldProduceKnownValues(long unixMs, ulong first, ulong second)
    {
        UnsignedSnowflakeService service = new(Options(), ScriptedTimeProvider.Sequence(unixMs));

        service.NextId().Should().Be(first);
        service.NextId().Should().Be(second);
    }

    [Fact]
    public void Decode_ShouldReadKnownIds()
    {
        SignedSnowflakeService signed = new(new SnowflakeOptions());
        UnsignedSnowflakeService unsigned = new(new SnowflakeOptions());
        SnowflakeMetadata expected = new(RealisticNow, 3, 7, 1);

        signed.Decode(243714634186911745L).Should().Be(expected);
        unsigned.Decode(243714634186911745UL).Should().Be(expected);
        signed.Decode(4198498303L).Should().Be(new SnowflakeMetadata(DefaultEpoch + 1000, 31, 31, 4095));
    }

    [Fact]
    public void Decode_ShouldExposeUtcTime()
    {
        SnowflakeMetadata metadata = new SignedSnowflakeService(new SnowflakeOptions()).Decode(243714634186911745L);

        metadata.ToDateTimeOffset().Should().Be(new DateTimeOffset(2026, 11, 4, 12, 34, 56, 789, TimeSpan.Zero));
        metadata.UtcDateTime.Kind.Should().Be(DateTimeKind.Utc);
        metadata.UnixSeconds.Should().Be(1793795696L);
    }
}
