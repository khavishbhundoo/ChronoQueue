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
            EvictionCallback = (_, value, reason, _) =>
            {
                if (reason != EvictionReason.Removed)
                {
                    Interlocked.Decrement(ref _count);
                }
            }
        };
    }

    public void Enqueue(ChronoQueueItem<T> item)
    {
        var id = GetNextId();
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
        _queue.Clear();
        _memoryCache.Clear();
        _memoryCache.Dispose();
    }

    private long GetNextId() => Interlocked.Increment(ref _idCounter);
}
