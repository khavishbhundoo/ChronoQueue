using System;
using System.Collections.Concurrent;
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

    public ChronoQueue(MemoryCacheOptions options = null)
    {
        _memoryCache = new MemoryCache(options ?? new MemoryCacheOptions()
        {
            ExpirationScanFrequency = TimeSpan.FromSeconds(30)
        });
        
        _globalPostEvictionCallback = new PostEvictionCallbackRegistration
        {
            EvictionCallback = (_, _, _, _) =>
            {
                Interlocked.Decrement(ref _count);
            }
        };
    }

    public void Enqueue(ChronoQueueItem<T> item)
    {
        if(item.ExpiresAt <= DateTimeOffset.UtcNow)
            return;
        
        var id = GetNextCacheKey();
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = item.ExpiresAt
        };
        options.PostEvictionCallbacks.Add(_globalPostEvictionCallback);
        _queue.Enqueue(id);    
        _memoryCache.Set(id, item, options);
        Interlocked.Increment(ref _count);
    }

    public bool TryDequeue(out ChronoQueueItem<T> item)
    {
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
    
    public bool TryPeek(out ChronoQueueItem<T> item)
    {
        item = default;

        while (_queue.TryPeek(out var id))
        {
            if (_memoryCache.TryGetValue(id, out item))
            {
                return true;
            }
            _memoryCache.Remove(id);
        }
        return false;
    }

    public long Count()
    {
        return Interlocked.Read(ref _count);
    }
    
    public void Flush()
    {
        _queue.Clear();
        _memoryCache.Clear();
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _queue.Clear();
    }

    private long GetNextCacheKey() => Interlocked.Increment(ref _idCounter);
}
