using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ArcanumLib.Randomization;

/// <summary>
/// A single loot entry: a value with a base weight and optional quality tier.
/// Tiers are used together with a <see cref="LootTable{T}.LuckMultiplier" /> to
/// shift probability toward higher-tier entries.
/// </summary>
/// <typeparam name="T">The type of the t value.</typeparam>
public class LootEntry<T>
{
    /// <summary>
    /// The value that can be rolled.
    /// </summary>
    public T Value { get; set; } = default!;

    /// <summary>
    /// Base relative weight. Negative values are treated as zero at roll time.
    /// </summary>
    public float Weight { get; set; } = 1f;

    /// <summary>
    /// Optional quality tier. Higher tiers become more likely as
    /// <see cref="LootTable{T}.LuckMultiplier" /> increases.
    /// </summary>
    public int Tier { get; set; } = 0;

    /// <summary>Performs the loot entry operation.</summary>
    public LootEntry() { }

    /// <summary>Performs the loot entry operation.</summary>
    /// <param name="value">The value to set or compare.</param>
    /// <param name="weight">The weight value.</param>
    /// <param name="tier">The tier value.</param>
    public LootEntry(T value, float weight, int tier = 0)
    {
        Value = value;
        Weight = weight;
        Tier = tier;
    }
}

/// <summary>
/// A JSON-serializable loot table with optional quality tiers and a luck multiplier.
/// Higher <see cref="LuckMultiplier" /> values shift probability toward entries with
/// higher <see cref="LootEntry{T}.Tier" />. The table can be rolled once or multiple
/// times, with or without replacement.
/// </summary>
/// <typeparam name="T">The type of the t value.</typeparam>
/// <remarks>
/// JSON format:
/// <code>
/// {
/// "luckMultiplier": 1.0,
/// "entries": [
/// { "value": "sword", "weight": 10, "tier": 0 },
/// { "value": "gem",   "weight": 2,  "tier": 2 }
/// ]
/// }
/// </code>
/// </remarks>
public class LootTable<T>
{
    /// <summary>
    /// Multiplier applied to each entry's effective weight based on its tier.
    /// Effective weight = <c>Weight * (1 + LuckMultiplier * Tier)</c>.
    /// A value of 0 means tiers have no effect. Must not be negative.
    /// </summary>
    [JsonProperty("luckMultiplier")]
    public float LuckMultiplier { get; set; } = 0f;

    /// <summary>
    /// All loot entries in the table.
    /// </summary>
    [JsonProperty("entries")]
    public List<LootEntry<T>> Entries { get; set; } = new();

    /// <summary>
    /// Number of entries.
    /// </summary>
    [JsonIgnore]
    public int Count => Entries.Count;

    /// <summary>
    /// Creates an empty loot table.
    /// </summary>
    public LootTable() { }

    /// <summary>
    /// Creates a loot table with the given entries and luck multiplier.
    /// </summary>
    /// <param name="entries">The collection of entries values.</param>
    /// <param name="luckMultiplier">The luck multiplier value.</param>
    public LootTable(IEnumerable<LootEntry<T>> entries, float luckMultiplier = 0f)
    {
        Entries = entries?.ToList() ?? new List<LootEntry<T>>();
        LuckMultiplier = Math.Max(0f, luckMultiplier);
    }

    /// <summary>
    /// Adds an entry to the table.
    /// </summary>
    /// <param name="value">The value to set or compare.</param>
    /// <param name="weight">The weight value.</param>
    /// <param name="tier">The tier value.</param>
    public void Add(T value, float weight, int tier = 0)
    {
        Entries.Add(new LootEntry<T>(value, weight, tier));
    }

    /// <summary>
    /// Removes all entries.
    /// </summary>
    public void Clear()
    {
        Entries.Clear();
    }

    /// <summary>
    /// Computes the effective weight of an entry given the current luck multiplier.
    /// </summary>
    /// <param name="entry">The entry value.</param>
    /// <returns>The effective weight.</returns>
    public float EffectiveWeight(LootEntry<T> entry)
    {
        if (entry == null) return 0f;
        float baseWeight = Math.Max(0f, entry.Weight);
        float tierBonus = 1f + LuckMultiplier * entry.Tier;
        return baseWeight * Math.Max(0f, tierBonus);
    }

    /// <summary>
    /// Total effective weight across all entries.
    /// </summary>
    /// <returns>The total effective weight.</returns>
    public float TotalEffectiveWeight()
    {
        float total = 0f;
        foreach (var entry in Entries)
            total += EffectiveWeight(entry);
        return total;
    }

    /// <summary>
    /// Rolls a single value from the table using the provided random source.
    /// Returns <c>default(T)</c> if the table is empty or all weights are zero.
    /// </summary>
    /// <param name="random">The random number generator.</param>
    /// <returns>The roll, or null if none is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random" /> is <see langword="null" />.</exception>
    public T? Roll(Random random)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (Entries.Count == 0) return default;

        if (TotalEffectiveWeight() <= 0f) return default;

        return WeightedRandom.Pick(Entries, e => e.Value, e => EffectiveWeight(e), random);
    }

    /// <summary>
    /// Rolls <paramref name="count" /> values with replacement (the same entry can
    /// appear multiple times). Returns an empty list if count is zero or negative.
    /// </summary>
    /// <param name="random">The random number generator.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>A collection of roll many values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random" /> is <see langword="null" />.</exception>
    public IReadOnlyList<T> RollMany(Random random, int count)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (count <= 0) return Array.Empty<T>();

        var result = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            var value = Roll(random);
            if (value != null || !typeof(T).IsValueType)
                result.Add(value!);
        }
        return result;
    }

    /// <summary>
    /// Rolls <paramref name="count" /> distinct values without replacement.
    /// If the table has fewer entries than <paramref name="count" />, all are returned.
    /// </summary>
    /// <param name="random">The random number generator.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>A collection of roll distinct values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random" /> is <see langword="null" />.</exception>
    public IReadOnlyList<T> RollDistinct(Random random, int count)
    {
        if (random == null) throw new ArgumentNullException(nameof(random));
        if (count <= 0) return Array.Empty<T>();

        return WeightedRandom.PickDistinct(Entries, e => EffectiveWeight(e), random, count)
            .Select(e => e.Value)
            .ToList();
    }

    /// <summary>
    /// Deserializes a loot table from a JSON string.
    /// </summary>
    /// <param name="json">The json value.</param>
    /// <returns>The from json.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json" /> is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the operation is invalid for the current state.</exception>
    public static LootTable<T> FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON cannot be empty.", nameof(json));
        return JsonConvert.DeserializeObject<LootTable<T>>(json)
            ?? throw new InvalidOperationException("Deserialized loot table was null.");
    }

    /// <summary>
    /// Serializes this loot table to a JSON string.
    /// </summary>
    /// <param name="indented">The indented value.</param>
    /// <returns>The to json string, or null if none is found.</returns>
    public string ToJson(bool indented = false)
        => JsonConvert.SerializeObject(this, indented ? Formatting.Indented : Formatting.None);
}
