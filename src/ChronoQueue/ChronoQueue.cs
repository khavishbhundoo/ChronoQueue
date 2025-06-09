using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly ConcurrentQueue<QueueValue> _queue = new();
    private readonly MemoryCache _memoryCache;
    private readonly PostEvictionCallbackRegistration _globalPostEvictionCallback;
    private readonly PeriodicTimer _cleanupTimer = new(TimeSpan.FromMilliseconds(1));
    private readonly CancellationTokenSource _cts = new();
    private long _count;
    private long _idCounter;
    private volatile bool _isDisposed;

    /// Optimistically tracks earliest expiry to reduce compaction; not always precise under concurrency.
    private long _nextExpiryTicks = long.MaxValue;
    
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
        
        Task.Factory.StartNew(
            AdaptiveExpiredItemsCleanup,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }
    
    private void OnEvicted(object key, object value, EvictionReason reason, object state)
    {
        if (value is not CacheValue<T> cacheItem) return;
        if (reason == EvictionReason.Removed) return;
        Interlocked.Decrement(ref _count);
                
        if (cacheItem.DisposeOnExpiry && cacheItem.Item is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
    
    /// <summary>
    /// Enqueues a time-aware item into the queue.
    /// </summary>
    /// The <see cref="ChronoQueueItem{T}"/> to enqueue, containing the value and its absolute expiration time.
    /// <exception cref="ChronoQueueItemExpiredException">
    /// Thrown if the item has already expired at the time of insertion.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the queue has already been disposed.
    /// </exception>
    /// <remarks>
    /// Time complexity: O(1)
    /// </remarks>
    public void Enqueue(ChronoQueueItem<T> item)
    {
        ThrowIfDisposed();
        
        if(item.IsExpired)
            throw new ChronoQueueItemExpiredException("The item has already expired and cannot be enqueued.");

        object id = Interlocked.Increment(ref _idCounter);
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = item.ExpiresAt,
            Priority = CacheItemPriority.NeverRemove
        };
        options.PostEvictionCallbacks.Add(_globalPostEvictionCallback);
        
        _queue.Enqueue(new QueueValue(id, item.ExpiryDeadlineTicks));
        _memoryCache.Set(id, new CacheValue<T>(item.Item, item.DisposeOnExpiry), options);
        Interlocked.Increment(ref _count);
        
        var expiryTicks = item.ExpiresAt.UtcTicks;
        var nextExpiryTicks = Interlocked.Read(ref _nextExpiryTicks);
        
        if (expiryTicks < nextExpiryTicks)
        {
            Interlocked.CompareExchange(ref _nextExpiryTicks, expiryTicks, nextExpiryTicks);
        }
    }

    /// <summary>
    /// Attempts to dequeue the next non-expired item from the queue and internal <see cref="MemoryCache"/>.
    /// </summary>
    /// <param name="item">The dequeued item, if available.</param>
    /// <returns>True if an item was dequeued; false if the queue was empty or all items were expired.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the queue has already been disposed.
    /// </exception>
    /// <remarks>
    /// Time complexity:
    /// Best case: O(1) – first item is valid.
    /// Worst case: O(n) – all items are expired.
    ///
    /// Expired items are removed from the internal cache automatically via background compaction,
    /// but their corresponding IDs remain in the queue until dequeued.
    /// </remarks>
    public bool TryDequeue(out T item)
    {
        ThrowIfDisposed();
        
        item = default;
        while (_queue.TryDequeue(out var queueValue))
        {
            if (Environment.TickCount64 >= queueValue.ExpiryDeadlineTicks)
                continue;
            
            if (_memoryCache.TryGetValue(queueValue.Id, out CacheValue<T> cachedValue))
            {
                _memoryCache.Remove(queueValue.Id);
                Interlocked.Decrement(ref _count);
                item = cachedValue.Item;
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Returns the current number of active (non-expired) items in the queue.
    /// </summary>
    /// <remarks>
    /// Time complexity: O(1)
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Count()
    {
        return Interlocked.Read(ref _count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasItemsExpired()
    {
       return Interlocked.Read(ref _nextExpiryTicks) < DateTimeOffset.UtcNow.UtcTicks;
    }
    
    /// <summary>
    /// Clears all queued items and resets the internal state.
    /// </summary>
    /// <remarks>
    /// Time complexity: O(n) – where n is the number of items in the queue.
    /// </remarks>
    public void Flush()
    {
        _queue.Clear();
        if (!IsDisposed) 
            _memoryCache.Clear();
        Interlocked.Exchange(ref _count, 0);
        Interlocked.Exchange(ref _nextExpiryTicks, long.MaxValue);

    }
    
    private async ValueTask AdaptiveExpiredItemsCleanup()
    {
        const long mb = 1024 * 1024;
        const long lowPressure = 64 * mb;
        const long midPressure = 128 * mb;
        const long highPressure = 256 * mb;
        
        while (await _cleanupTimer.WaitForNextTickAsync(_cts.Token))
        {
            var memoryUsed = GC.GetTotalMemory(false); // In bytes
            
            var compactFraction = memoryUsed switch
            {
                < lowPressure => Count() < 10_000 ? 0.05 : 0.10,
                < midPressure => Count() < 10_000 ? 0.10 : 0.15,
                < highPressure => 0.20,
                _ => 0.25
            };
            
            if(Count() == 0)
                Interlocked.Exchange(ref _nextExpiryTicks, long.MaxValue);

            if (HasItemsExpired() && Count() > 0)
            {
                _memoryCache.Compact(compactFraction);
            }
        }
    }

    
    public void Dispose()
    {
        if (IsDisposed) return;
        Flush();
        _cts.Cancel();
        _cleanupTimer.Dispose();
        _memoryCache.Dispose();
        _cts.Dispose();
        _isDisposed = true;
    }
    
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }
}
