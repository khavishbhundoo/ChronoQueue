using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ChronoQueue;

namespace ChronoQueueBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
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
    
    [Benchmark]
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