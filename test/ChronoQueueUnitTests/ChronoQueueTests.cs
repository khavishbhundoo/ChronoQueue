using System.Collections.Concurrent;
using ChronoQueue;
using Shouldly;

namespace ChronoQueueUnitTests;

public class ChronoQueueTests
{
    [Fact]
    public void Enqueue_Then_Dequeue_Should_Return_Item()
    {
        using var queue = new ChronoQueue<string>();
        var expiryTime = DateTime.UtcNow.AddSeconds(1);
        var item = new ChronoQueueItem<string>("test1", expiryTime);
        
        item.GetUtcExpiryTime().ShouldBe(expiryTime);
        queue.Enqueue(item);
        queue.Count().ShouldBe(1);

        var success = queue.TryDequeue(out var result);

        success.ShouldBeTrue();
        result.ShouldBe("test1");
        queue.Count().ShouldBe(0);
    }

    [Fact]
    public async Task Enqueue_Item_Should_Expire_After_Lifetime()
    {
        using var queue = new ChronoQueue<string>();
        var item = new ChronoQueueItem<string>("test2", DateTime.UtcNow.AddMilliseconds(100));

        queue.Enqueue(item);
        queue.Count().ShouldBe(1);

        await Task.Delay(200);

        var success = queue.TryDequeue(out var result);
        success.ShouldBeFalse();
        queue.Count().ShouldBe(0);
        result.ShouldBeNull();
    }

    [Fact]
    public void Dequeue_On_EmptyQueue_Should_Return_False()
    {
        using var queue = new ChronoQueue<string>();

        var success = queue.TryDequeue(out var result);

        success.ShouldBeFalse();
        result.ShouldBeNull();
        ;
        queue.Count().ShouldBe(0);
    }

    [Fact]
    public void Enqueue_Then_Flush_Should_Return_Empty_Queue()
    {
        using var queue = new ChronoQueue<string>();
        var expiryTime = DateTime.UtcNow.AddSeconds(1);
        var item = new ChronoQueueItem<string>("test1", expiryTime);


        queue.Enqueue(item);
        queue.Count().ShouldBe(1);

        queue.Flush();
        queue.Count().ShouldBe(0);
    }

    [Fact]
    public async Task Enqueue_ExpiredItem_Then_Should_Throw_ChronoQueueItemExpiredException()
    {
        using var queue = new ChronoQueue<string>();
        var expiryTime = DateTime.UtcNow.AddSeconds(1);
        var item = new ChronoQueueItem<string>("test1", expiryTime);
        await Task.Delay(1000);
        Should.Throw<ChronoQueueItemExpiredException>(() =>
            queue.Enqueue(new ChronoQueueItem<string>("test1", DateTime.UtcNow.AddSeconds(-1))));
        Should.Throw<ChronoQueueItemExpiredException>(() => queue.Enqueue(item));
    }

    [Fact]
    public async Task Enqueue_With_DisposeOnExpiry_Expired_Item_After_Lifetime_Dispose_Should_Be_Called()
    {
        var q = new AwesomeClass();
        using var queue = new ChronoQueue<AwesomeClass>();
        var item = new ChronoQueueItem<AwesomeClass>(q, DateTime.UtcNow.AddMilliseconds(100), true);

        queue.Enqueue(item);
        queue.Count().ShouldBe(1);

        await Task.Delay(200);

        var success = queue.TryDequeue(out var result);
        success.ShouldBeFalse();
        queue.Count().ShouldBe(0);
        result.ShouldBeNull();
        Should.Throw<ObjectDisposedException>(() => q.Dispose());

        var item2 = new ChronoQueueItem<AwesomeClass>(new AwesomeClass(), DateTime.UtcNow.AddMilliseconds(100), true);
        await Task.Delay(200);
        ChronoQueue<AwesomeClass>.DisposeOnExpiry(item2).ShouldBeTrue();
    }

    [Fact]
    public void Flush_With_DisposeOnFlush_Item_Dispose_Should_Be_Called()
    {
        var q = new AwesomeClass();
        using var queue = new ChronoQueue<AwesomeClass>();
        var item = new ChronoQueueItem<AwesomeClass>(q, DateTime.UtcNow.AddMilliseconds(1000), false, true);

        queue.Enqueue(item);
        queue.Count().ShouldBe(1);
        queue.Flush();
        Should.Throw<ObjectDisposedException>(() => q.Dispose());

        var item2 = new ChronoQueueItem<AwesomeClass>(new AwesomeClass(), DateTime.UtcNow.AddMilliseconds(1000), false,
            true);
        ChronoQueue<AwesomeClass>.DisposeOnFlush(item2).ShouldBeTrue();
    }

    [Fact]
    public async Task TryDequeue_Should_Not_Return_Expired_Item_Before_Cleanup_Runs()
    {
        // No background cleanup, so only TryDequeue can see the expired item.
        using var queue = new ChronoQueue<string>(runBackgroundCleanup: false);
        var item = new ChronoQueueItem<string>("stale", DateTime.UtcNow.AddMilliseconds(50));
        queue.Enqueue(item);
        queue.Enqueue(new ChronoQueueItem<string>("fresh", DateTime.UtcNow.AddSeconds(10)));
        await SpinUntilExpired(item);

        queue.TryDequeue(out var result).ShouldBeTrue();
        result.ShouldBe("fresh");
        queue.Count().ShouldBe(0);
    }

    [Fact]
    public async Task TryDequeue_Skipping_Expired_DisposeOnExpiry_Item_Should_Decrement_Count()
    {
        using var queue = new ChronoQueue<AwesomeClass>(runBackgroundCleanup: false);
        var payload = new AwesomeClass();
        var item = new ChronoQueueItem<AwesomeClass>(payload, DateTime.UtcNow.AddMilliseconds(50), true);
        queue.Enqueue(item);
        await SpinUntilExpired(item);

        queue.TryDequeue(out var result).ShouldBeFalse();
        result.ShouldBeNull();
        queue.Count().ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => payload.Dispose());
    }

    [Fact]
    public async Task Expired_Items_Should_Be_Removed_Without_Dequeue()
    {
        using var queue = new ChronoQueue<string>();
        for (var i = 0; i < 1_000; i++)
            queue.Enqueue(new ChronoQueueItem<string>("short", DateTime.UtcNow.AddMilliseconds(100 + i % 300)));
        queue.Enqueue(new ChronoQueueItem<string>("long", DateTime.UtcNow.AddSeconds(30)));
        queue.Count().ShouldBe(1_001);

        await WaitFor(() => queue.Count() == 1, TimeSpan.FromSeconds(3));

        queue.Count().ShouldBe(1);
        queue.TryDequeue(out var result).ShouldBeTrue();
        result.ShouldBe("long");
    }

    [Fact]
    public async Task Each_Expired_Item_Should_Be_Disposed_Exactly_Once_Under_Concurrency()
    {
        using var queue = new ChronoQueue<CountingDisposable>();
        var payloads = new ConcurrentBag<CountingDisposable>();
        var producers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 5_000; i++)
            {
                var payload = new CountingDisposable();
                payloads.Add(payload);
                queue.Enqueue(new ChronoQueueItem<CountingDisposable>(payload,
                    DateTime.UtcNow.AddMilliseconds(Random.Shared.Next(50, 400)), true));
            }
        })).ToArray();
        var dequeued = 0;
        var consumer = Task.Run(async () =>
        {
            while (!producers.All(p => p.IsCompleted) || queue.Count() > 0)
            {
                if (queue.TryDequeue(out _)) Interlocked.Increment(ref dequeued);
                else await Task.Delay(1);
            }
        });

        await Task.WhenAll(producers);
        await consumer;

        queue.Count().ShouldBe(0);
        payloads.Count(p => p.DisposeCount > 1).ShouldBe(0);
        // Everything not handed out expired, and was disposed once.
        payloads.Count(p => p.DisposeCount == 1).ShouldBe(payloads.Count - dequeued);
    }

    [Fact]
    public void Flush_Should_Keep_Accepting_Items()
    {
        using var queue = new ChronoQueue<string>();
        for (var i = 0; i < 100; i++)
            queue.Enqueue(new ChronoQueueItem<string>("a", DateTime.UtcNow.AddSeconds(10)));
        queue.Flush();
        queue.Count().ShouldBe(0);
        queue.TryDequeue(out _).ShouldBeFalse();

        queue.Enqueue(new ChronoQueueItem<string>("b", DateTime.UtcNow.AddSeconds(10)));
        queue.Count().ShouldBe(1);
        queue.TryDequeue(out var result).ShouldBeTrue();
        result.ShouldBe("b");
    }

    private static async Task SpinUntilExpired<T>(ChronoQueueItem<T> item)
    {
        while (!item.IsExpired())
            await Task.Delay(5);
    }

    private static async Task WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    [Theory]
    [InlineData(4, 50_000, 5)] // 4 producers, 50k items each, 5 iterations
    [InlineData(2, 100_000, 3)] // 2 producers, 100k items each, 3 iterations
    public async Task Enqueue_Dequeue_ShouldNotLoseItems_UnderConcurrency(
        int producerCount,
        int itemsPerProducer,
        int iterations)
    {
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var queue = new ChronoQueue<int>();
            var totalItems = producerCount * itemsPerProducer;
            var produced = new ConcurrentBag<int>();
            var consumed = new ConcurrentBag<int>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            // Start producers
            var producers = Enumerable.Range(0, producerCount)
                .Select(_ => Task.Run(() =>
                {
                    for (var i = 0; i < itemsPerProducer; i++)
                    {
                        var item = new ChronoQueueItem<int>(i, DateTime.UtcNow.AddSeconds(10), true);
                        queue.Enqueue(item);
                        produced.Add(item.Item);
                    }
                }, cts.Token))
                .ToArray();

            // Start consumer
            var consumer = Task.Run(() =>
            {
                while (!cts.IsCancellationRequested && consumed.Count < totalItems)
                {
                    if (queue.TryDequeue(out var item))
                    {
                        consumed.Add(item);
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
            }, cts.Token);

            await Task.WhenAll(Task.WhenAll(producers), consumer);

            // Assert: no items were lost
            produced.Count.ShouldBe(totalItems, $"Iteration {iteration}: All produced items should be counted");
            consumed.Count.ShouldBe(totalItems, $"Iteration {iteration}: All consumed items should be counted");
            produced.Except(consumed).ShouldBeEmpty($"Iteration {iteration}: No items should be lost in the queue");
        }
    }
}


class CountingDisposable : IDisposable
{
    private int _disposeCount;
    public int DisposeCount => Volatile.Read(ref _disposeCount);
    public void Dispose() => Interlocked.Increment(ref _disposeCount);
}

class AwesomeClass : IDisposable
{
    private volatile bool _disposed;
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        else
        {
            throw new ObjectDisposedException(nameof(AwesomeClass));
        }
    }
}