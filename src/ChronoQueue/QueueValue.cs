namespace ChronoQueue;

internal readonly struct QueueValue
{
    public readonly long Id;
    public readonly long ExpiryDeadlineTicks;

    public QueueValue(long id, long expiryDeadlineTicks)
    {
        Id = id;
        ExpiryDeadlineTicks = expiryDeadlineTicks;
    }
}