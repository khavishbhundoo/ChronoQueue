using System;

namespace ChronoQueue;

public readonly struct ChronoQueueItem<TInner> :  IDisposable
{
    public TInner Item { get; }
    public DateTime ExpiresAt { get; }

    public ChronoQueueItem(TInner item, DateTime expiresAt)
    {
        Item = item;
        ExpiresAt = expiresAt;
    }

    public void Dispose()
    {
        if (Item is IDisposable d)
        {
            d.Dispose();
        }
    }
}