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
    public void Enqueue_Then_Peek_Should_Return_Item_Without_Removing_It()
    {
        using var queue = new ChronoQueue<string>();
        var expiryTime = DateTime.UtcNow.AddSeconds(1);
        var item = new ChronoQueueItem<string>("test1", expiryTime);


        queue.Enqueue(item);
        queue.Count().ShouldBe(1);

        var success = queue.TryPeek(out var result);

        success.ShouldBeTrue();
        result.ShouldBe("test1");
        queue.Count().ShouldBe(1);
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
        
        await Task.Delay(5); // Give time for callback to decrement count
        
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
}