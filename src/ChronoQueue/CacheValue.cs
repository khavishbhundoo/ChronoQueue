namespace ChronoQueue;

using System;

internal sealed class CacheValue<T> : IDisposable
{
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

    
    public T Item { get; }
}