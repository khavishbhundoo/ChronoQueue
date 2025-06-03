using BenchmarkDotNet.Attributes;
using ChronoQueue;
using Microsoft.Extensions.Caching.Memory;

namespace ChronoQueueBenchmark;

[MemoryDiagnoser]
public class ChronoQueueBenchmarks : IDisposable
{
    private ChronoQueue<string> _queue;

    [Params(100,1000,10_000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        _queue = new ChronoQueue<string>();
    }

    [IterationSetup(Target = nameof(Enqueue_Items))]
    public void SetupEnqueue()
    {
        _queue.Dispose();
        _queue = new ChronoQueue<string>();
    }

    [IterationSetup(Target = nameof(Dequeue_Items))]
    public void SetupDequeue()
    {
        _queue.Dispose();
        _queue = new ChronoQueue<string>();

        for (var i = 0; i < ItemCount; i++)
        {
            var item = new ChronoQueueItem<string>("item", DateTime.UtcNow.AddSeconds(10));
            _queue.Enqueue(item);
        }
    }

    [Benchmark]
    public void Enqueue_Items()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            var item = new ChronoQueueItem<string>("item", DateTime.UtcNow.AddSeconds(1));
            _queue.Enqueue(item);
        }
    }

    [Benchmark]
    public void Dequeue_Items()
    {
        while (_queue.TryDequeue(out _)) { }
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _queue.Dispose();
    }
}