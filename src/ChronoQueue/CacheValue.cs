using System;

namespace ChronoQueue;

internal readonly struct CacheValue<T> : IDisposable
{
    public readonly T Item;
    private readonly bool _disposeOnExpiry;

    public CacheValue(T item, bool disposeOnExpiry)
    {
        Item = item;
        _disposeOnExpiry = disposeOnExpiry;
    }

    public void Dispose()
    {
        if (_disposeOnExpiry && Item is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}