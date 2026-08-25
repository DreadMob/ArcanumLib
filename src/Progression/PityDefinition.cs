using System;
using System.Collections.Generic;

namespace ArcanumLib.Progression
{
    /// <summary>
    /// Defines a pity guarantee rule for a loot quality tier.
    /// </summary>
    [Serializable]
    public class PityTierRule
    {
        /// <summary>
        /// Minimum quality tier index this rule applies to (e.g. 3 for Radiant).
        /// </summary>
        public int qualityTierIndex;

        /// <summary>
        /// Number of consecutive non-quality opens before guarantee triggers.
        /// </summary>
        public int opensUntilGuarantee;

        /// <summary>
        /// Optional display name key for GUI.
        /// </summary>
        public string? displayNameKey;
    }

    /// <summary>
    /// Defines a complete pity system for a loot pool / case tier.
    /// </summary>
    [Serializable]
    public class PityDefinition
    {
        /// <summary>
        /// Unique identifier for this pity definition.
        /// </summary>
        public string? definitionId;

        /// <summary>
        /// Rules ordered by quality tier (ascending). Lower tiers reset higher tier counters.
        /// </summary>
        public List<PityTierRule> rules = new();

        /// <summary>
        /// Validate and sort rules by quality tier.
        /// </summary>
        public void Validate()
        {
            rules ??= new List<PityTierRule>();
            rules.Sort((a, b) => a.qualityTierIndex.CompareTo(b.qualityTierIndex));
        }

        /// <summary>
        /// Get the guaranteed quality for given pity state, or 0 if none.
        /// </summary>
        /// <param name="opensSinceQuality">Key = quality tier index, Value = opens since last drop.</param>
        /// <returns>The guaranteed quality.</returns>
        public int GetGuaranteedQuality(Dictionary<int, int> opensSinceQuality)
        {
            if (opensSinceQuality == null) return 0;

            // Check highest quality first (strongest guarantee wins)
            for (int i = rules.Count - 1; i >= 0; i--)
            {
                var rule = rules[i];
                if (rule.opensUntilGuarantee <= 0) continue;
                opensSinceQuality.TryGetValue(rule.qualityTierIndex, out int opens);
                if (opens >= rule.opensUntilGuarantee)
                    return rule.qualityTierIndex;
            }
            return 0;
        }
    }
}
