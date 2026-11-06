using System.Collections.Concurrent;
using Snowspeck.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Snowspeck.Tests;

public class SignedSnowflakeServiceTests
{
    private readonly SignedSnowflakeService _service;

    public SignedSnowflakeServiceTests()
    {
        IOptions<SnowflakeOptions> options = Options.Create(new SnowflakeOptions
        {
            DatacenterId = 1,
            WorkerId = 1,
            Epoch = 1735689600000L
        });

        _service = new SignedSnowflakeService(options);
    }

    [Fact]
    public void NextId_ShouldReturnUniqueIds()
    {
        HashSet<long> ids = [];

        for (int i = 0; i < 1000; i++)
        {
            long id = _service.NextId();
            ids.Add(id);
        }

        ids.Count.Should().Be(1000);
    }

    [Fact]
    public void NextId_ShouldBePositive()
    {
        long id = _service.NextId();
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenWorkerIdOutOfRange()
    {
        IOptions<SnowflakeOptions> optionsA = Options.Create(new SnowflakeOptions
        {
            WorkerId = 2048, // Invalid, max is 31
            Epoch = 1735689600000L
        });

        IOptions<SnowflakeOptions> optionsB = Options.Create(new SnowflakeOptions
        {
            WorkerId = -2048, // Invalid, min is 0
            Epoch = 1735689600000L
        });

        Action actionA = () =>
        {
            _ = new SignedSnowflakeService(optionsA);
        };

        Action actionB = () =>
        {
            _ = new SignedSnowflakeService(optionsB);
        };
        
        actionA.Should().Throw<ArgumentException>();
        actionB.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void NextId_ShouldBeThreadSafeAndUnique()
    {
        const int threadCount = 8;
        const int idsPerThread = 1000;
        List<Thread> threads = [];
        ConcurrentBag<long> ids = [];

        for (int i = 0; i < threadCount; i++)
        {
            Thread thread = new(() =>
            {
                for (int j = 0; j < idsPerThread; j++)
                {
                    long id = _service.NextId();
                    ids.Add(id);
                }
            });

            threads.Add(thread);
            thread.Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        ids.Count.Should().Be(threadCount * idsPerThread);
        ids.Distinct().Count().Should().Be(ids.Count);
    }
    
    [Fact]
    public void NextId_ShouldEncodeCorrectTimestampAndWorkerId()
    {
        long id = _service.NextId();

        (long timestamp, short datacenterId, short workerId, long sequence) = _service.Decode(id);

        datacenterId.Should().Be(1);
        workerId.Should().Be(1);
        timestamp.Should().BeGreaterThan(1735689600000L);
        sequence.Should().BeGreaterThanOrEqualTo(0);
    }
}
