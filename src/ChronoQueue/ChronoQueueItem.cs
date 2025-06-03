using System;

namespace ChronoQueue;

public readonly struct ChronoQueueItem<TInner> 
{
    public TInner Item { get; }
    public DateTimeOffset ExpiresAt { get; }

    public ChronoQueueItem(TInner item, DateTimeOffset expiresAt)
    {
        Item = item;
        ExpiresAt = expiresAt;
    }
}