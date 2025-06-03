namespace ChronoQueue;

public interface IChronoQueue<T>
{
    void Enqueue(ChronoQueueItem<T> item);
    bool TryDequeue(out T item);
    bool TryPeek(out T item);
    void Flush();
    long Count();
}