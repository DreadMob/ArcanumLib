# TimedCache<TKey, TValue>

A thread-safe cache that evicts entries after a configurable time-to-live (TTL).
Can also be bounded by a maximum size; when both apply, the oldest unused
entries are removed first.

## Usage

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

## API

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

The cache uses a `ReaderWriterLockSlim` for safe concurrent access and, when an
`ICoreAPI` is supplied, a periodic game-tick listener to clean stale entries.
