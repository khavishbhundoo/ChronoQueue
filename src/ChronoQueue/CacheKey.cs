using System;

namespace ChronoQueue;

internal readonly struct CacheKey : IEquatable<CacheKey>
{
    public readonly long Id;
    public CacheKey(long id) => Id = id;

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is CacheKey other && Equals(other);
    }

    public bool Equals(CacheKey other)
    {
        return Id == other.Id;
    }
}