using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Items
{
    /// <summary>
    /// Generic, data-driven helpers for item charge attributes.
    /// Supports generic 'charge', legacy '*chargehours', '*chargeuses' and '*chargepct' attributes,
    /// plus metadata-driven max charge, refuel materials, charge-per-unit and stat gating.
    /// </summary>
    public static class ItemCharge
    {
        /// <summary>
        /// Default configuration with <c>arcanumlib:</c> keys and no legacy prefixes.
        /// </summary>
        public static ItemChargeConfig DefaultConfig { get; } = new();

        /// <summary>
        /// Finds the charge attribute key on the stack. Prefers the generic key,
        /// then legacy keys, then explicitly configured suffixed charge keys.
        /// Returns null if no charge key is found.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The find charge key, or null if none is found.</returns>
        public static string? FindChargeKey(ItemStack? stack, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (stack?.Attributes == null) return null;

            foreach (var key in config.GetAllChargeKeys())
            {
                if (stack.Attributes.HasAttribute(key))
                    return key;
            }

            return null;
        }

        /// <summary>
        /// Returns the short (unprefixed) name of the charge attribute, or null if none.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The charge short key, or null if none is found.</returns>
        public static string? GetChargeShortKey(ItemStack? stack, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            string? key = FindChargeKey(stack, config);
            if (key == null) return null;

            if (key.StartsWith(config.AttributePrefix))
                return key.Substring(config.AttributePrefix.Length);

            foreach (var prefix in config.LegacyAttributePrefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix)) continue;
                if (key.StartsWith(prefix))
                    return key.Substring(prefix.Length);
            }

            return key;
        }

        /// <summary>
        /// Current charge value.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The charge value.</returns>
        public static float GetChargeValue(ItemStack? stack, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            string? key = FindChargeKey(stack, config);
            if (key == null || stack?.Attributes == null) return 0f;
            return stack.Attributes.GetFloat(key, 0f);
        }

        /// <summary>
        /// Sets the current charge value, clamped to [0, max].
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="value">The value to set or compare.</param>
        /// <param name="config">The config value.</param>
        public static void SetChargeValue(ItemStack? stack, float value, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (stack?.Attributes == null) return;

            string? key = FindChargeKey(stack, config);
            if (key == null) return;

            float max = GetChargeMax(stack, config);
            stack.Attributes.SetFloat(key, GameMath.Clamp(value, 0f, max));
        }

        /// <summary>
        /// Maximum charge capacity for the stack.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The charge max.</returns>
        public static float GetChargeMax(ItemStack? stack, ItemChargeConfig? config = null)
            => GetMetaFloat(stack, "chargemax", config?.DefaultChargeMax ?? DefaultConfig.DefaultChargeMax, config);

        /// <summary>
        /// Returns the current charge as a percentage of the maximum (0..100).
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The charge percentage.</returns>
        public static float GetChargePercentage(ItemStack? stack, ItemChargeConfig? config = null)
        {
            float max = GetChargeMax(stack, config);
            if (max <= 0f) return 0f;
            return GetChargeValue(stack, config) / max * 100f;
        }

        /// <summary>
        /// Charge restored per unit of refuel material.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The charge per unit.</returns>
        public static float GetChargePerUnit(ItemStack? stack, ItemChargeConfig? config = null)
            => GetMetaFloat(stack, "chargeperunit", config?.DefaultChargePerUnit ?? DefaultConfig.DefaultChargePerUnit, config);

        /// <summary>
        /// List of refuel material patterns accepted by this stack.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The charge materials.</returns>
        public static List<string> GetChargeMaterials(ItemStack? stack, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            var result = new List<string>();
            string? json = GetMetaString(stack, "chargematerials", config);
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                var parsed = JsonConvert.DeserializeObject<List<string>>(json);
                if (parsed != null) result.AddRange(parsed.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            catch (Exception ex)
            {
                config.Logger?.Warning("[ArcanumLib] [ItemCharge] Failed to parse charge materials: {0}", ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Returns true if the stack has at least one refuel material.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation has charge materials; otherwise, false.</returns>
        public static bool HasChargeMaterials(ItemStack? stack, ItemChargeConfig? config = null)
            => GetChargeMaterials(stack, config).Count > 0;

        /// <summary>
        /// Returns true if the stack currently has a charge attribute.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if charged; otherwise, false.</returns>
        public static bool IsCharged(ItemStack? stack, ItemChargeConfig? config = null)
            => FindChargeKey(stack, config) != null;

        /// <summary>
        /// Returns the display name for a charge attribute, optionally with the first refuel material in parentheses.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="shortKey">The short key value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The charge display name, or null if none is found.</returns>
        public static string? GetChargeDisplayName(ItemStack? stack, string? shortKey, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (string.IsNullOrEmpty(shortKey) || stack?.Attributes == null)
                return shortKey;

            string? displayName = config.DisplayNameResolver?.Invoke(shortKey);
            if (string.IsNullOrEmpty(displayName)) displayName = shortKey;

            if (config.IsChargeAttribute(shortKey) && HasChargeMaterials(stack, config))
            {
                string? material = GetFirstChargeMaterialDisplayName(stack, config);
                if (!string.IsNullOrEmpty(material))
                    return $"{displayName} ({material})";
            }

            return displayName;
        }

        /// <summary>
        /// Resolves the display name for the first refuel material pattern, if any.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The first charge material display name, or null if none is found.</returns>
        public static string? GetFirstChargeMaterialDisplayName(ItemStack? stack, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            var materials = GetChargeMaterials(stack, config);
            if (materials.Count == 0) return null;

            string first = materials[0].TrimEnd('-');
            if (config.MaterialDisplayNameResolver != null)
            {
                string? resolved = config.MaterialDisplayNameResolver(first, stack);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }

            return first;
        }

        /// <summary>
        /// Returns the unit suffix for a charge attribute, or empty string if none.
        /// </summary>
        /// <param name="shortKey">The short key value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The charge unit string, or null if none is found.</returns>
        public static string GetChargeUnit(string? shortKey, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (string.IsNullOrEmpty(shortKey)) return "";
            if (shortKey == config.ChargeAttributeName) return "";

            string? resolved = config.UnitResolver?.Invoke(shortKey);
            if (resolved != null) return resolved;

            if (shortKey.EndsWith(config.TimeChargeSuffix)) return "h";
            return "";
        }

        /// <summary>
        /// Determines whether the source item can refill the sink item's charge pool.
        /// </summary>
        /// <param name="sinkStack">The item stack.</param>
        /// <param name="sourceStack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation can recharge with; otherwise, false.</returns>
        public static bool CanRechargeWith(ItemStack? sinkStack, ItemStack? sourceStack, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            if (!IsCharged(sinkStack, config)) return false;

            string? chargeKey = FindChargeKey(sinkStack, config);
            if (chargeKey == null) return false;

            float currentCharge = sinkStack.Attributes.GetFloat(chargeKey, 0f);
            float chargeMax = GetChargeMax(sinkStack, config);
            if (currentCharge >= chargeMax) return false;

            var materials = GetChargeMaterials(sinkStack, config);
            if (materials.Count == 0) return false;

            string sourcePath = sourceStack.Collectible.Code.Path;
            string sourceFullCode = sourceStack.Collectible.Code.ToString();

            foreach (var pattern in materials)
            {
                if (string.IsNullOrEmpty(pattern)) continue;
                if (sourcePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                    || sourceFullCode.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Adds one unit of charge to the sink item and returns whether charge changed.
        /// <paramref name="consumedQuantity" /> is set to 1 if charge was added, 0 otherwise.
        /// </summary>
        /// <param name="sinkStack">The item stack.</param>
        /// <param name="consumedQuantity">When this method returns, contains the <paramref name="consumedQuantity" /> value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryRecharge(ItemStack? sinkStack, out int consumedQuantity, ItemChargeConfig? config = null)
        {
            consumedQuantity = 0;
            if (sinkStack?.Attributes == null) return false;

            config ??= DefaultConfig;
            string? chargeKey = FindChargeKey(sinkStack, config);
            if (chargeKey == null) return false;

            float currentCharge = sinkStack.Attributes.GetFloat(chargeKey, 0f);
            float chargeMax = GetChargeMax(sinkStack, config);
            if (currentCharge >= chargeMax) return false;

            float chargePerUnit = GetChargePerUnit(sinkStack, config);
            float newCharge = Math.Min(chargeMax, currentCharge + chargePerUnit);

            sinkStack.Attributes.SetFloat(chargeKey, newCharge);
            consumedQuantity = 1;
            return true;
        }

        /// <summary>
        /// Tries to consume a flat amount of charge from the stack.
        /// Returns true if any charge was consumed.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="amount">The amount value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryConsumeCharge(ItemStack? stack, float amount, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (stack?.Attributes == null || amount <= 0f) return false;

            string? chargeKey = FindChargeKey(stack, config);
            if (chargeKey == null) return false;

            float currentCharge = stack.Attributes.GetFloat(chargeKey, 0f);
            if (currentCharge <= 0f) return false;

            float newCharge = Math.Max(0f, currentCharge - amount);
            stack.Attributes.SetFloat(chargeKey, newCharge);
            return true;
        }

        /// <summary>
        /// Returns the stat multiplier from active charge gating, or false if the attribute is not gated.
        /// Only time-based charges gate other stats by default.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="attributeName">The attribute name value.</param>
        /// <param name="multiplier">When this method returns, contains the <paramref name="multiplier" /> value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryGetChargeGatingMultiplier(ItemStack? stack, string? attributeName, out float multiplier, ItemChargeConfig? config = null)
        {
            multiplier = 1f;

            config ??= DefaultConfig;
            if (stack?.Attributes == null || string.IsNullOrEmpty(attributeName)) return false;

            string? chargeKey = FindChargeKey(stack, config);
            if (chargeKey == null) return false;

            string? chargeShort = GetChargeShortKey(stack, config);
            if (chargeShort == null) return false;

            // The charge attribute itself and percent-charge attributes are never gated.
            if (attributeName == chargeShort || attributeName.EndsWith(config.PercentChargeSuffix)) return false;

            // Only time-based charges gate other stats.
            if (!config.IsTimeChargeAttribute(chargeShort)) return false;

            string mode = GetMetaString(stack, "chargemode", config) ?? "all";

            bool isGated;
            if (mode == "partial")
            {
                string? gatedJson = GetMetaString(stack, "chargegatedattrs", config) ?? "[]";
                var gated = TryParseStringList(gatedJson, config);
                isGated = gated.Contains(attributeName, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                isGated = true;
            }

            if (!isGated) return false;

            float charge = stack.Attributes.GetFloat(chargeKey, 0f);
            float depletedMult = GetMetaFloat(stack, "chargedepletedmult", 0f, config);

            if (charge <= 0f || charge < config.DepletedThreshold)
            {
                multiplier = depletedMult;
                return true;
            }

            if (charge >= config.FullChargeThreshold)
            {
                multiplier = config.MaxActiveMultiplier;
            }
            else
            {
                float t = charge / config.FullChargeThreshold;
                multiplier = GameMath.Clamp(
                    config.MinActiveMultiplier + (config.MaxActiveMultiplier - config.MinActiveMultiplier) * t,
                    config.MinActiveMultiplier,
                    config.MaxActiveMultiplier);
            }

            return true;
        }

        /// <summary>
        /// Drains time-based charge from all '*chargehours' attributes on the stack.
        /// Returns true if any charge changed.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="elapsedHours">The elapsed hours value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryDrainTimeCharge(ItemStack? stack, float elapsedHours, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (stack?.Attributes == null || elapsedHours <= 0f) return false;

            bool changed = false;
            if (stack.Attributes is not TreeAttribute tree) return false;

            foreach (var key in GetTimeChargeKeys(stack, config))
            {
                float currentCharge = stack.Attributes.GetFloat(key, 0f);
                if (currentCharge <= 0f) continue;

                float newCharge = Math.Max(0f, currentCharge - elapsedHours);
                stack.Attributes.SetFloat(key, newCharge);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Gets all time-based charge attribute keys present on the stack.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The time charge keys.</returns>
        public static List<string> GetTimeChargeKeys(ItemStack? stack, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            var result = new List<string>();
            if (stack?.Attributes is not TreeAttribute tree) return result;

            foreach (var kvp in tree)
                if (kvp.Key.EndsWith(config.TimeChargeSuffix))
                    result.Add(kvp.Key);

            return result;
        }

        /// <summary>
        /// Returns true if the stack has any time-based charge remaining.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation has any time charge; otherwise, false.</returns>
        public static bool HasAnyTimeCharge(ItemStack? stack, ItemChargeConfig? config = null)
        {
            foreach (var key in GetTimeChargeKeys(stack, config))
                if (stack?.Attributes?.GetFloat(key, 0f) > 0f)
                    return true;
            return false;
        }

        /// <summary>
        /// Reads a float from the first matching metadata key.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="shortKey">The short key value.</param>
        /// <param name="defaultValue">The default value to use when none is found.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The meta float.</returns>
        public static float GetMetaFloat(ItemStack? stack, string shortKey, float defaultValue, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (stack?.Attributes == null) return defaultValue;

            foreach (var key in config.GetAllMetaKeys(shortKey))
            {
                if (stack.Attributes.HasAttribute(key))
                    return stack.Attributes.GetFloat(key, defaultValue);
            }

            return defaultValue;
        }

        /// <summary>
        /// Reads a string from the first matching metadata key.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="shortKey">The short key value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The meta string, or null if none is found.</returns>
        public static string? GetMetaString(ItemStack? stack, string shortKey, ItemChargeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (stack?.Attributes == null) return null;

            foreach (var key in config.GetAllMetaKeys(shortKey))
            {
                if (stack.Attributes.HasAttribute(key))
                    return stack.Attributes.GetString(key, null);
            }

            return null;
        }

        private static List<string> TryParseStringList(string json, ItemChargeConfig? config)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<List<string>>(json);
                if (parsed != null) return parsed.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
            catch (Exception ex)
            {
                config?.Logger?.Warning("[ArcanumLib] [ItemCharge] Failed to parse string list: {0}", ex.Message);
            }

            return new List<string>();
        }
    }
}
