using BenchmarkDotNet.Attributes;
using ChronoQueue;
using Microsoft.Extensions.Caching.Memory;

namespace ChronoQueueBenchmarks;

[MemoryDiagnoser]
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
        _queue.Dispose();
        _queue = new ChronoQueue<int>();
    }

    [IterationSetup(Target = nameof(Dequeue_Items))]
    public void SetupDequeue()
    {
        _queue.Dispose();
        _queue = new ChronoQueue<int>();

        for (int i = 0; i < ItemCount; i++)
        {
            var item = new ChronoQueueItem<int>(9, DateTime.UtcNow.AddSeconds(10));
            _queue.Enqueue(item);
        }
    }

    [IterationSetup(Target = nameof(FullSweepExpiredItems))]
    public void SetupSweep()
    {
        _queue.Dispose();
        var options = new MemoryCacheOptions
        {
            ExpirationScanFrequency = TimeSpan.FromSeconds(1)
        };
        _queue = new ChronoQueue<int>(options);

        for (var i = 0; i < ItemCount; i++)
        {
            var item = new ChronoQueueItem<int>(9, DateTime.UtcNow.AddMilliseconds(-1));
            _queue.Enqueue(item);
        }
    }

    [Benchmark]
    public void Enqueue_Items()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            var item = new ChronoQueueItem<int>(9, DateTime.UtcNow.AddSeconds(1));
            _queue.Enqueue(item);
        }
    }

    [Benchmark]
    public void Dequeue_Items()
    {
        while (_queue.TryDequeue(out _)) { }
    }

    [Benchmark]
    public async Task FullSweepExpiredItems()
    {
        while (_queue.Count() > 0)
        {
            await Task.Yield();
        }
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _queue.Dispose();
    }
}