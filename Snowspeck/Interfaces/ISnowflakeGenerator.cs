// Copyright (c) 2025 Tiago Ferreira Alves. Licensed under the MIT License.

using Snowspeck.Metadata;

namespace Snowspeck.Interfaces;

/// <summary>
/// Generates unique, time-ordered Snowflake IDs and decodes them back into their components.
/// </summary>
/// <typeparam name="T">The numeric type of the IDs, such as <see cref="long"/> or <see cref="ulong"/>.</typeparam>
/// <remarks>The built-in implementations are thread-safe and should be used as singletons.</remarks>
public interface ISnowflakeGenerator<T>
{
    /// <summary>
    /// Generates the next ID. IDs from the same generator are unique and strictly increasing.
    /// </summary>
    /// <returns>A new ID.</returns>
    /// <exception cref="Exceptions.SnowflakeClockException">
    /// The system clock moved backwards by more than <see cref="SnowflakeOptions.MaxClockSkewMs"/>,
    /// or it reads earlier than <see cref="SnowflakeOptions.Epoch"/>.
    /// </exception>
    T NextId();

    /// <summary>
    /// Decodes an ID into its timestamp, datacenter ID, worker ID and sequence number.
    /// </summary>
    /// <param name="id">An ID generated with the same <see cref="SnowflakeOptions.Epoch"/> as this generator.</param>
    /// <returns>The decoded components.</returns>
    SnowflakeMetadata Decode(T id);
}
