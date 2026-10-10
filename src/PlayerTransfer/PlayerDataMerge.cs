using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;

namespace ArcanumLib.PlayerTransfer
{
    /// <summary>How two numbers stored under the same key combine when two accounts are merged.</summary>
    public enum NumberMerge
    {
        /// <summary>Counters: kills, points, currency, opens.</summary>
        Sum,
        /// <summary>Times, levels, tiers, records, streaks: keep the larger.</summary>
        Max,
        /// <summary>"First seen" style timestamps: keep the earlier non-zero.</summary>
        MinNonZero
    }

    /// <summary>
    /// Merge rules shared by every transfer participant, so "combine two accounts" means the same
    /// thing everywhere: numbers add up unless the key names a time/level/record (then the larger
    /// wins, or the earlier for "first" timestamps), flags OR, strings keep the target's value,
    /// lists union, nested objects merge recursively.
    /// </summary>
    public static class PlayerDataMerge
    {
        private static readonly Regex FirstLike = new(
            @"(?i:^(first|created|joined|since))|(First|Created|Joined)(At|Ms|Time|Hours|Day)?$",
            RegexOptions.Compiled);

        private static readonly Regex MaxLike = new(
            @"(^|[a-z])(best|max|highest|peak|record|level|lvl|tier|streak|rank|stage|version|index|happiness|mood|health|satiety|stability|friendship)|^(Best|Max|Highest|Peak|Record|Level|Tier|Streak|Rank|Stage|Version|Index|Happiness|Mood|Health|Friendship)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SumLike = new(
            @"count|total|kills?|deaths?|points?|xp|played|playtime|amount|opens|wins|losses|score|damage|purchases|completions?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Case-sensitive on purpose: "unlockedAt"/"updated_at" are times, "combat"/"items" are not.
        private static readonly Regex TimeLike = new(
            @"(At|_at|Ms|_ms|Time|_time|Hours|_hours|Day|Days|_day|Date|_date|Stamp|Until|Expires|Cooldown|Sec|Seconds)$|^(last|next|time|until|expires|cooldown)",
            RegexOptions.Compiled);

        /// <summary>Picks the rule for a key by its name (the leaf name, not the full path).</summary>
        public static NumberMerge Classify(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return NumberMerge.Sum;
            string leaf = key!;
            int cut = Math.Max(leaf.LastIndexOf(':'), leaf.LastIndexOf('.'));
            if (cut >= 0 && cut < leaf.Length - 1) leaf = leaf.Substring(cut + 1);

            if (FirstLike.IsMatch(leaf)) return NumberMerge.MinNonZero;
            if (MaxLike.IsMatch(leaf)) return NumberMerge.Max;
            if (SumLike.IsMatch(leaf)) return NumberMerge.Sum;
            if (TimeLike.IsMatch(leaf)) return NumberMerge.Max;
            return NumberMerge.Sum;
        }

        public static double Combine(double target, double source, NumberMerge rule) => rule switch
        {
            NumberMerge.Max => Math.Max(target, source),
            NumberMerge.MinNonZero => target == 0 ? source : source == 0 ? target : Math.Min(target, source),
            _ => target + source
        };

        public static long Combine(long target, long source, NumberMerge rule) => rule switch
        {
            NumberMerge.Max => Math.Max(target, source),
            NumberMerge.MinNonZero => target == 0 ? source : source == 0 ? target : Math.Min(target, source),
            _ => target + source
        };

        public static int Combine(int target, int source, NumberMerge rule) => (int)Combine((long)target, source, rule);

        // ------------------------------------------------------------------ tree attributes

        /// <summary>Merges <paramref name="source" /> into <paramref name="target" /> in place.</summary>
        public static void MergeTree(ITreeAttribute target, ITreeAttribute source)
        {
            if (target == null || source == null) return;
            foreach (var kv in source.ToList())
            {
                var existing = target[kv.Key];
                var merged = MergeAttribute(kv.Key, existing, kv.Value);
                if (merged != null && !ReferenceEquals(merged, existing)) target[kv.Key] = merged;
            }
        }

        /// <summary>Merges one attribute value; returns what the target should hold.</summary>
        public static IAttribute MergeAttribute(string key, IAttribute? target, IAttribute source)
        {
            if (source == null) return target!;
            if (target == null) return source.Clone();

            var rule = Classify(key);
            switch (target)
            {
                case ITreeAttribute tt when source is ITreeAttribute st:
                    MergeTree(tt, st);
                    return tt;
                case IntAttribute ti when source is IntAttribute si:
                    return new IntAttribute(Combine(ti.value, si.value, rule));
                case LongAttribute tl when source is LongAttribute sl:
                    return new LongAttribute(Combine(tl.value, sl.value, rule));
                case FloatAttribute tf when source is FloatAttribute sf:
                    return new FloatAttribute((float)Combine(tf.value, sf.value, rule));
                case DoubleAttribute td when source is DoubleAttribute sd:
                    return new DoubleAttribute(Combine(td.value, sd.value, rule));
                case BoolAttribute tb when source is BoolAttribute sb:
                    return new BoolAttribute(tb.value || sb.value);
                case StringAttribute ts when source is StringAttribute ss:
                    return string.IsNullOrEmpty(ts.value) ? new StringAttribute(ss.value) : ts;
                case StringArrayAttribute tsa when source is StringArrayAttribute ssa:
                    return new StringArrayAttribute(Union(tsa.value, ssa.value));
                case IntArrayAttribute tia when source is IntArrayAttribute sia:
                    return new IntArrayAttribute(Union(tia.value, sia.value));
                case LongArrayAttribute tla when source is LongArrayAttribute sla:
                    return new LongArrayAttribute(Union(tla.value, sla.value));
                default:
                    // Types differ or are opaque (byte arrays, item stacks): keep what the target has.
                    return target;
            }
        }

        private static T[] Union<T>(T[]? a, T[]? b)
        {
            var list = new List<T>(a ?? Array.Empty<T>());
            foreach (var v in b ?? Array.Empty<T>())
                if (!list.Contains(v)) list.Add(v);
            return list.ToArray();
        }

        // ------------------------------------------------------------------ JSON / POCO values

        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None
            // Auto (reuse): collections created by the type's initializers keep their comparers
            // (OrdinalIgnoreCase uid/id dictionaries) when the merged JSON is read back.
        });

        /// <summary>
        /// Merges two values of any serializable type field by field with the same rules
        /// (numbers by key name, bools OR, strings keep target, arrays union, objects recurse).
        /// </summary>
        public static T MergeValues<T>(T target, T source)
        {
            if (source == null) return target;
            if (target == null) return source;
            var tj = JToken.FromObject(target, Serializer);
            var sj = JToken.FromObject(source, Serializer);
            var merged = MergeJson(null, tj, sj);
            return merged.ToObject<T>(Serializer)!;
        }

        public static JToken MergeJson(string? key, JToken? target, JToken? source)
        {
            if (source == null || source.Type == JTokenType.Null) return target ?? JValue.CreateNull();
            if (target == null || target.Type == JTokenType.Null) return source.DeepClone();

            if (target is JObject to && source is JObject so)
            {
                var result = (JObject)to.DeepClone();
                foreach (var prop in so.Properties())
                    result[prop.Name] = MergeJson(prop.Name, result[prop.Name], prop.Value);
                return result;
            }

            if (target is JArray ta && source is JArray sa)
            {
                var result = (JArray)ta.DeepClone();
                foreach (var item in sa)
                    if (!result.Any(x => JToken.DeepEquals(x, item))) result.Add(item.DeepClone());
                return result;
            }

            var rule = Classify(key);
            if (target.Type == JTokenType.Integer && source.Type == JTokenType.Integer)
                return new JValue(Combine(target.Value<long>(), source.Value<long>(), rule));
            if ((target.Type == JTokenType.Float || target.Type == JTokenType.Integer) &&
                (source.Type == JTokenType.Float || source.Type == JTokenType.Integer))
                return new JValue(Combine(target.Value<double>(), source.Value<double>(), rule));
            if (target.Type == JTokenType.Boolean && source.Type == JTokenType.Boolean)
                return new JValue(target.Value<bool>() || source.Value<bool>());
            if (target.Type == JTokenType.String && string.IsNullOrEmpty(target.Value<string>()))
                return source.DeepClone();
            if (target.Type == JTokenType.Date && source.Type == JTokenType.Date)
                return rule == NumberMerge.MinNonZero
                    ? (target.Value<DateTime>() <= source.Value<DateTime>() ? target : source).DeepClone()
                    : (target.Value<DateTime>() >= source.Value<DateTime>() ? target : source).DeepClone();
            return target;
        }

        // ------------------------------------------------------------------ uid-keyed collections

        /// <summary>
        /// Moves <paramref name="fromKey" />'s entry onto <paramref name="toKey" />, merging with an
        /// existing target entry (<see cref="MergeValues{T}" /> unless a custom merge is given).
        /// Returns true when something moved.
        /// </summary>
        public static bool MoveEntry<TValue>(IDictionary<string, TValue> dict, string fromKey, string toKey, Func<TValue, TValue, TValue>? merge = null)
        {
            if (dict == null || string.IsNullOrEmpty(fromKey) || string.IsNullOrEmpty(toKey)) return false;
            if (string.Equals(fromKey, toKey, StringComparison.OrdinalIgnoreCase)) return false;

            string? actualFrom = FindKey(dict, fromKey);
            if (actualFrom == null) return false;

            var src = dict[actualFrom];
            dict.Remove(actualFrom);

            string? actualTo = FindKey(dict, toKey);
            if (actualTo == null)
            {
                dict[toKey] = src;
                return true;
            }

            dict[actualTo] = merge != null ? merge(dict[actualTo], src) : MergeValues(dict[actualTo], src);
            return true;
        }

        /// <summary>Renames <paramref name="fromKey" /> to <paramref name="toKey" /> in a set.</summary>
        public static bool MoveMember(ICollection<string> set, string fromKey, string toKey)
        {
            if (set == null) return false;
            var actual = set.FirstOrDefault(s => string.Equals(s, fromKey, StringComparison.OrdinalIgnoreCase));
            if (actual == null) return false;
            set.Remove(actual);
            if (!set.Any(s => string.Equals(s, toKey, StringComparison.OrdinalIgnoreCase))) set.Add(toKey);
            return true;
        }

        /// <summary>Applies <see cref="MoveEntry{TValue}" /> to every inner dictionary (e.g. per-encounter → per-player).</summary>
        public static int MoveEntryInEach<TOuter, TValue>(IDictionary<string, TOuter> outer, string fromKey, string toKey, Func<TValue, TValue, TValue>? merge = null)
            where TOuter : IDictionary<string, TValue>
        {
            int moved = 0;
            if (outer == null) return 0;
            foreach (var inner in outer.Values)
                if (inner != null && MoveEntry(inner, fromKey, toKey, merge)) moved++;
            return moved;
        }

        private static string? FindKey<TValue>(IDictionary<string, TValue> dict, string key)
        {
            if (dict.ContainsKey(key)) return key;
            return dict.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
