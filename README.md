[![NuGet](https://img.shields.io/nuget/v/khavishbhundoo.ChronoQueue.svg)](https://www.nuget.org/packages/khavishbhundoo.ChronoQueue/)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=coverage)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=ChronoQueue&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=ChronoQueue)

# ChronoQueue
ChronoQueue<T> is a high-performance, thread-safe, time-aware FIFO queue with automatic item expiration. It is designed for scenarios where you need time-based eviction of in-memory data, such as TTL-based task buffering, lightweight scheduling, or caching with strict ordering.
Internally, it combines a ConcurrentQueue for preserving FIFO ordering and a dedicated MemoryCache instance for managing expiration and cleanup of stale items.

## 🚀 Features

- ✅ FIFO ordering with O(1) enqueue and dequeue with dedicated memory cache
- 🕒 Per-item TTL using `DateTimeOffset`
- 🧹 Background adaptive cleanup using `MemoryCache.Compact()` to handle memory pressure at scale and offer near realtime eviction of expired items 
- ⚡ Fast in-memory access (no locks or semaphores)
- 🛡 Thread-safe, designed for high-concurrency use cases
- 🧯 Disposal-aware and safe to use in long-lived applications

## 📘 API Overview

### `ChronoQueue<T>`

| Method | Description |
|--------|-------------|
| `Enqueue(ChronoQueueItem<T>)` | Adds an item with an expiration timestamp. Throws if item is already expired or queue is disposed. |
| `TryDequeue(out T item)` | Attempts to dequeue the next **non-expired** item. Returns `true` if successful. |
| `Count()` | Returns the current number of active (non-expired) items in the queue. |
| `Flush()` | Clears the queue and cache, resetting all internal state. |
| `Dispose()` | Disposes all resources, cancels cleanup, and resets the queue. |


**Note**: If `T` is a reference type that implements `IDisposable` is it never disposed when dequeued for safety reasons.
In case `T` expired before a dequeue operation, the item is disposed provided `DisposeOnExpiry` is set to true when creating
`ChronoQueueItem`. 

Examples of usage for methods above can be found in this README and in unit tests

---

## 🛑 Exceptions

- **`ChronoQueueItemExpiredException`**  
  Thrown when you attempt to enqueue an item that has already expired. This ensures expired data does not enter the queue.

- **`ObjectDisposedException`**  
  Thrown if any method is invoked on the queue after it has been disposed. This protects against operations on invalid or released resources.


### 🧠 Memory Pressure Heuristics

The internal cleanup logic uses `GC.GetTotalMemory(false)` to adaptively determine how aggressively to compact the underlying `MemoryCache`. The compaction fraction is scaled based on memory usage and queue size as follows:

| Memory Used         | Queue Size         | Compaction Fraction |
|---------------------|--------------------|---------------------|
| `< 64MB`            | `< 10,000` items   | `0.05`              |
| `< 64MB`            | `≥ 10,000` items   | `0.10`              |
| `64MB – 128MB`      | `< 10,000` items   | `0.10`              |
| `64MB – 128MB`      | `≥ 10,000` items   | `0.15`              |
| `128MB – 256MB`     | any                | `0.20`              |
| `≥ 256MB`           | any                | `0.25`              |

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

| Method         | Time Complexity                  | Notes                                                                 |
|----------------|----------------------------------|-----------------------------------------------------------------------|
| `Enqueue`      | **O(1)**                         | Adds item to queue and MemoryCache                                    |
| `TryDequeue`   | **Best:** O(1)                   | First item is valid                                                   |
|                | **Worst:** O(n)                  | Scans over expired items                                              |
| `Count()`      | **O(1)**                         | Atomic read via `Interlocked.Read`                                    |
| `Flush()`      | **O(n)**                         | Clears both queue and cache                                           |
| `Dispose()`    | **O(n)**                         | Calls `Flush()` + disposes all resources                              |

> ❗ Expired items are removed from the internal `MemoryCache` via background compaction, but their **queue IDs remain until a dequeue attempt**.

## 📊 Benchmark

```

BenchmarkDotNet v0.15.0, Windows 10 (10.0.19045.5917/22H2/2022Update)
Intel Core i7-4790K CPU 4.00GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK 9.0.101
  [Host]   : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2
  .NET 9.0 : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2

InvocationCount=1  UnrollFactor=1  

ChronoQueueBenchmarks.ChronoQueueBenchmark

```
| Method        | Job      | Runtime  | Categories | ItemCount | Mean [ns]       | Error [ns]   | StdDev [ns]   | Median [ns]     | Gen0        | Gen1       | Gen2      | Allocated [B] |
|-------------- |--------- |--------- |----------- |---------- |----------------:|-------------:|--------------:|----------------:|------------:|-----------:|----------:|--------------:|
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **100**       |        **69,000.0** |      **6,011.3** |      **17,535.1** |        **65,450.0** |      **0.0000** |     **0.0000** |    **0.0000** |         **14288** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 100       |        70,767.0 |      6,486.9 |      19,126.9 |        73,900.0 |      0.0000 |     0.0000 |    0.0000 |         14288 |
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **1000**      |       **729,716.0** |     **14,589.4** |      **30,453.6** |       **729,250.0** |      **0.0000** |     **0.0000** |    **0.0000** |        **136688** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 1000      |       556,992.9 |     30,629.2 |      89,830.1 |       521,200.0 |      0.0000 |     0.0000 |    0.0000 |        136688 |
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **10000**     |     **5,127,371.0** |    **370,849.3** |   **1,093,457.7** |     **4,452,000.0** |      **0.0000** |     **0.0000** |    **0.0000** |       **1360688** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 10000     |     4,777,725.3 |    418,021.0 |   1,225,983.4 |     4,200,000.0 |      0.0000 |     0.0000 |    0.0000 |       1360352 |
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **100000**    |    **51,208,261.5** |  **1,004,857.3** |     **839,101.4** |    **50,891,500.0** |   **2000.0000** |  **1000.0000** |    **0.0000** |      **13601000** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 100000    |    47,157,956.8 |    941,478.1 |   1,598,701.4 |    47,209,700.0 |   2000.0000 |  1000.0000 |    0.0000 |      13601000 |
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **1000000**   |   **499,470,156.2** |  **9,870,719.4** |   **9,694,366.3** |   **497,318,200.0** |  **32000.0000** |  **1000.0000** |    **0.0000** |     **136002280** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 1000000   |   461,017,457.1 |  5,232,564.0 |   4,638,531.7 |   460,656,200.0 |  32000.0000 |  1000.0000 |    0.0000 |     136002608 |
|               |          |          |            |           |                 |              |               |                 |             |            |           |               |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **100**       |       **126,831.4** |      **9,708.5** |      **28,166.1** |       **119,450.0** |      **0.0000** |     **0.0000** |    **0.0000** |         **73296** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 100       |       121,977.8 |      7,982.2 |      23,157.7 |       115,450.0 |      0.0000 |     0.0000 |    0.0000 |         73008 |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **1000**      |       **980,302.1** |     **51,912.3** |     **150,607.1** |       **976,600.0** |      **0.0000** |     **0.0000** |    **0.0000** |        **747944** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 1000      |       995,672.0 |     28,213.1 |      80,035.8 |       965,600.0 |      0.0000 |     0.0000 |    0.0000 |        747944 |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **10000**     |    **10,450,369.0** |    **891,080.1** |   **2,627,370.1** |    **11,198,850.0** |   **1000.0000** |     **0.0000** |    **0.0000** |       **7273992** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 10000     |     7,810,750.0 |    388,255.8 |   1,056,278.6 |     7,533,700.0 |   1000.0000 |     0.0000 |    0.0000 |       7273992 |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **100000**    |   **132,656,567.0** |  **2,712,315.1** |   **7,868,918.4** |   **129,133,800.0** |  **12000.0000** |  **6000.0000** | **1000.0000** |      **71383688** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 100000    |   105,559,639.4 |  2,086,203.9 |   3,308,940.7 |   105,658,100.0 |  12000.0000 |  6000.0000 | 1000.0000 |      71350984 |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **1000000**   | **1,531,081,925.0** | **51,691,464.2** | **152,413,459.5** | **1,516,394,350.0** | **108000.0000** | **53000.0000** | **1000.0000** |     **711196960** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 1000000   | 1,417,219,478.0 | 56,209,271.7 | 165,734,318.0 | 1,365,568,450.0 | 106000.0000 | 53000.0000 | 1000.0000 |     702562872 |
|               |          |          |            |           |                 |              |               |                 |             |            |           |               |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **100**       |        **32,368.5** |      **1,867.7** |       **5,267.8** |        **29,900.0** |      **0.0000** |     **0.0000** |    **0.0000** |         **11920** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 100       |        27,446.4 |      1,736.3 |       5,037.4 |        25,300.0 |      0.0000 |     0.0000 |    0.0000 |         11920 |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **1000**      |       **396,775.5** |     **12,087.8** |      **35,260.8** |       **395,200.0** |      **0.0000** |     **0.0000** |    **0.0000** |         **91120** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 1000      |       379,307.1 |     21,911.3 |      64,261.9 |       355,900.0 |      0.0000 |     0.0000 |    0.0000 |         91120 |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **10000**     |     **3,712,721.4** |     **68,479.4** |      **60,705.2** |     **3,735,200.0** |      **0.0000** |     **0.0000** |    **0.0000** |        **883120** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 10000     |     2,341,744.0 |    310,152.8 |     914,492.5 |     2,363,450.0 |      0.0000 |     0.0000 |    0.0000 |        883120 |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **100000**    |    **26,219,673.7** |    **599,131.5** |   **1,719,020.7** |    **26,221,500.0** |   **1000.0000** |     **0.0000** |    **0.0000** |       **8803432** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 100000    |    19,681,937.5 |    264,159.8 |     259,440.2 |    19,701,400.0 |   1000.0000 |     0.0000 |    0.0000 |       8803432 |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **1000000**   |   **290,813,977.8** |  **5,797,853.7** |   **9,686,911.8** |   **290,901,950.0** |  **21000.0000** |  **1000.0000** |    **0.0000** |      **88004056** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 1000000   |   199,517,847.6 |  3,691,417.4 |   4,394,370.2 |   200,138,600.0 |  20000.0000 |  1000.0000 |    0.0000 |      88003120 |


```

BenchmarkDotNet v0.15.0, Windows 10 (10.0.19045.5917/22H2/2022Update)
Intel Core i7-4790K CPU 4.00GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK 9.0.101
  [Host]   : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2
  .NET 9.0 : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2

InvocationCount=1  UnrollFactor=1  

ChronoQueueBenchmarks.ChronoQueueValueTypesBenchmarks
```
| Method        | Job      | Runtime  | Categories | ItemCount | Mean [ns]       | Error [ns]   | StdDev [ns]   | Median [ns]     | Gen0        | Gen1       | Gen2      | Allocated [B] |
|-------------- |--------- |--------- |----------- |---------- |----------------:|-------------:|--------------:|----------------:|------------:|-----------:|----------:|--------------:|
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **100**       |        **75,414.0** |      **5,434.8** |      **16,024.6** |        **76,250.0** |      **0.0000** |     **0.0000** |    **0.0000** |         **16688** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 100       |        55,640.4 |      5,137.4 |      15,067.1 |        49,800.0 |      0.0000 |     0.0000 |    0.0000 |         16688 |
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **1000**      |       **651,265.3** |     **22,065.0** |      **64,364.8** |       **666,700.0** |      **0.0000** |     **0.0000** |    **0.0000** |        **160688** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 1000      |       554,653.5 |     29,499.4 |      86,516.5 |       510,200.0 |      0.0000 |     0.0000 |    0.0000 |        160352 |
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **10000**     |     **5,115,423.0** |    **400,345.7** |   **1,180,428.5** |     **4,393,050.0** |      **0.0000** |     **0.0000** |    **0.0000** |       **1600688** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 10000     |     4,491,154.4 |    385,922.1 |   1,082,167.0 |     4,062,350.0 |      0.0000 |     0.0000 |    0.0000 |       1600688 |
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **100000**    |    **48,940,520.8** |    **830,083.7** |   **1,079,343.4** |    **48,796,700.0** |   **3000.0000** |  **1000.0000** |    **0.0000** |      **16000664** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 100000    |    45,928,438.2 |    914,575.3 |   1,476,868.7 |    45,868,850.0 |   3000.0000 |     0.0000 |    0.0000 |      16001000 |
| **Dequeue_Items** | **.NET 8.0** | **.NET 8.0** | **Dequeue**    | **1000000**   |   **486,758,290.0** |  **6,422,263.7** |   **6,007,389.5** |   **485,409,150.0** |  **38000.0000** |  **1000.0000** |    **0.0000** |     **160001912** |
| Dequeue_Items | .NET 9.0 | .NET 9.0 | Dequeue    | 1000000   |   467,282,952.6 |  8,917,531.6 |   9,911,812.7 |   464,089,400.0 |  38000.0000 |  1000.0000 |    0.0000 |     160002248 |
|               |          |          |            |           |                 |              |               |                 |             |            |           |               |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **100**       |        **95,954.6** |      **4,682.5** |      **13,584.8** |        **90,800.0** |      **0.0000** |     **0.0000** |    **0.0000** |         **72496** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 100       |        97,846.8 |      4,052.1 |      11,560.9 |        92,650.0 |      0.0000 |     0.0000 |    0.0000 |         72496 |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **1000**      |       **835,587.8** |     **16,533.6** |      **41,479.6** |       **828,150.0** |      **0.0000** |     **0.0000** |    **0.0000** |        **738696** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 1000      |       868,175.0 |     19,019.4 |      52,385.0 |       855,500.0 |      0.0000 |     0.0000 |    0.0000 |        739944 |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **10000**     |     **8,723,929.0** |    **872,876.9** |   **2,573,697.5** |     **7,696,600.0** |   **1000.0000** |     **0.0000** |    **0.0000** |       **7193992** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 10000     |     8,876,890.0 |    768,050.3 |   2,264,613.9 |     7,832,450.0 |   1000.0000 |     0.0000 |    0.0000 |       7193992 |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **100000**    |   **129,952,028.6** |  **2,507,168.3** |   **2,222,539.4** |   **129,351,400.0** |  **11000.0000** |  **5000.0000** | **1000.0000** |      **70549136** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 100000    |   125,205,241.1 |  2,496,534.1 |   7,163,024.9 |   122,369,900.0 |  11000.0000 |  5000.0000 | 1000.0000 |      70564464 |
| **Enqueue_Items** | **.NET 8.0** | **.NET 8.0** | **Enqueue**    | **1000000**   | **1,516,495,394.7** | **75,881,769.1** | **217,719,035.8** | **1,477,579,600.0** | **107000.0000** | **52000.0000** | **1000.0000** |     **705602344** |
| Enqueue_Items | .NET 9.0 | .NET 9.0 | Enqueue    | 1000000   | 1,483,727,556.0 | 54,525,111.6 | 160,768,533.5 | 1,545,304,100.0 | 107000.0000 | 53000.0000 | 1000.0000 |     703438496 |
|               |          |          |            |           |                 |              |               |                 |             |            |           |               |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **100**       |        **39,390.6** |      **2,457.0** |       **7,089.1** |        **38,650.0** |      **0.0000** |     **0.0000** |    **0.0000** |         **13984** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 100       |        23,756.2 |        223.0 |         219.0 |        23,700.0 |      0.0000 |     0.0000 |    0.0000 |         14320 |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **1000**      |       **404,029.5** |     **11,885.3** |      **34,101.1** |       **405,600.0** |      **0.0000** |     **0.0000** |    **0.0000** |        **115120** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 1000      |       339,110.4 |      9,182.3 |      26,493.1 |       341,150.0 |      0.0000 |     0.0000 |    0.0000 |        115120 |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **10000**     |     **2,560,795.0** |    **315,040.0** |     **928,902.6** |     **2,127,750.0** |      **0.0000** |     **0.0000** |    **0.0000** |       **1122832** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 10000     |     2,161,327.0 |    310,864.7 |     916,591.7 |     1,545,600.0 |      0.0000 |     0.0000 |    0.0000 |       1123120 |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **100000**    |    **26,141,478.9** |    **559,770.1** |   **1,623,994.6** |    **25,921,850.0** |   **2000.0000** |  **1000.0000** |    **0.0000** |      **11203432** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 100000    |    21,036,772.1 |    411,279.0 |     762,332.0 |    20,863,600.0 |   2000.0000 |  1000.0000 |    0.0000 |      11203120 |
| **Flush_Items**   | **.NET 8.0** | **.NET 8.0** | **Flush**      | **1000000**   |   **282,474,312.2** |  **5,634,415.6** |  **15,706,499.9** |   **281,542,450.0** |  **26000.0000** |     **0.0000** |    **0.0000** |     **112004056** |
| Flush_Items   | .NET 9.0 | .NET 9.0 | Flush      | 1000000   |   206,478,045.8 |  3,972,013.6 |   5,164,740.1 |   206,815,200.0 |  26000.0000 |  1000.0000 |    0.0000 |     112003408 |



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