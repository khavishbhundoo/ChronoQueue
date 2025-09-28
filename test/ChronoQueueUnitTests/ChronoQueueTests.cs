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
            Assert.Equal(totalItems, produced.Count);
            Assert.Equal(totalItems, consumed.Count);
            Assert.Empty(produced.Except(consumed));
        }
    }
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