using System.Collections.Generic;
using ArcanumLib.Actions;
using Newtonsoft.Json;

namespace ArcanumLib.Items
{
    /// <summary>
    /// A selectable mode for an item (e.g. bound to the F-key tool mode cycle).
    /// Each mode can have its own display name, icon, and list of actions.
    /// </summary>
    public class ItemMode
    {
        /// <summary>
        /// Unique mode identifier. Also used as the <see cref="Vintagestory.API.Common.AssetLocation"/> code for tool modes.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// Display name for the mode. May be a raw string or a localization key depending on the consumer.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Optional icon path/code. If empty, the first letter of <see cref="Name"/> is used.
        /// </summary>
        [JsonProperty("icon")]
        public string Icon { get; set; } = "";

        /// <summary>
        /// Actions associated with this mode. When the mode is active, these actions are used in place of the item's default actions.
        /// </summary>
        [JsonProperty("actions")]
        public List<ActionDescriptor> Actions { get; set; } = new List<ActionDescriptor>();
    }
}
