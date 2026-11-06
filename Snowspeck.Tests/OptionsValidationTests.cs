using FluentAssertions;
using Microsoft.Extensions.Options;
using Snowspeck.Services;

namespace Snowspeck.Tests;

public class OptionsValidationTests
{
    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)32)]
    public void Constructor_ShouldThrow_WhenDatacenterIdOutOfRange(short datacenterId)
    {
        Action act = () => _ = new SignedSnowflakeService(new SnowflakeOptions { DatacenterId = datacenterId });

        act.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("DatacenterId");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMaxClockSkewIsNegative()
    {
        Action act = () => _ = new UnsignedSnowflakeService(new SnowflakeOptions { MaxClockSkewMs = -1 });

        act.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("MaxClockSkewMs");
    }

    [Fact]
    public void Constructor_ShouldAcceptLargestIds()
    {
        SnowflakeOptions options = new() { DatacenterId = 31, WorkerId = 31 };
        SignedSnowflakeService service = new(options);

        (long _, short datacenterId, short workerId, long _) = service.Decode(service.NextId());

        datacenterId.Should().Be(31);
        workerId.Should().Be(31);
    }

    [Fact]
    public void Constructors_ShouldThrowArgumentNull_ForNullArguments()
    {
        SnowflakeOptions options = new();

        FluentActions.Invoking(() => new SignedSnowflakeService((SnowflakeOptions)null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SignedSnowflakeService((IOptions<SnowflakeOptions>)null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SignedSnowflakeService(options, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new UnsignedSnowflakeService((SnowflakeOptions)null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new UnsignedSnowflakeService((IOptions<SnowflakeOptions>)null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new UnsignedSnowflakeService(options, null!)).Should().Throw<ArgumentNullException>();
    }
}
