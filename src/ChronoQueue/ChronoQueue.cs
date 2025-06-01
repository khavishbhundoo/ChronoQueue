using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ChronoQueue;

public sealed class ChronoQueue<T> : IChronoQueue<T>, IDisposable
{
    private readonly ConcurrentQueue<Guid> _queue = new();
    private readonly MemoryCache _memoryCache;
    private readonly PostEvictionCallbackRegistration _globalPostEvictionCallback;
    private long _count;

    public ChronoQueue(MemoryCacheOptions options = null)
    {
        _memoryCache = new MemoryCache(options ?? new MemoryCacheOptions()
        {
            ExpirationScanFrequency = TimeSpan.FromSeconds(30)
        });
        
        _globalPostEvictionCallback = new PostEvictionCallbackRegistration
        {
            EvictionCallback = (_, value, reason, _) =>
            {
                if (value is ChronoQueueItem<T> item)
                {
                    item.Dispose();  
                }
                if (reason != EvictionReason.Removed)
                {
                    Interlocked.Decrement(ref _count);
                }
            }
        };
    }

    public void Enqueue(ChronoQueueItem<T> item)
    {
        var id = Guid.NewGuid();
        _queue.Enqueue(id);
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = item.ExpiresAt
        };
        options.PostEvictionCallbacks.Add(_globalPostEvictionCallback);
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
                Interlocked.Decrement(ref _count);
                return true;
            }
        }
        return false;
    }

    public long Count()
    {
        return Interlocked.Read(ref _count);
    }

    public void Dispose()
    {
        _count = 0;
        _memoryCache.Clear();
        _memoryCache.Dispose();
    }
}
