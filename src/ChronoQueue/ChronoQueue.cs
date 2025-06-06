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
    private readonly ConcurrentQueue<long> _queue = new();
    private readonly MemoryCache _memoryCache;
    private readonly PostEvictionCallbackRegistration _globalPostEvictionCallback;
    private readonly PeriodicTimer _cleanupTimer = new(TimeSpan.FromMilliseconds(100));
    private readonly CancellationTokenSource _cts = new();
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
        
        Task.Factory.StartNew(
            AdaptiveExpiredItemsCleanup,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }
    
    private void OnEvicted(object key, object value, EvictionReason reason, object state)
    {
        if (value is (T item, bool dispose))
        {
            if (dispose && reason != EvictionReason.Removed && item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        Interlocked.Decrement(ref _count);
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
    public void Enqueue(ChronoQueueItem<T> item)
    {
        ThrowIfDisposed();
        
        if(item.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ChronoQueueItemExpiredException("The item has already expired and cannot be enqueued.");
        
        var id = Interlocked.Increment(ref _idCounter);
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = item.ExpiresAt,
            Priority = CacheItemPriority.NeverRemove,
        };
        options.PostEvictionCallbacks.Add(_globalPostEvictionCallback);
        _queue.Enqueue(id);    
        _memoryCache.Set(id, (item.Item, item.DisposeOnExpiry), options);
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
            if (_memoryCache.TryGetValue(id, out (T,bool) cachedValue))
            {
                _memoryCache.Remove(id);
                item = cachedValue.Item1;
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

            if (Count() > 0)
            {
                _memoryCache.Compact(compactFraction);
                //Console.WriteLine($"[Cleanup] Count={Count()}, Mem={memoryUsed / 1024 / 1024}MB, Compact={compactFraction}");
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
