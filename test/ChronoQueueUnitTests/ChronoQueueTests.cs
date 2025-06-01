using ChronoQueue;
using Microsoft.Extensions.Caching.Memory;
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
        result.Item.ShouldBe("test1");
        result.ExpiresAt.ShouldBe(expiryTime);
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
        await Task.Delay(50); // Give time for callback to decrement count
        queue.Count().ShouldBe(0);
        result.Item.ShouldBeNullOrEmpty();
    }
    
    [Fact]
    public void Dequeue_On_EmptyQueue_Should_Return_False()
    {
        using var queue = new ChronoQueue<string>();

        var success = queue.TryDequeue(out var result);

        success.ShouldBeFalse();
        result.Item.ShouldBeNullOrEmpty();
        queue.Count().ShouldBe(0);
    }
}