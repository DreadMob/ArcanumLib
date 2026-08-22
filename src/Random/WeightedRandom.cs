using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcanumLib.Randomization
{
    /// <summary>
    /// A single weighted entry.
    /// </summary>
    public readonly struct WeightedEntry<T>
    {
        /// <summary>
        /// The value that can be chosen.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Relative chance. Negative values are treated as zero.
        /// </summary>
        public float Weight { get; }

        public WeightedEntry(T value, float weight)
        {
            Value = value;
            Weight = weight;
        }
    }

    /// <summary>
    /// Reusable weighted table. Allows multiple entries to be added and picked
    /// repeatedly without recomputing the total weight each time, unless entries change.
    /// </summary>
    public class WeightedTable<T>
    {
        private readonly List<WeightedEntry<T>> _entries = new();
        private float _totalWeight;
        private bool _stale;

        /// <summary>
        /// Total of all (non-negative) weights. Recalculated when stale.
        /// </summary>
        public float TotalWeight
        {
            get
            {
                if (_stale)
                {
                    _totalWeight = 0f;
                    foreach (var entry in _entries)
                        _totalWeight += Math.Max(0f, entry.Weight);
                    _stale = false;
                }
                return _totalWeight;
            }
        }

        /// <summary>
        /// Number of entries in the table.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// All entries.
        /// </summary>
        public IReadOnlyList<WeightedEntry<T>> Entries => _entries;

        /// <summary>
        /// Add a weighted entry.
        /// </summary>
        public void Add(T value, float weight)
        {
            _entries.Add(new WeightedEntry<T>(value, weight));
            _stale = true;
        }

        /// <summary>
        /// Add a range of weighted entries.
        /// </summary>
        public void AddRange(IEnumerable<WeightedEntry<T>> entries)
        {
            if (entries == null) return;
            _entries.AddRange(entries);
            _stale = true;
        }

        /// <summary>
        /// Add a range of values with a weight selector.
        /// </summary>
        public void AddRange(IEnumerable<T> values, System.Func<T, float> weightSelector)
        {
            if (values == null || weightSelector == null) return;
            foreach (var value in values)
                _entries.Add(new WeightedEntry<T>(value, weightSelector(value)));
            _stale = true;
        }

        /// <summary>
        /// Remove all entries.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _totalWeight = 0f;
            _stale = false;
        }

        /// <summary>
        /// Pick a value using the provided random source.
        /// </summary>
        public T Pick(Random random)
        {
            return WeightedRandom.Pick(_entries, e => e.Value, e => e.Weight, random);
        }

        /// <summary>
        /// Pick a value or return default when the table is empty or total weight is zero.
        /// </summary>
        public T? PickOrDefault(Random random)
        {
            if (Count == 0) return default;
            var total = TotalWeight;
            if (total <= 0f) return default;
            return Pick(random);
        }
    }

    /// <summary>
    /// Weighted random selection helpers.
    /// </summary>
    public static class WeightedRandom
    {
        /// <summary>
        /// Pick a value from a weighted list. Returns the first item if all weights are zero.
        /// </summary>
        public static T Pick<T>(IEnumerable<T> items, System.Func<T, float> weightSelector, Random random)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (weightSelector == null) throw new ArgumentNullException(nameof(weightSelector));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var list = items as IReadOnlyList<T> ?? items.ToList();
            if (list.Count == 0) return default!;
            if (list.Count == 1) return list[0];

            float total = 0f;
            for (int i = 0; i < list.Count; i++)
                total += Math.Max(0f, weightSelector(list[i]));

            if (total <= 0f) return list[0];

            float roll = (float)(random.NextDouble() * total);
            float cumulative = 0f;

            for (int i = 0; i < list.Count; i++)
            {
                float w = Math.Max(0f, weightSelector(list[i]));
                cumulative += w;
                if (roll < cumulative) return list[i];
            }

            return list[list.Count - 1];
        }

        /// <summary>
        /// Pick a value from a list of entries.
        /// </summary>
        public static T Pick<TEntry, T>(IEnumerable<TEntry> entries, System.Func<TEntry, T> valueSelector, System.Func<TEntry, float> weightSelector, Random random)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            if (weightSelector == null) throw new ArgumentNullException(nameof(weightSelector));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var list = entries as IReadOnlyList<TEntry> ?? entries.ToList();
            if (list.Count == 0) return default!;
            if (list.Count == 1) return valueSelector(list[0]);

            float total = 0f;
            for (int i = 0; i < list.Count; i++)
                total += Math.Max(0f, weightSelector(list[i]));

            if (total <= 0f) return valueSelector(list[0]);

            float roll = (float)(random.NextDouble() * total);
            float cumulative = 0f;

            for (int i = 0; i < list.Count; i++)
            {
                float w = Math.Max(0f, weightSelector(list[i]));
                cumulative += w;
                if (roll < cumulative) return valueSelector(list[i]);
            }

            return valueSelector(list[list.Count - 1]);
        }

        /// <summary>
        /// Pick from a list of weighted entries.
        /// </summary>
        public static WeightedEntry<T> Pick<T>(IEnumerable<WeightedEntry<T>> items, Random random)
        {
            return Pick(items, v => v.Weight, random);
        }

        /// <summary>
        /// Pick or return default when the list is empty or all weights are zero.
        /// </summary>
        public static T? PickOrDefault<T>(IEnumerable<T> items, System.Func<T, float> weightSelector, Random random)
        {
            if (items == null || random == null) return default;
            var list = items as IReadOnlyList<T> ?? items.ToList();
            if (list.Count == 0) return default;

            float total = 0f;
            for (int i = 0; i < list.Count; i++)
                total += Math.Max(0f, weightSelector(list[i]));

            if (total <= 0f) return default;

            return Pick(list, weightSelector, random);
        }

        /// <summary>
        /// Roll a weighted table and pick a specific number of distinct winners without replacement.
        /// If there are fewer unique entries than requested count, all are returned.
        /// </summary>
        public static IReadOnlyList<T> PickDistinct<T>(IEnumerable<T> items, System.Func<T, float> weightSelector, Random random, int count)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (weightSelector == null) throw new ArgumentNullException(nameof(weightSelector));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count <= 0) return Array.Empty<T>();

            var pool = new List<WeightedEntry<T>>();
            foreach (var item in items)
                pool.Add(new WeightedEntry<T>(item, weightSelector(item)));

            var result = new List<T>(Math.Min(count, pool.Count));
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                var winner = Pick(pool, random);
                result.Add(winner.Value);
                pool.Remove(winner);
            }

            return result;
        }

        /// <summary>
        /// Computes each item's percentage share of the total weight (negative weights treated as zero).
        /// Returns a list aligned with the input enumeration order.
        /// </summary>
        public static IReadOnlyList<(T Item, float Percentage)> GetPercentages<T>(IEnumerable<T> items, System.Func<T, float> weightSelector)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (weightSelector == null) throw new ArgumentNullException(nameof(weightSelector));

            var list = items as IReadOnlyList<T> ?? items.ToList();
            var result = new List<(T Item, float Percentage)>(list.Count);

            float total = 0f;
            for (int i = 0; i < list.Count; i++)
                total += Math.Max(0f, weightSelector(list[i]));

            if (total <= 0f)
            {
                for (int i = 0; i < list.Count; i++)
                    result.Add((list[i], 0f));
                return result;
            }

            for (int i = 0; i < list.Count; i++)
            {
                float w = Math.Max(0f, weightSelector(list[i]));
                result.Add((list[i], w * 100f / total));
            }

            return result;
        }
    }


}
