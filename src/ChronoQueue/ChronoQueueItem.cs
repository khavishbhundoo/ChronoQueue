using System;

namespace ChronoQueue;

public readonly struct ChronoQueueItem<TInner> 
{
    public TInner Item { get; }
    public DateTime ExpiresAt { get; }

    public ChronoQueueItem(TInner item, DateTime expiresAt)
    {
        Item = item;
        ExpiresAt = expiresAt;
    }
}