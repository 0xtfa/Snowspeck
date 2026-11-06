// Copyright (c) 2025 Tiago Ferreira Alves. Licensed under the MIT License.
namespace Snowspeck.Exceptions;

/// <summary>
/// Exception thrown when the system clock moves backwards relative to the last generated ID.
/// </summary>
public sealed class SnowflakeClockException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance with the specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SnowflakeClockException(string message) : base(message) { }
}
