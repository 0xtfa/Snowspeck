using System.Collections.Concurrent;
using FluentAssertions;
using Snowspeck.Exceptions;
using Snowspeck.Metadata;
using Snowspeck.Services;

namespace Snowspeck.Tests;

public class ClockBehaviorTests
{
    private const long T = 1793795696789L; // 2026-11-04T12:34:56.789Z
    private const int IdsPerMillisecond = 4096;

    [Fact]
    public void NextId_WhenSequenceIsExhausted_ShouldWaitForNextMillisecond()
    {
        // Calls 1-4097 are NextId reading the clock; call 4098 is the wait loop seeing the next millisecond.
        ScriptedTimeProvider clock = new(call => call <= IdsPerMillisecond + 1 ? T : T + 1);
        SignedSnowflakeService service = new(new SnowflakeOptions(), clock);

        long[] ids = Enumerable.Range(0, IdsPerMillisecond + 1).Select(_ => service.NextId()).ToArray();

        ids.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        service.Decode(ids[0]).Should().Be(new SnowflakeMetadata(T, 0, 0, 0));
        service.Decode(ids[IdsPerMillisecond - 1]).Should().Be(new SnowflakeMetadata(T, 0, 0, 4095));
        service.Decode(ids[IdsPerMillisecond]).Should().Be(new SnowflakeMetadata(T + 1, 0, 0, 0));
    }

    [Fact]
    public void NextId_WhenClockMovesBackWithinTolerance_ShouldWaitAndKeepIncreasing()
    {
        SignedSnowflakeService service = new(new SnowflakeOptions(), ScriptedTimeProvider.Sequence(T, T - 5, T - 3, T, T + 1));

        long first = service.NextId();
        long second = service.NextId();

        second.Should().BeGreaterThan(first);
        service.Decode(second).Should().Be(new SnowflakeMetadata(T + 1, 0, 0, 0));
    }

    [Fact]
    public void NextId_WhenClockMovesBackBeyondTolerance_ShouldThrow_AndRecoverAfterwards()
    {
        SignedSnowflakeService service = new(new SnowflakeOptions(), ScriptedTimeProvider.Sequence(T, T - 11, T + 1));

        long first = service.NextId();
        Action act = () => service.NextId();

        act.Should().ThrowExactly<SnowflakeClockException>()
            .WithMessage("System clock moved backwards by 11 ms (max tolerated 10 ms).");

        long afterRecovery = service.NextId();
        afterRecovery.Should().BeGreaterThan(first);
        service.Decode(afterRecovery).Timestamp.Should().Be(T + 1);
    }

    [Fact]
    public void NextId_WithZeroTolerance_ShouldThrowOnAnyBackwardMove()
    {
        UnsignedSnowflakeService service = new(new SnowflakeOptions { MaxClockSkewMs = 0 }, ScriptedTimeProvider.Sequence(T, T - 1));

        service.NextId();
        Action act = () => service.NextId();

        act.Should().ThrowExactly<SnowflakeClockException>();
    }

    [Fact]
    public void NextId_WhenClockJumpsBackWhileWaitingOutSkew_ShouldThrowInsteadOfHanging()
    {
        // The clock is 5 ms behind (tolerated), then jumps back 197 ms more while the generator waits.
        long[] script = [T, T - 5, T - 3, T - 200];
        ScriptedTimeProvider clock = new(call => script[Math.Min(call, script.Length) - 1], maxCalls: 1_000);
        SignedSnowflakeService service = new(new SnowflakeOptions(), clock);

        service.NextId();
        Action act = () => service.NextId();

        act.Should().ThrowExactly<SnowflakeClockException>().WithMessage("*moved backwards by 197 ms*");
    }

    [Fact]
    public void NextId_WhenClockJumpsBackWhileWaitingForNextMillisecond_ShouldThrowInsteadOfHanging()
    {
        // Exhaust the sequence, then the clock jumps back 500 ms while the generator waits for the next millisecond.
        ScriptedTimeProvider clock = new(call => call <= IdsPerMillisecond + 2 ? T : T - 500, maxCalls: 5_000);
        SignedSnowflakeService service = new(new SnowflakeOptions(), clock);

        for (int i = 0; i < IdsPerMillisecond; i++)
            service.NextId();

        Action act = () => service.NextId();

        act.Should().ThrowExactly<SnowflakeClockException>().WithMessage("*moved backwards by 500 ms*");
    }

    [Fact]
    public void NextId_WhenEpochIsSlightlyInTheFuture_ShouldWaitForIt()
    {
        SnowflakeOptions options = new() { Epoch = T + 5 };
        SignedSnowflakeService service = new(options, ScriptedTimeProvider.Sequence(T, T + 3, T + 6));

        long id = service.NextId();

        service.Decode(id).Timestamp.Should().Be(T + 6);
    }

    [Fact]
    public void NextId_WhenEpochIsInTheFuture_ShouldExplainTheProblem()
    {
        SnowflakeOptions options = new() { Epoch = T + 3_600_000 };
        SignedSnowflakeService service = new(options, ScriptedTimeProvider.Sequence(T));

        Action act = () => service.NextId();

        act.Should().ThrowExactly<SnowflakeClockException>().WithMessage("*earlier than the configured epoch*");
    }

    [Fact]
    public void Subclass_ThatWaitsOutAnySkew_ShouldStillBeAbleToWait()
    {
        // A documented extension point: overriding HandleClockSkew to tolerate large drifts. The clock is
        // 1000 ms behind and then moves forward steadily, which must not be mistaken for a backwards jump.
        ScriptedTimeProvider clock = new(call => call == 1 ? T : T - 1000 + (call - 2) * 100L);
        TolerantGenerator generator = new(new SnowflakeOptions(), clock);

        generator.NextId();
        long id = generator.NextId();

        (id >> 22).Should().Be(T + 100 - new SnowflakeOptions().Epoch);
    }

    [Fact]
    public void NextId_UnderContention_ShouldStayUniqueAndIncreasingPerThread()
    {
        const int threadCount = 8;
        const int idsPerThread = 25_000; // Far more than 4096 per millisecond, so threads keep waiting for the next one.
        SignedSnowflakeService service = new(new SnowflakeOptions { DatacenterId = 1, WorkerId = 2 });
        ConcurrentBag<long[]> perThread = [];

        Parallel.For(0, threadCount, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, _ =>
        {
            long[] ids = new long[idsPerThread];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = service.NextId();
            perThread.Add(ids);
        });

        foreach (long[] ids in perThread)
            ids.Should().BeInAscendingOrder();

        perThread.SelectMany(ids => ids).Should().OnlyHaveUniqueItems();
    }

    private sealed class TolerantGenerator(SnowflakeOptions options, TimeProvider timeProvider)
        : SnowflakeServiceBase<long>(options, timeProvider)
    {
        protected override long ComposeId(long timestampOffset, short datacenterId, short workerId, long sequence) =>
            (timestampOffset << TsShift) | ((long)datacenterId << DcShift) | ((long)workerId << WorkerShift) | sequence;

        protected override SnowflakeMetadata ParseId(long id, long epoch) => default;

        protected override long HandleClockSkew(long nowOffset, long lastOffset) => WaitForNextMillisOffset(lastOffset);
    }
}
