using System;
using System.Collections.Generic;
using ArcanumLib.Actions;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ArcanumLib.Items
{
    /// <summary>
    /// Generic helpers for item modes: parsing, active mode selection, tool-mode UI integration,
    /// and mode-based effect/action gating.
    /// </summary>
    public static class ItemModeManager
    {
        /// <summary>
        /// Default configuration using <c>arcanumlib:</c> attribute keys.
        /// </summary>
        public static ItemModeConfig DefaultConfig { get; } = new();

        /// <summary>
        /// Tries to parse the mode list from the given attributes.
        /// </summary>
        /// <param name="attributes">The attributes value.</param>
        /// <param name="modes">When this method returns, contains the <paramref name="modes" /> value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryGetModes(ITreeAttribute? attributes, out List<ItemMode> modes, ItemModeConfig? config = null)
        {
            config ??= DefaultConfig;
            modes = new List<ItemMode>();

            if (attributes == null) return false;

            string? modesJson = attributes.GetString(config.ModesAttributeKey);
            if (string.IsNullOrWhiteSpace(modesJson)) return false;

            try
            {
                var parsed = JsonConvert.DeserializeObject<List<ItemMode>>(modesJson);
                if (parsed != null) modes = parsed;
            }
            catch (Exception ex)
            {
                config.Logger?.Warning("[ArcanumLib] [ItemModeManager] Failed to parse modes: {0}", ex.Message);
                return false;
            }

            return modes.Count > 0;
        }

        /// <summary>
        /// Convenience overload for <see cref="ItemStack" />.
        /// </summary>
        /// <param name="stack">The item stack.</param>
        /// <param name="modes">When this method returns, contains the <paramref name="modes" /> value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryGetModes(ItemStack? stack, out List<ItemMode> modes, ItemModeConfig? config = null)
            => TryGetModes(stack?.Attributes, out modes, config);

        /// <summary>
        /// Returns the active mode index, clamped to [0, modeCount - 1] if modeCount is greater than zero.
        /// </summary>
        /// <param name="attributes">The attributes value.</param>
        /// <param name="modeCount">The mode count value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The active mode index.</returns>
        public static int GetActiveModeIndex(ITreeAttribute? attributes, int modeCount, ItemModeConfig? config = null)
        {
            config ??= DefaultConfig;
            if (attributes == null || modeCount <= 0) return 0;

            int index = attributes.GetInt(config.ModeIndexAttributeKey, 0);
            if (index < 0) index = 0;
            if (index >= modeCount) index = modeCount - 1;
            return index;
        }

        /// <summary>
        /// Returns the active mode from the parsed list, or null if no modes are present.
        /// </summary>
        /// <param name="attributes">The attributes value.</param>
        /// <param name="modes">The modes value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The active mode, or null if none is found.</returns>
        public static ItemMode? GetActiveMode(ITreeAttribute? attributes, List<ItemMode>? modes, ItemModeConfig? config = null)
        {
            if (modes == null || modes.Count == 0) return null;
            int index = GetActiveModeIndex(attributes, modes.Count, config);
            return modes[index];
        }

        /// <summary>
        /// Tries to get the active mode and its id.
        /// </summary>
        /// <param name="attributes">The attributes value.</param>
        /// <param name="activeModeId">When this method returns, contains the <paramref name="activeModeId" /> value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryGetActiveModeId(ITreeAttribute? attributes, out string? activeModeId, ItemModeConfig? config = null)
        {
            activeModeId = null;
            if (!TryGetModes(attributes, out var modes, config)) return false;

            var mode = GetActiveMode(attributes, modes, config);
            activeModeId = mode?.Id;
            return !string.IsNullOrEmpty(activeModeId);
        }

        /// <summary>
        /// Tries to get the actions of the active mode. Returns false if there are no modes or the active mode has no actions.
        /// </summary>
        /// <param name="attributes">The attributes value.</param>
        /// <param name="actions">When this method returns, contains the <paramref name="actions" /> value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>true if the operation succeeded; otherwise, false.</returns>
        public static bool TryGetActiveModeActions(ITreeAttribute? attributes, out List<ActionDescriptor> actions, ItemModeConfig? config = null)
        {
            actions = new List<ActionDescriptor>();
            if (!TryGetModes(attributes, out var modes, config)) return false;

            var mode = GetActiveMode(attributes, modes, config);
            if (mode?.Actions == null || mode.Actions.Count == 0) return false;

            actions = mode.Actions;
            return true;
        }

        /// <summary>
        /// Sets the active mode index on the attributes.
        /// </summary>
        /// <param name="attributes">The attributes value.</param>
        /// <param name="index">The zero-based index.</param>
        /// <param name="config">The config value.</param>
        public static void SetActiveModeIndex(ITreeAttribute? attributes, int index, ItemModeConfig? config = null)
        {
            config ??= DefaultConfig;
            attributes?.SetInt(config.ModeIndexAttributeKey, Math.Max(0, index));
        }

        /// <summary>
        /// Convenience overload that writes the index and marks the slot dirty.
        /// </summary>
        /// <param name="slot">The inventory slot.</param>
        /// <param name="index">The zero-based index.</param>
        /// <param name="config">The config value.</param>
        public static void SetActiveModeIndex(ItemSlot? slot, int index, ItemModeConfig? config = null)
        {
            if (slot?.Itemstack?.Attributes == null) return;
            SetActiveModeIndex(slot.Itemstack.Attributes, index, config);
            slot.MarkDirty();
        }

        /// <summary>
        /// Returns true when an effect or action should run for the given active mode.
        /// An empty <paramref name="effectModeId" /> means "runs in any mode".
        /// </summary>
        /// <param name="effectModeId">The effect mode id value.</param>
        /// <param name="activeModeId">The active mode id value.</param>
        /// <returns>true if the operation should run for mode; otherwise, false.</returns>
        public static bool ShouldRunForMode(string? effectModeId, string? activeModeId)
        {
            if (string.IsNullOrWhiteSpace(effectModeId)) return true;
            if (string.IsNullOrWhiteSpace(activeModeId)) return false;
            return string.Equals(effectModeId, activeModeId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a <see cref="SkillItem" /> array for the vanilla tool mode UI.
        /// Returns null if there are no modes.
        /// </summary>
        /// <param name="capi">The client API instance.</param>
        /// <param name="modes">The modes value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>A collection of tool mode skill items values, or null if none is found.</returns>
        public static SkillItem[]? GetToolModeSkillItems(ICoreClientAPI? capi, List<ItemMode> modes, ItemModeConfig? config = null)
        {
            if (capi == null || modes == null || modes.Count == 0) return null;
            config ??= DefaultConfig;

            var items = new SkillItem[modes.Count];
            for (int i = 0; i < modes.Count; i++)
            {
                var mode = modes[i];
                string displayName = mode?.Name ?? "";
                if (config.NameResolver != null && !string.IsNullOrEmpty(displayName))
                {
                    string? resolved = config.NameResolver(displayName);
                    if (!string.IsNullOrEmpty(resolved)) displayName = resolved;
                }

                if (string.IsNullOrWhiteSpace(displayName)) displayName = $"Mode {i + 1}";

                string code = !string.IsNullOrWhiteSpace(mode?.Id)
                    ? mode.Id!
                    : $"arcanumlib:mode-{i}";

                var skill = new SkillItem
                {
                    Name = displayName,
                    Code = new AssetLocation(code),
                    Linebreak = i % Math.Max(1, config.ModesPerLine) == 0
                };

                if (!string.IsNullOrWhiteSpace(mode?.Icon))
                {
                    skill.WithIcon(capi, mode.Icon);
                }
                else
                {
                    string letter = displayName.Length > 0 ? displayName.Substring(0, 1).ToUpperInvariant() : "?";
                    skill.WithLetterIcon(capi, letter);
                }

                items[i] = skill;
            }

            return items;
        }

        /// <summary>
        /// Returns the current tool mode index for a slot, clamped to the available number of modes.
        /// Returns -1 when the stack has no modes.
        /// </summary>
        /// <param name="slot">The inventory slot.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The tool mode index.</returns>
        public static int GetToolModeIndex(ItemSlot? slot, ItemModeConfig? config = null)
        {
            if (slot?.Itemstack?.Attributes == null) return -1;
            if (!TryGetModes(slot.Itemstack.Attributes, out var modes, config)) return -1;
            return GetActiveModeIndex(slot.Itemstack.Attributes, modes.Count, config);
        }

        /// <summary>
        /// Cycles the active mode by <paramref name="delta" /> (positive or negative) and returns the new mode id.
        /// </summary>
        /// <param name="attributes">The attributes value.</param>
        /// <param name="delta">The delta value.</param>
        /// <param name="config">The config value.</param>
        /// <returns>The cycle active mode, or null if none is found.</returns>
        public static string? CycleActiveMode(ITreeAttribute? attributes, int delta, ItemModeConfig? config = null)
        {
            if (attributes == null) return null;
            if (!TryGetModes(attributes, out var modes, config)) return null;
            if (modes.Count == 0) return null;

            int current = GetActiveModeIndex(attributes, modes.Count, config);
            int next = current + delta;

            if (next < 0) next = modes.Count - 1;
            if (next >= modes.Count) next = 0;

            SetActiveModeIndex(attributes, next, config);
            return modes[next]?.Id;
        }
    }
}
