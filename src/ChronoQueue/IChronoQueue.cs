using System;

namespace ChronoQueue;

public interface IChronoQueue<T>
{
    void Enqueue(ChronoQueueItem<T> item);
    bool TryDequeue(out ChronoQueueItem<T> item);
    long Count();
}