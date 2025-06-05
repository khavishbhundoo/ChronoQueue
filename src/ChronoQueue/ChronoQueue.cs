using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace ChronoQueue;

public sealed class ChronoQueue<T> : IChronoQueue<T>, IDisposable
{
    private readonly ConcurrentQueue<long> _queue = new();
    private readonly MemoryCache _memoryCache;
    private readonly PostEvictionCallbackRegistration _globalPostEvictionCallback;
    private long _count;
    private long _idCounter;
    private volatile bool _isDisposed;
    
    private bool IsDisposed => _isDisposed;

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

    public void Enqueue(ChronoQueueItem<T> item)
    {
        ThrowIfDisposed();
        
        if(item.ExpiresAt <= DateTimeOffset.UtcNow)
            return;
        
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Count()
    {
        return Interlocked.Read(ref _count);
    }
    
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
