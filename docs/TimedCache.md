---
layout: default
title: TimedCache<TKey, TValue>
parent: "DeferredWork"
nav_order: 3
---

# TimedCache<TKey, TValue>

## What is it for?

`ArcanumLib.Caching.TimedCache<TKey, TValue>` is a thread-safe cache that evicts entries after a configurable time-to-live (TTL). It can also be bounded by a maximum size; when both apply, the oldest unused entries are removed first.

## When to use it

- Caching expensive-to-create data that should expire automatically.
- Limiting memory usage with a maximum size.
- A shared cache accessed from multiple threads.
- Active entries should stay alive because each access refreshes their timestamp.

## Quick example

```csharp
using ArcanumLib.Caching;

var cache = new TimedCache<string, Texture2D>(
    api: capi,
    ttlMs: 60_000,          // 1 minute
    cleanupIntervalMs: 30_000,
    maxSize: 100
);

cache.Add("sword", texture);

if (cache.TryGetValue("sword", out var texture))
{
    // texture is still fresh; its access time is refreshed
}

cache.Dispose();
```

## Usage

```csharp
public sealed class TimedCache<TKey, TValue> : IDisposable
    where TKey : notnull
{
    public TimedCache(ICoreAPI? api, int ttlMs, int cleanupIntervalMs = 60000, int? maxSize = null);

    public void Add(TKey key, TValue value);
    public bool TryGetValue(TKey key, out TValue? value);
    public bool Remove(TKey key);
    public void Clear();
    public void Clean();        // manual stale entry cleanup
    public int Count { get; }
    public void Dispose();
}
```

| Method | Description |
| --- | --- |
| `Add` | Stores a value under the given key. |
| `TryGetValue` | Returns the value if it exists and is still fresh; refreshes the access time. |
| `Remove` | Removes a single entry. |
| `Clear` | Removes all entries. |
| `Clean` | Manually removes stale entries. |
| `Count` | Current number of entries. |
| `Dispose` | Stops the tick listener and clears the cache. |

## Notes

- Accessing an entry refreshes its access time, so active entries are not evicted.
- The cache uses a `ReaderWriterLockSlim` for safe concurrent access.
- When an `ICoreAPI` is supplied, a periodic game-tick listener cleans stale entries in the background.
- Call `Dispose()` when the cache is no longer needed to stop the tick listener and release references.