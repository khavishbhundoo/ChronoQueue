using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using ChronoQueue;

namespace ChronoQueueBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[DisplayName("ChronoQueueValueTypesBenchmarks")]
public class ChronoQueueValueTypesBenchmarks : IDisposable
{
    private ChronoQueue<int> _queue;

    [Params(100,1000,10_000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        _queue = new ChronoQueue<int>();
    }

    [IterationSetup(Target = nameof(Enqueue_Items))]
    public void SetupEnqueue()
    {
        _queue.Flush();
    }

    [IterationSetup(Target = nameof(Dequeue_Items))]
    public void SetupDequeue()
    {
        _queue.Flush();

        if (ItemCount >= 100_000)
        {
            Parallel.For(0, ItemCount, i =>
            {
                var item = new ChronoQueueItem<int>(9, DateTime.UtcNow.AddSeconds(10));
                _queue.Enqueue(item);
            });
        }
        else
        {
            for (var i = 0; i < ItemCount; i++)
            {
                var item = new ChronoQueueItem<int>(9, DateTime.UtcNow.AddSeconds(10));
                _queue.Enqueue(item);
            }
        }
    }
    
    [IterationSetup(Target = nameof(Flush_Items))]
    public void SetupFlush()
    {
        _queue.Flush();

        if (ItemCount >= 100_000)
        {
            Parallel.For(0, ItemCount, i =>
            {
                var item = new ChronoQueueItem<int>(9, DateTime.UtcNow.AddSeconds(10));
                _queue.Enqueue(item);
            });
        }
        else
        {
            for (var i = 0; i < ItemCount; i++)
            {
                var item = new ChronoQueueItem<int>(9, DateTime.UtcNow.AddSeconds(10));
                _queue.Enqueue(item);
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("Enqueue")]
    public void Enqueue_Items()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            var item = new ChronoQueueItem<int>(9, DateTime.UtcNow.AddSeconds(1));
            _queue.Enqueue(item);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Dequeue")]
    public void Dequeue_Items()
    {
        while (_queue.TryDequeue(out _)) { }
    }
    
    [Benchmark]
    [BenchmarkCategory("Flush")]
    public void Flush_Items()
    {
        _queue.Flush();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _queue.Dispose();
    }
}