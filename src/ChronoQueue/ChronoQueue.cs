using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace ChronoQueue;

/// <summary>
/// A thread-safe, in-memory, time-aware FIFO queue that evicts items automatically after a specified expiration time.
/// Uses a <b>dedicated instance</b> of <see cref="MemoryCache"/> for time-based eviction and <see cref="ConcurrentQueue{T}"/> for ordering.
/// Ensures fast in-memory access and automatic cleanup of expired entries, making it suitable for lightweight scheduling,
/// TTL-based queues, and temporal buffering scenarios.</summary>
/// <typeparam name="T">The type of items stored in the queue.</typeparam>
public sealed class ChronoQueue<T> : IChronoQueue<T>, IDisposable
{
    private readonly ConcurrentQueue<long> _queue = new();
    private readonly MemoryCache _memoryCache;
    private readonly PostEvictionCallbackRegistration _globalPostEvictionCallback;
    private long _count;
    private long _idCounter;
    private volatile bool _isDisposed;
    
    private bool IsDisposed => _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChronoQueue{T}"/> class.
    /// The provided <paramref name="options"/> allows customization of MemoryCacheOptions.
    /// Note: This instance creates and owns its own <see cref="MemoryCache"/>
    /// </summary>
    public ChronoQueue(MemoryCacheOptions options = null)
    {
        _memoryCache = new MemoryCache(options ?? new MemoryCacheOptions()
        {
            ExpirationScanFrequency = TimeSpan.FromSeconds(30)
        });
        
        _globalPostEvictionCallback = new PostEvictionCallbackRegistration
        {
            EvictionCallback = OnEvicted
        };
    }
    
    private void OnEvicted(object key, object value, EvictionReason reason, object state)
    {
        Interlocked.Decrement(ref _count);
    }
    
    /// <summary>
    /// Enqueues a time-aware item into the queue.
    /// </summary>
    /// <param name="item">The item to enqueue, including its expiration timestamp.</param>
    /// <exception cref="ChronoQueueItemExpiredException">
    /// Thrown if the item has already expired at the time of insertion.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the queue has already been disposed.
    /// </exception>
    public void Enqueue(ChronoQueueItem<T> item)
    {
        ThrowIfDisposed();
        
        if(item.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ChronoQueueItemExpiredException("The item has already expired and cannot be enqueued.");
        
        var id = Interlocked.Increment(ref _idCounter);
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = item.ExpiresAt
        };
        options.PostEvictionCallbacks.Add(_globalPostEvictionCallback);
        _queue.Enqueue(id);    
        _memoryCache.Set(id, item.Item, options);
        Interlocked.Increment(ref _count);
    }

    /// <summary>
    /// Attempts to dequeue the next non-expired item from the queue and internal <see cref="MemoryCache"/>.
    /// </summary>
    /// <param name="item">The dequeued item, if available.</param>
    /// <returns>True if an item was dequeued; false if the queue was empty or all items were expired.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the queue has already been disposed.
    /// </exception>
    public bool TryDequeue(out T item)
    {
        ThrowIfDisposed();
        
        item = default;

        while (_queue.TryDequeue(out var id))
        {
            if (_memoryCache.TryGetValue(id, out item))
            {
                _memoryCache.Remove(id);
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Returns the current number of active (non-expired) items in the queue.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Count()
    {
        return Interlocked.Read(ref _count);
    }
    
    /// <summary>
    /// Clears all queued items and resets the internal state.
    /// </summary>
    public void Flush()
    {
        _queue.Clear();
        if (!IsDisposed) 
            _memoryCache.Clear();
        Interlocked.Exchange(ref _count, 0);
    }
    
    public void Dispose()
    {
        if (IsDisposed) return;
        Flush();
        _memoryCache.Dispose();
        _isDisposed = true;
    }
    
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }
}
