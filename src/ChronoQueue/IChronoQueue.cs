namespace ChronoQueue;

public interface IChronoQueue<T>
{
    void Enqueue(ChronoQueueItem<T> item);
    bool TryDequeue(out ChronoQueueItem<T> item);
    bool TryPeek(out ChronoQueueItem<T> item);
    void Flush();
    long Count();
}