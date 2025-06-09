namespace ChronoQueue;

internal readonly struct QueueValue
{
    public readonly object Id;
    public readonly long ExpiryDeadlineTicks;

    public QueueValue(object id, long expiryDeadlineTicks)
    {
        Id = id;
        ExpiryDeadlineTicks = expiryDeadlineTicks;
    }
}