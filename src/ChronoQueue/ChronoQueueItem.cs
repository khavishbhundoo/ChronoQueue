using System;
using System.Runtime.CompilerServices;

namespace ChronoQueue;

/// <summary>
/// Represents an immutable, time-aware item to be enqueued in a <see cref="ChronoQueue{T}"/>.
/// Each item includes a value and an expiration timestamp, which is normalized to UTC internally
/// to ensure consistent expiry evaluation.
/// </summary>
/// <typeparam name="T">The type of the payload stored in the item.</typeparam>
public readonly struct ChronoQueueItem<T> : IDisposable
{
    /// <summary>
    /// Gets the actual value stored in the queue item.
    /// </summary>
    public T Item { get; }
    
    /// <summary>
    /// Gets the absolute expiration time of the item in UTC.
    /// Internally normalized using <see cref="DateTimeOffset.ToUniversalTime"/> to ensure consistent comparisons against <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }
    
    /// <summary>
    /// Indicates whether the item should be automatically disposed when it expires and is removed from the queue.
    /// This applies only if <typeparamref name="T"/> is a reference type that implements <see cref="IDisposable"/>.
    /// When set to <c>true</c>, attempting to use the item after expiration may result in exceptions due to the object being disposed.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool DisposeOnExpiry { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChronoQueueItem{T}"/> struct with the specified value and expiration time.
    /// </summary>
    /// <param name="item">The value to store in the queue.</param>
    /// <param name="expiresAt">
    /// The absolute expiration time of the item. This value is automatically converted to UTC using <see cref="DateTimeOffset.ToUniversalTime"/>.
    /// </param>
    /// <param name="disposeOnExpiry">
    /// Specifies whether the item should be automatically disposed when it expires and is removed from the queue.
    /// Applicable only if <typeparamref name="T"/> is a reference type implementing <see cref="IDisposable"/>.
    /// If set to <c>true</c> and the item expires before dequeuing, accessing it afterward may result in exceptions due to it being disposed.
    /// Defaults to <c>false</c>.
    /// </param>
    public ChronoQueueItem(T item, DateTimeOffset expiresAt, bool disposeOnExpiry = false)
    {
        Item = item;
        ExpiresAt = expiresAt.ToUniversalTime();
        DisposeOnExpiry = disposeOnExpiry;
        
        var now = DateTime.UtcNow;
        if(ExpiresAt <= now)
            throw new ChronoQueueItemExpiredException("The item has already expired and cannot be enqueued.");
        
        var duration = ExpiresAt - now;
        var nowTicks = Environment.TickCount64;
        ExpiryDeadlineTicks = nowTicks + (long)duration.TotalMilliseconds;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExpired() => Environment.TickCount64 >= ExpiryDeadlineTicks;
    
    /// <summary>
    /// Tick count (based on Environment.TickCount64) at which this item expires.
    /// </summary>
    internal long ExpiryDeadlineTicks { get; }

    public void Dispose()
    {
        if (Item is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}