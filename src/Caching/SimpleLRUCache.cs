using System;
using System.Collections.Generic;

namespace ArcanumLib.Caching;

/// <summary>
/// Simple LRU cache with a size limit to prevent unbounded memory growth.
/// Thread-safe implementation using lock synchronization.
/// </summary>
public class SimpleLRUCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> cache;
    private readonly LinkedList<TKey> lruList;
    private readonly int maxSize;
    private readonly object syncLock = new object();

    /// <summary>
    /// Creates a cache with the given maximum number of entries.
    /// </summary>
    /// <param name="maxSize">Maximum number of entries. Must be positive.</param>
    /// <param name="comparer">Optional equality comparer for keys.</param>
    public SimpleLRUCache(int maxSize, IEqualityComparer<TKey>? comparer = null)
    {
        if (maxSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be greater than 0.");

        this.maxSize = maxSize;
        this.cache = new Dictionary<TKey, TValue>(maxSize, comparer);
        this.lruList = new LinkedList<TKey>();
    }

    /// <summary>
    /// Tries to get a value and marks it as most recently used.
    /// </summary>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        lock (syncLock)
        {
            if (cache.TryGetValue(key, out var local))
            {
                value = local;
                lruList.Remove(key);
                lruList.AddFirst(key);
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Adds or updates a value, evicting the least recently used entry if needed.
    /// </summary>
    public void Add(TKey key, TValue value)
    {
        lock (syncLock)
        {
            if (cache.ContainsKey(key))
            {
                cache[key] = value;
                lruList.Remove(key);
                lruList.AddFirst(key);
            }
            else
            {
                if (cache.Count >= maxSize && lruList.Last != null)
                {
                    TKey evictKey = lruList.Last.Value;
                    lruList.RemoveLast();
                    cache.Remove(evictKey);
                }

                cache.Add(key, value);
                lruList.AddFirst(key);
            }
        }
    }

    /// <summary>
    /// Removes the given key if it exists.
    /// </summary>
    public bool Remove(TKey key)
    {
        lock (syncLock)
        {
            if (cache.Remove(key))
            {
                lruList.Remove(key);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Clears all entries.
    /// </summary>
    public void Clear()
    {
        lock (syncLock)
        {
            cache.Clear();
            lruList.Clear();
        }
    }

    /// <summary>
    /// Current number of cached entries.
    /// </summary>
    public int Count
    {
        get
        {
            lock (syncLock) return cache.Count;
        }
    }
}
