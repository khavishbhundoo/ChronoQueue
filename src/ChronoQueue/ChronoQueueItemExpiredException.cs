using System;

namespace ChronoQueue;

public class ChronoQueueItemExpiredException : Exception
{
    public ChronoQueueItemExpiredException(string message) : base(message) { }
}