using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ChronoQueue;

/// <summary>
/// A thread-safe, in-memory, time-aware FIFO queue that evicts items based on expiration timestamps.
/// </summary>
/// <remarks>
/// Each item is stored once, in a node held by a <see cref="ConcurrentQueue{T}"/> that preserves insertion order.
/// Whoever claims a node first (a dequeue, the background cleanup, or a flush) owns it, so each item is
/// handed out, expired or flushed exactly once.
/// <para>
/// Producers also append each node to a striped intake list. Every <see cref="TickMs"/> the cleanup drains the
/// intake, drops nodes that were already dequeued, and files the rest in time buckets that only the cleanup
/// thread touches. It then expires the buckets that are due, so each tick costs time proportional to new and
/// expiring items, not to everything in the queue.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of values stored in the queue.</typeparam>
public sealed class ChronoQueue<T> : IChronoQueue<T>, IDisposable
{
    /// <summary>Cleanup interval, and the width of one expiry bucket, in milliseconds.</summary>
    internal const long TickMs = 100;

    private static readonly int StripeMask =
        (int)BitOperations.RoundUpToPowerOf2((uint)Math.Clamp(Environment.ProcessorCount, 1, 16)) - 1;

    /// <summary>How many enqueues a thread makes between trims of its intake stripe.</summary>
    private const int IntakeTrimInterval = 256;

    [ThreadStatic] private static int t_enqueuesSinceTrim;

    private readonly ConcurrentQueue<Node> _queue = new();
    private readonly ConcurrentQueue<Node>[] _intake = new ConcurrentQueue<Node>[StripeMask + 1];
    private readonly PeriodicTimer _cleanupTimer = new(TimeSpan.FromMilliseconds(TickMs));
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _isDisposed;
    private int _isFlushing;

    // Owned by the cleanup (guarded by _cleanupLock, which is only ever contended in tests).
    private readonly object _cleanupLock = new();
    private readonly Dictionary<long, List<Node>> _buckets = new();
    private long _processedKey;
    private long _filedNodes;

    // Written on every enqueue and dequeue; padded on both sides so it never shares a cache line
    // with the fields above, which are read on every call.
    private PaddedLong _count;

    private bool IsDisposed => _isDisposed;

    public ChronoQueue() : this(runBackgroundCleanup: true)
    {
    }

    /// <param name="runBackgroundCleanup">
    /// <c>false</c> leaves expiry to <see cref="TryDequeue"/> and explicit <see cref="RemoveExpiredItems"/> calls (tests only).
    /// </param>
    internal ChronoQueue(bool runBackgroundCleanup)
    {
        _processedKey = Environment.TickCount64 / TickMs - 1;
        // Returns at its first await, so this does not tie up a thread.
        if (runBackgroundCleanup)
            _ = RunCleanupAsync();
    }

    /// <summary>
    /// Enqueues a time-aware item into the queue.
    /// </summary>
    /// <param name="item">The <see cref="ChronoQueueItem{T}"/> to enqueue, which includes the value and expiration time.</param>
    /// <exception cref="ChronoQueueItemExpiredException">Thrown if the item has already expired at enqueue time.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the queue has already been disposed.</exception>
    /// <remarks>
    /// Time Complexity: <b>O(1)</b>
    /// </remarks>
    public void Enqueue(ChronoQueueItem<T> item)
    {
        ThrowIfDisposed();

        if (item.IsExpired())
            throw new ChronoQueueItemExpiredException("The item has already expired and cannot be enqueued.");

        var node = new Node(item);
        // Count first, so a racing claim can never push the count below zero.
        Interlocked.Increment(ref _count.Value);
        _queue.Enqueue(node);

        var stripe = IntakeStripe();
        stripe.Enqueue(node);
        if (++t_enqueuesSinceTrim >= IntakeTrimInterval)
        {
            t_enqueuesSinceTrim = 0;
            TrimIntake(stripe);
        }
    }

    /// <summary>
    /// When consumers keep up, most nodes in the intake are claimed long before the next cleanup tick.
    /// Dropping them from the front here keeps the intake small, so they can be collected young.
    /// Stops after a few live nodes, so it costs almost nothing when nobody is dequeuing.
    /// </summary>
    private static void TrimIntake(ConcurrentQueue<Node> stripe)
    {
        // Not TryPeek: it stops ConcurrentQueue from reusing its segments.
        var liveSeen = 0;
        for (var i = 0; i < 2 * IntakeTrimInterval && stripe.TryDequeue(out var node); i++)
        {
            if (!node.IsLive)
                continue;

            stripe.Enqueue(node); // order in the intake doesn't matter
            if (++liveSeen == 4)
                return;
        }
    }

    /// <summary>
    /// Attempts to dequeue the next non-expired item from the queue.
    /// Skips expired items and disposes them if configured.
    /// </summary>
    /// <param name="item">
    /// When this method returns, contains the dequeued item if a non-expired item was found;
    /// otherwise, the default value of <typeparamref name="T"/>.
    /// </param>
    /// <returns><c>true</c> if a non-expired item was dequeued; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the queue has already been disposed.</exception>
    /// <remarks>
    /// Time Complexity:
    /// <list type="bullet">
    ///   <item><description><b>Best Case:</b> O(1) — first item is valid and returned</description></item>
    ///   <item><description><b>Worst Case:</b> O(n) — all items are expired and returns false</description></item>
    /// </list>
    /// </remarks>
    public bool TryDequeue(out T item)
    {
        ThrowIfDisposed();

        while (_queue.TryDequeue(out var node))
        {
            if (!node.TryClaim(out var chronoQueueItem))
                continue; // already expired by the cleanup, or flushed

            Interlocked.Decrement(ref _count.Value);

            if (chronoQueueItem.IsExpired())
            {
                // Expired but not yet reached by the cleanup: never hand it out.
                DisposeOnExpiry(chronoQueueItem);
                continue;
            }

            item = chronoQueueItem.Item;
            return true;
        }

        item = default;
        return false;
    }

    /// <summary>
    /// Gets the current number of active (non-expired) items in the queue.
    /// </summary>
    /// <returns>The current count.</returns>
    /// <remarks>
    /// Time Complexity: <b>O(1)</b>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Count() => Interlocked.Read(ref _count.Value);

    /// <summary>
    /// Atomically flushes the queue, ensuring only one thread performs the operation at a time.
    /// Disposes items as configured and clears all internal state.
    /// Items enqueued while a flush is running may or may not be flushed.
    /// </summary>
    /// <remarks>
    /// Time Complexity: <b>O(n)</b> — where n is the number of total queued items.
    /// </remarks>
    public void Flush()
    {
        // Atomically set _isFlushing to 1 if it was 0; otherwise exit. Only one flush at a time.
        if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0)
            return;

        long claimed = 0;
        try
        {
            // Bounded by the size at the start so a steady stream of producers cannot keep Flush running.
            // Flushed nodes left in the intake and buckets are claimed, so the cleanup just drops them.
            var remaining = _queue.Count;
            while (remaining-- > 0 && _queue.TryDequeue(out var node))
            {
                if (!node.TryClaim(out var chronoQueueItem))
                    continue;

                claimed++;

                if (!DisposeOnExpiry(chronoQueueItem))
                    DisposeOnFlush(chronoQueueItem);
            }
        }
        finally
        {
            // One update for the whole flush instead of one contended decrement per item.
            Interlocked.Add(ref _count.Value, -claimed);
            Volatile.Write(ref _isFlushing, 0);
        }
    }

    /// <summary>
    /// Disposes the queue and cancels internal timers.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        Flush();
        _cts.Cancel();
        _cleanupTimer.Dispose();
        _cts.Dispose();
        _isDisposed = true;
    }

    private async Task RunCleanupAsync()
    {
        try
        {
            while (await _cleanupTimer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
            {
                RemoveExpiredItems();
            }
        }
        catch (OperationCanceledException)
        {
            // Queue disposed.
        }
    }

    /// <summary>
    /// Files newly enqueued items and expires every item whose deadline has passed.
    /// </summary>
    internal void RemoveExpiredItems()
    {
        lock (_cleanupLock)
        {
            var now = Environment.TickCount64;
            var nowKey = now / TickMs;

            TakeIntake(now);

            // Buckets entirely in the past: every live node in them has expired.
            for (var key = _processedKey + 1; key < nowKey; key++)
            {
                if (!_buckets.Remove(key, out var bucket))
                    continue;

                _filedNodes -= bucket.Count;
                foreach (var node in bucket)
                    TryExpire(node);
            }
            _processedKey = Math.Max(_processedKey, nowKey - 1);

            // The current bucket is partly due.
            if (_buckets.TryGetValue(nowKey, out var current))
                _filedNodes -= current.RemoveAll(node => TryExpire(node, now));

            // Dequeued and flushed nodes stay in their bucket until it falls due. Once they far outnumber
            // live items (long TTLs, consumers keeping up), drop them so they don't pin memory.
            if (_filedNodes > 2 * Count() + 4096)
                Compact();
        }
    }

    private void TakeIntake(long now)
    {
        foreach (var stripe in _intake)
        {
            if (stripe is null)
                continue;

            // Only what is there now, so producers can't keep this loop going.
            var remaining = stripe.Count;
            while (remaining-- > 0 && stripe.TryDequeue(out var node))
            {
                if (!node.IsLive || TryExpire(node, now))
                    continue; // already dequeued or flushed (the common case when consumers keep up), or just expired

                var key = node.DeadlineMs / TickMs; // > _processedKey, since the deadline is still ahead
                if (!_buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<Node>();
                    _buckets.Add(key, bucket);
                }

                bucket.Add(node);
                _filedNodes++;
            }
        }
    }

    private void Compact()
    {
        List<long> emptied = null;
        foreach (var (key, bucket) in _buckets)
        {
            _filedNodes -= bucket.RemoveAll(static node => !node.IsLive);
            if (bucket.Count == 0)
                (emptied ??= new List<long>()).Add(key);
        }

        if (emptied is null)
            return;

        foreach (var key in emptied)
            _buckets.Remove(key);
    }

    /// <summary>Expires the node if it is past its deadline and still live. Returns true if the node can be forgotten.</summary>
    private bool TryExpire(Node node, long now)
    {
        if (!node.IsLive)
            return true;

        return now >= node.DeadlineMs && TryExpire(node);
    }

    /// <summary>Expires the node if nobody has claimed it yet.</summary>
    private bool TryExpire(Node node)
    {
        if (!node.TryClaim(out var chronoQueueItem))
            return true;

        Interlocked.Decrement(ref _count.Value);
        DisposeOnExpiry(chronoQueueItem);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ConcurrentQueue<Node> IntakeStripe()
    {
        var index = Thread.GetCurrentProcessorId() & StripeMask;
        return Volatile.Read(ref _intake[index]) ?? CreateIntakeStripe(index);
    }

    private ConcurrentQueue<Node> CreateIntakeStripe(int index)
    {
        var created = new ConcurrentQueue<Node>();
        return Interlocked.CompareExchange(ref _intake[index], created, null) ?? created;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool DisposeOnExpiry(in ChronoQueueItem<T> item)
    {
        // Flag first: it is free, while IsExpired reads the clock.
        if (item.DisposeOnExpiry && item.IsExpired() && item.Item is IDisposable disposableItem)
        {
            disposableItem.Dispose();
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool DisposeOnFlush(in ChronoQueueItem<T> item)
    {
        if (item is { DisposeOnFlush: true, Item: IDisposable disposableItem })
        {
            disposableItem.Dispose();
            return true;
        }
        return false;
    }

    /// <summary>
    /// One queued item, shared by the main queue and the cleanup's intake and buckets.
    /// </summary>
    private sealed class Node
    {
        private const int Live = 0;
        private const int Claimed = 1;

        private ChronoQueueItem<T> _item;
        private int _state;

        public Node(in ChronoQueueItem<T> item)
        {
            _item = item;
            DeadlineMs = item.ExpiryDeadlineMs;
        }

        public long DeadlineMs { get; }

        public bool IsLive => Volatile.Read(ref _state) == Live;

        /// <summary>
        /// Takes ownership of the item. Succeeds for exactly one caller; the node then drops its
        /// reference so a leftover copy in the cleanup's lists does not keep the payload alive.
        /// </summary>
        public bool TryClaim(out ChronoQueueItem<T> item)
        {
            if (Interlocked.CompareExchange(ref _state, Claimed, Live) != Live)
            {
                item = default;
                return false;
            }

            item = _item;
            _item = default;
            return true;
        }
    }
}

/// <summary>
/// A long alone on its own 64-byte cache line, with padding on both sides.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct PaddedLong
{
    [FieldOffset(64)] public long Value;
}
