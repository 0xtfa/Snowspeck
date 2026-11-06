using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Snowspeck.Extensions;
using Snowspeck.Interfaces;
using Snowspeck.Metadata;
using Snowspeck.Services;

namespace Snowspeck.Tests;

public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider Build(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    [Fact]
    public void AddSnowspeck_ShouldRegisterConfiguredSignedSingleton()
    {
        ServiceCollection services = new();
        services.AddSnowspeck(o => { o.DatacenterId = 2; o.WorkerId = 9; });
        using ServiceProvider provider = Build(services);

        ISnowflakeGenerator<long> generator = provider.GetRequiredService<ISnowflakeGenerator<long>>();
        SnowflakeMetadata metadata = generator.Decode(generator.NextId());

        generator.Should().BeOfType<SignedSnowflakeService>();
        provider.GetRequiredService<ISnowflakeGenerator<long>>().Should().BeSameAs(generator);
        metadata.DatacenterId.Should().Be(2);
        metadata.WorkerId.Should().Be(9);
    }

    [Fact]
    public void AddSnowspeckUnsigned_ShouldRegisterConfiguredUnsignedSingleton()
    {
        ServiceCollection services = new();
        services.AddSnowspeckUnsigned(o => { o.DatacenterId = 4; o.WorkerId = 11; });
        using ServiceProvider provider = Build(services);

        ISnowflakeGenerator<ulong> generator = provider.GetRequiredService<ISnowflakeGenerator<ulong>>();
        SnowflakeMetadata metadata = generator.Decode(generator.NextId());

        generator.Should().BeOfType<UnsignedSnowflakeService>();
        provider.GetRequiredService<ISnowflakeGenerator<ulong>>().Should().BeSameAs(generator);
        metadata.DatacenterId.Should().Be(4);
        metadata.WorkerId.Should().Be(11);
    }

    [Fact]
    public void AddSnowspeck_ShouldKeepUsingTheSystemClock_WhenATimeProviderIsRegistered()
    {
        // Apps (and test hosts) often register a TimeProvider. The generator must not silently switch to it:
        // a frozen test clock would make NextId wait forever once 4096 IDs were issued in the same millisecond.
        DateTimeOffset frozenBeforeEpoch = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(new ScriptedTimeProvider(_ => frozenBeforeEpoch.ToUnixTimeMilliseconds()));
        services.AddSnowspeck(_ => { });
        services.AddSnowspeckUnsigned(_ => { });
        using ServiceProvider provider = Build(services);

        ISnowflakeGenerator<long> signed = provider.GetRequiredService<ISnowflakeGenerator<long>>();
        ISnowflakeGenerator<ulong> unsigned = provider.GetRequiredService<ISnowflakeGenerator<ulong>>();

        signed.Decode(signed.NextId()).ToDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        unsigned.Decode(unsigned.NextId()).ToDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void AddSnowspeck_WithInvalidOptions_ShouldThrowWhenResolved()
    {
        ServiceCollection services = new();
        services.AddSnowspeck(o => o.WorkerId = 32);
        using ServiceProvider provider = Build(services);

        Action act = () => provider.GetRequiredService<ISnowflakeGenerator<long>>();

        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("WorkerId");
    }
}
