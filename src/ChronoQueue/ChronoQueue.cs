using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ChronoQueue;

/// <summary>
/// A thread-safe, in-memory, time-aware FIFO queue that evicts items based on expiration timestamps.
/// Combines a <see cref="ConcurrentQueue{T}"/> for insertion order and a <see cref="ConcurrentDictionary{TKey, TValue}"/> for fast lookup and removal.
/// Expired items are proactively cleaned up in the background and are skipped during dequeue operations.
/// </summary>
/// <typeparam name="T">The type of values stored in the queue.</typeparam>
public sealed class ChronoQueue<T> : IChronoQueue<T>, IDisposable
{
    private readonly ConcurrentQueue<long> _queue = new();
    private readonly ConcurrentDictionary<long, ChronoQueueItem<T>> _items = new();
    private readonly PeriodicTimer _cleanupTimer = new(TimeSpan.FromMilliseconds(100));
    private readonly CancellationTokenSource _cts = new();
    private long _count;
    private long _idCounter;
    private volatile bool _isDisposed;
    private int _isFlushing;

    private bool IsDisposed => _isDisposed;

    public ChronoQueue()
    {
        Task.Factory.StartNew(
            ExpiredItemsCleanup,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
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

        var id = Interlocked.Increment(ref _idCounter);
        _queue.Enqueue(id);
        _items.TryAdd(id, item);
        Interlocked.Increment(ref _count);
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

        item = default;

        while (_queue.TryDequeue(out var id))
        {
            if (!_items.TryRemove(id, out var chronoQueueItem))
                continue;
            Interlocked.Decrement(ref _count);

            if (chronoQueueItem.IsExpired())
            {
                if (chronoQueueItem is { DisposeOnExpiry: true, Item: IDisposable disposable })
                    disposable.Dispose();
                continue;
            }

            item = chronoQueueItem.Item;
            return true;
        }

        return false;
    }
    private async ValueTask ExpiredItemsCleanup()
    {
        while (await _cleanupTimer.WaitForNextTickAsync(_cts.Token))
        {
            foreach (var (id, item) in _items)
            {
                if (!item.IsExpired() || !_items.TryRemove(id, out var chronoQueueItem)) continue;

                if (chronoQueueItem is { DisposeOnExpiry: true, Item: IDisposable disposable })
                    disposable.Dispose();

                Interlocked.Decrement(ref _count);
            }
        }
    }
    /// <summary>
    /// Gets the current number of active (non-expired) items in the queue.
    /// </summary>
    /// <returns>The current count.</returns>
    /// <remarks>
    /// Time Complexity: <b>O(1)</b>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Count() => Interlocked.Read(ref _count);

    /// <summary>
    /// Atomically flushes the queue, ensuring only one thread performs the operation at a time.
    /// Disposes expired items and clears all internal state.
    /// </summary>
    /// <remarks>
    /// Time Complexity: <b>O(n)</b> — where n is the number of total queued items.
    /// </remarks>
    public void Flush()
    {
        // Atomically set _isFlushing to 1 if it was 0; otherwise exit.Only one flush at a time
        if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0)
            return;
        
        foreach (var (id, item) in _items)
        {
            if (!item.IsExpired() || !_items.TryRemove(id, out var chronoQueueItem)) continue;

            if (chronoQueueItem is { DisposeOnExpiry: true, Item: IDisposable disposable })
                disposable.Dispose();
        }
        _items.Clear();
        _queue.Clear();
        Interlocked.Exchange(ref _count, 0);
        Interlocked.Exchange(ref _isFlushing, 0);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }
}