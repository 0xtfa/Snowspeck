// Copyright (c) 2025 Tiago Ferreira Alves. Licensed under the MIT License.
using Microsoft.Extensions.DependencyInjection;
using Snowspeck.Interfaces;
using Snowspeck.Services;

namespace Snowspeck.Extensions;

/// <summary>
/// Extension methods for registering Snowspeck generators with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SignedSnowflakeService"/> as a singleton <see cref="ISnowflakeGenerator{T}"/> of <see cref="long"/>.
    /// </summary>
    /// <param name="services">The service collection to add the generator to.</param>
    /// <param name="configure">Configures the <see cref="SnowflakeOptions"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSnowspeck(
        this IServiceCollection services, Action<SnowflakeOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ISnowflakeGenerator<long>, SignedSnowflakeService>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="UnsignedSnowflakeService"/> as a singleton <see cref="ISnowflakeGenerator{T}"/> of <see cref="ulong"/>.
    /// </summary>
    /// <param name="services">The service collection to add the generator to.</param>
    /// <param name="configure">Configures the <see cref="SnowflakeOptions"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSnowspeckUnsigned(
        this IServiceCollection services, Action<SnowflakeOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ISnowflakeGenerator<ulong>, UnsignedSnowflakeService>();
        return services;
    }
}
