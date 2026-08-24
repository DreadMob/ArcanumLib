using System;
using System.Collections.Generic;

namespace ArcanumLib.Items
{
    /// <summary>
    /// Configuration for the generic <see cref="ItemCharge"/> helpers.
    /// Consumers can set their own attribute and metadata key namespaces, charge suffixes,
    /// and optional resolvers for display names, units, and material names.
    /// </summary>
    public class ItemChargeConfig
    {
        /// <summary>
        /// Short name of the generic charge attribute, e.g. "charge".
        /// </summary>
        public string ChargeAttributeName { get; set; } = "charge";

        /// <summary>
        /// Prefix for the generic charge attribute, e.g. "arcanumlib:attr:".
        /// The full generic charge key is <see cref="AttributePrefix"/> + <see cref="ChargeAttributeName"/>.
        /// </summary>
        public string AttributePrefix { get; set; } = "arcanumlib:attr:";

        /// <summary>
        /// Legacy prefixes for the generic charge attribute, checked in order if the generic key is absent.
        /// Empty string or whitespace allows unprefixed attributes.
        /// </summary>
        public IReadOnlyList<string> LegacyAttributePrefixes { get; set; } = new List<string>();

        /// <summary>
        /// Suffixes recognized as charge attributes.
        /// </summary>
        public string TimeChargeSuffix { get; set; } = "chargehours";

        /// <summary>
        /// Suffix for use-based charge attributes.
        /// </summary>
        public string UseChargeSuffix { get; set; } = "chargeuses";

        /// <summary>
        /// Suffix for percentage charge attributes.
        /// </summary>
        public string PercentChargeSuffix { get; set; } = "chargepct";

        /// <summary>
        /// Prefix for charge metadata keys, e.g. "arcanumlib:".
        /// </summary>
        public string MetaPrefix { get; set; } = "arcanumlib:";

        /// <summary>
        /// Legacy prefixes for charge metadata keys, checked in order if the generic key is absent.
        /// </summary>
        public IReadOnlyList<string> LegacyMetaPrefixes { get; set; } = new List<string>();

        /// <summary>
        /// Default maximum charge capacity when no metadata is present.
        /// </summary>
        public float DefaultChargeMax { get; set; } = 100f;

        /// <summary>
        /// Default charge restored per unit of refuel material.
        /// </summary>
        public float DefaultChargePerUnit { get; set; } = 8f;

        /// <summary>
        /// Charge value at which stats are considered fully active.
        /// </summary>
        public float FullChargeThreshold { get; set; } = 24f;

        /// <summary>
        /// Multiplier used for gated stats when charge is fully active.
        /// </summary>
        public float MaxActiveMultiplier { get; set; } = 1f;

        /// <summary>
        /// Multiplier used for gated stats when charge is between 0 and the full threshold.
        /// </summary>
        public float MinActiveMultiplier { get; set; } = 0.4f;

        /// <summary>
        /// Charge value below which the depleted multiplier is used.
        /// </summary>
        public float DepletedThreshold { get; set; } = 0.05f;

        /// <summary>
        /// Optional logger for non-fatal warnings.
        /// </summary>
        public Vintagestory.API.Common.ILogger? Logger { get; set; }

        /// <summary>
        /// Resolves a display name for a charge short key, or null to fall back to the raw key.
        /// </summary>
        public System.Func<string, string?>? DisplayNameResolver { get; set; }

        /// <summary>
        /// Resolves a unit suffix for a charge short key, or null for none.
        /// </summary>
        public System.Func<string, string?>? UnitResolver { get; set; }

        /// <summary>
        /// Resolves a display name for a refuel material pattern, or null to fall back to the raw pattern.
        /// </summary>
        public System.Func<string, Vintagestory.API.Common.ItemStack?, string?>? MaterialDisplayNameResolver { get; set; }

        /// <summary>
        /// Builds the generic charge attribute key.
        /// </summary>
        public string GetChargeKey() => AttributePrefix + ChargeAttributeName;

        /// <summary>
        /// Builds all candidate charge attribute keys (generic + legacy prefixes + suffixed variants).
        /// </summary>
        public IEnumerable<string> GetAllChargeKeys()
        {
            foreach (var key in GetAllChargeBaseKeys())
                yield return key;

            if (!string.IsNullOrWhiteSpace(TimeChargeSuffix))
            {
                yield return AttributePrefix + TimeChargeSuffix;
                foreach (var prefix in LegacyAttributePrefixes)
                    yield return string.IsNullOrWhiteSpace(prefix) ? TimeChargeSuffix : prefix + TimeChargeSuffix;
            }

            if (!string.IsNullOrWhiteSpace(UseChargeSuffix))
            {
                yield return AttributePrefix + UseChargeSuffix;
                foreach (var prefix in LegacyAttributePrefixes)
                    yield return string.IsNullOrWhiteSpace(prefix) ? UseChargeSuffix : prefix + UseChargeSuffix;
            }

            if (!string.IsNullOrWhiteSpace(PercentChargeSuffix))
            {
                yield return AttributePrefix + PercentChargeSuffix;
                foreach (var prefix in LegacyAttributePrefixes)
                    yield return string.IsNullOrWhiteSpace(prefix) ? PercentChargeSuffix : prefix + PercentChargeSuffix;
            }
        }

        private IEnumerable<string> GetAllChargeBaseKeys()
        {
            yield return GetChargeKey();
            foreach (var prefix in LegacyAttributePrefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                    yield return ChargeAttributeName;
                else
                    yield return prefix + ChargeAttributeName;
            }
        }

        /// <summary>
        /// Builds all candidate metadata keys for the given short metadata name (e.g. "chargemax").
        /// </summary>
        public IEnumerable<string> GetAllMetaKeys(string shortKey)
        {
            yield return MetaPrefix + shortKey;
            foreach (var prefix in LegacyMetaPrefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                    yield return shortKey;
                else
                    yield return prefix + shortKey;
            }
        }

        /// <summary>
        /// Returns true if the short key is a recognized charge attribute.
        /// </summary>
        public bool IsChargeAttribute(string shortKey)
        {
            if (string.IsNullOrEmpty(shortKey)) return false;
            return shortKey == ChargeAttributeName
                || shortKey.EndsWith(TimeChargeSuffix)
                || shortKey.EndsWith(UseChargeSuffix)
                || shortKey.EndsWith(PercentChargeSuffix);
        }

        /// <summary>
        /// Returns true if the short key is a time-based charge attribute.
        /// </summary>
        public bool IsTimeChargeAttribute(string shortKey)
            => !string.IsNullOrEmpty(shortKey) && shortKey.EndsWith(TimeChargeSuffix);

        /// <summary>
        /// Returns true if the short key is a use-based charge attribute.
        /// </summary>
        public bool IsUseChargeAttribute(string shortKey)
            => !string.IsNullOrEmpty(shortKey) && (shortKey == ChargeAttributeName || shortKey.EndsWith(UseChargeSuffix));
    }
}
