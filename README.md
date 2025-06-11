[![NuGet](https://img.shields.io/nuget/v/khavishbhundoo.ChronoQueue.svg)](https://www.nuget.org/packages/khavishbhundoo.ChronoQueue/)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=coverage)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)

# ChronoQueue
ChronoQueue is a thread-safe, time-aware FIFO queue with automatic item expiration. It is designed for scenarios where you need time-based eviction of in-memory data, such as TTL-based task buffering, lightweight scheduling, or caching with strict ordering.
Internally, it combines a ConcurrentQueue for preserving FIFO ordering and a ConcurrentDictionary + PeriodicTimer for managing expiration and cleanup of stale items.

## 🚀 Features

- ✅ FIFO ordering
- 🕒 Per-item TTL using `DateTimeOffset`
- 🧹 Background cleanup every 100 ms for near realtime eviction of expired items 
- ⚡ Fast in-memory access (no additional locks or semaphores at ChronoQueue level)
- 🛡 Thread-safe, designed for high-concurrency use cases
- 🧯 Disposal-aware and safe to use in long-lived applications

## 📘 API Overview

### `ChronoQueue<T>`

| Method | Description                                                                                                                          |
|--------|--------------------------------------------------------------------------------------------------------------------------------------|
| `Enqueue(ChronoQueueItem<T>)` | Adds an item with an expiration timestamp. Throws if item is already expired or queue is disposed.                                   |
| `TryDequeue(out T item)` | Attempts to dequeue the next **non-expired** item, otherwise return default(T) . Returns `true` if non-expired found otherwise false |
| `Count()` | Returns the current number of active (non-expired) items in the queue.                                                               |
| `Flush()` | Clears the queue and cache, resetting all internal state.                                                                            |
| `Dispose()` | Disposes all resources, cancels cleanup, and resets the queue.                                                                       |


**Note**: If `T` is a reference type that implements `IDisposable` , it is disposed on expiry only if `DisposeOnExpiry` was set to true when creating
`ChronoQueueItem`.

### `ChronoQueue<T>`



| Property          | Description                                                               |
|-------------------|---------------------------------------------------------------------------|
| `Item`            | Adds an item of type T to the queue                                       |
| `ExpiresAt`       | Absolute time at which item T should expire in as a DateTimeOffset        |
| `DisposeOnExpiry` | Auto-dispose expired IDisposable items if true otherwise false by default |


Examples of usage for methods above can be found in this README and in unit tests

---

## 🛑 Exceptions

- **`ChronoQueueItemExpiredException`**  
  Thrown when you attempt to enqueue an item that has already expired. This ensures expired data does not enter the queue.

- **`ObjectDisposedException`**  
  Thrown if any method is invoked on the queue after it has been disposed. This protects against operations on invalid or released resources.

## 📦 Usage
Install the package via NuGet:

`dotnet add package khavishbhundoo.ChronoQueue`

```csharp
using ChronoQueue;
var queue = new ChronoQueue<string>();

queue.Enqueue(new ChronoQueueItem<string>("Hello", DateTimeOffset.UtcNow.AddSeconds(10)));
queue.Enqueue(new ChronoQueueItem<string>("World", DateTimeOffset.UtcNow.AddSeconds(30)));
Console.WriteLine(queue.Count()); // 2

if (queue.TryDequeue(out var result))
{
    Console.WriteLine(result); // "Hello"
}
Console.WriteLine(queue.Count()); // 1 since "Hello" is dequeued
await Task.Delay(TimeSpan.FromSeconds(30)); // wait for expiry 
queue.TryDequeue(out _) // false since no new fresh item is in queue
Console.WriteLine(queue.Count()); // 0  

```

### ⏱ Performance

| Method         | Time Complexity                  | Notes                                    |
|----------------|----------------------------------|------------------------------------------|
| `Enqueue`      | **O(1)**                         | Adds item to queue and MemoryCache       |
| `TryDequeue`   | **Best:** O(1)                   | First item is valid                      |
|                | **Worst:** O(n)                  | Scans over N expired items               |
| `Count()`      | **O(1)**                         | Atomic read via `Interlocked.Read`       |
| `Flush()`      | **O(n)**                         | Clears both queue and cache              |
| `Dispose()`    | **O(n)**                         | Calls `Flush()` + disposes all resources |

> ❗ Expired items are removed from the internal `ConcurrentDictionary` cache via background cleanup, but their **internal queue IDs remain until a dequeue attempt**.

ChronoQueue aims to _work correctly_ and meet reasonable expectations by default while being fast. Benchmarking results are available [here](https://github.com/khavishbhundoo/ChronoQueue/tree/main/benchmarks), check them out!

## 📄 License
```
Copyright (c) 2024 Khavish Anshudass Bhundoo

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```