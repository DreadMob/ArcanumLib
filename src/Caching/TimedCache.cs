using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Common;

namespace ArcanumLib.Caching;

/// <summary>
/// A thread-safe cache that evicts entries after they have not been accessed for
/// a configurable time. Optionally bounded by a maximum size; when both limits apply,
/// the oldest unused entries are removed first.
/// </summary>
public sealed class TimedCache<TKey, TValue> : IDisposable
    where TKey : notnull
{
    private readonly Dictionary<TKey, CacheEntry> _mapping = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly int _ttlMs;
    private readonly int? _maxSize;
    private readonly ICoreAPI? _api;
    private long _cleanupTimer;
    private bool _disposed;

    private sealed class CacheEntry
    {
        public TValue Value = default!;
        public long LastAccess;
    }

    /// <summary>
    /// Creates a cache with time-based eviction.
    /// </summary>
    /// <param name="api">Optional API for registering a periodic cleanup tick.</param>
    /// <param name="ttlMs">Time after which an unused entry is considered stale, in milliseconds.</param>
    /// <param name="cleanupIntervalMs">How often the cache should scan for stale entries, in milliseconds.</param>
    /// <param name="maxSize">Optional hard size limit. Oldest entries are evicted first when exceeded.</param>
    public TimedCache(ICoreAPI? api, int ttlMs, int cleanupIntervalMs = 60000, int? maxSize = null)
    {
        _api = api;
        _ttlMs = ttlMs;
        _maxSize = maxSize;

        if (api?.World != null)
        {
            _cleanupTimer = api.World.RegisterGameTickListener(_ => Clean(), cleanupIntervalMs, cleanupIntervalMs);
        }
    }

    /// <summary>
    /// Adds or updates the value for the given key.
    /// </summary>
    public void Add(TKey key, TValue value)
    {
        _lock.EnterWriteLock();
        try
        {
            _mapping[key] = new CacheEntry
            {
                Value = value,
                LastAccess = _api?.World?.ElapsedMilliseconds ?? Environment.TickCount64
            };

            if (_maxSize.HasValue)
            {
                TrimToSize(_maxSize.Value);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Tries to get the value and refreshes its last-access time on success.
    /// </summary>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_mapping.TryGetValue(key, out var entry))
            {
                long now = _api?.World?.ElapsedMilliseconds ?? Environment.TickCount64;
                if (now - entry.LastAccess <= _ttlMs)
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        entry.LastAccess = now;
                        value = entry.Value;
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                    return true;
                }

                _lock.EnterWriteLock();
                try
                {
                    _mapping.Remove(key);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            value = default;
            return false;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Removes the key from the cache.
    /// </summary>
    public bool Remove(TKey key)
    {
        _lock.EnterWriteLock();
        try
        {
            return _mapping.Remove(key);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all entries.
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _mapping.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Manually removes stale entries. Called automatically if an API was supplied.
    /// </summary>
    public void Clean()
    {
        _lock.EnterWriteLock();
        try
        {
            long now = _api?.World?.ElapsedMilliseconds ?? Environment.TickCount64;
            var stale = new List<TKey>();
            foreach (var kvp in _mapping)
            {
                if (now - kvp.Value.LastAccess > _ttlMs)
                {
                    stale.Add(kvp.Key);
                }
            }

            foreach (var key in stale)
            {
                _mapping.Remove(key);
            }

            if (_maxSize.HasValue)
            {
                TrimToSize(_maxSize.Value);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Current number of cached entries.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _mapping.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    private void TrimToSize(int maxSize)
    {
        while (_mapping.Count > maxSize)
        {
            TKey? oldest = default;
            long oldestAccess = long.MaxValue;
            foreach (var kvp in _mapping)
            {
                if (kvp.Value.LastAccess < oldestAccess)
                {
                    oldest = kvp.Key;
                    oldestAccess = kvp.Value.LastAccess;
                }
            }

            if (oldest != null)
            {
                _mapping.Remove(oldest);
            }
            else
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lock.EnterWriteLock();
        try
        {
            _mapping.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        if (_api?.World != null && _cleanupTimer != 0)
        {
            _api.World.UnregisterGameTickListener(_cleanupTimer);
            _cleanupTimer = 0;
        }

        _lock.Dispose();
    }
}
