namespace Snowspeck.Tests;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock is scripted per call, so tests can simulate frozen time,
/// sequence exhaustion and backwards clock jumps deterministically.
/// </summary>
internal sealed class ScriptedTimeProvider : TimeProvider
{
    private readonly Func<int, long> _unixMsForCall;
    private readonly int _maxCalls;
    private int _calls;

    /// <param name="unixMsForCall">Maps the 1-based call number to the Unix time, in milliseconds, to return.</param>
    /// <param name="maxCalls">
    /// Fails the test instead of letting a generator that never stops reading the clock hang the test run.
    /// </param>
    public ScriptedTimeProvider(Func<int, long> unixMsForCall, int maxCalls = 100_000)
    {
        _unixMsForCall = unixMsForCall;
        _maxCalls = maxCalls;
    }

    /// <summary>Returns each value in turn, then keeps returning the last one.</summary>
    public static ScriptedTimeProvider Sequence(params long[] unixMs) =>
        new(call => unixMs[Math.Min(call, unixMs.Length) - 1]);

    public int Calls => Volatile.Read(ref _calls);

    public override DateTimeOffset GetUtcNow()
    {
        int call = Interlocked.Increment(ref _calls);
        if (call > _maxCalls)
            throw new InvalidOperationException($"The clock was read more than {_maxCalls} times; the generator would hang.");

        return DateTimeOffset.FromUnixTimeMilliseconds(_unixMsForCall(call));
    }
}
