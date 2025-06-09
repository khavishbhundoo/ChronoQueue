namespace ChronoQueue;

internal sealed record CacheValue<T>
{
    public readonly T Item;
    public readonly bool DisposeOnExpiry;

    public CacheValue(T item, bool disposeOnExpiry)
    {
        Item = item;
        DisposeOnExpiry = disposeOnExpiry;
    }
}