using ChronoQueue;
using NSubstitute;
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
        result.ShouldBeNull();;
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
        Should.Throw<ChronoQueueItemExpiredException>(() => queue.Enqueue(new ChronoQueueItem<string>("test1", DateTime.UtcNow.AddSeconds(-1))));
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
    }
    
    [Fact]
    public async Task Flush_With_DisposeOnExpiry_Expired_Item_After_Lifetime_Dispose_Should_Be_Called()
    {
        var q = Substitute.For<AwesomeClass>();
        using var queue = new ChronoQueue<AwesomeClass>();
        var item = new ChronoQueueItem<AwesomeClass>(q, DateTime.UtcNow.AddMilliseconds(100), true);

        queue.Enqueue(item);
        queue.Count().ShouldBe(1);
        queue.Flush();
        q.Received(1).Dispose();
    }
}

public class AwesomeClass : IDisposable
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