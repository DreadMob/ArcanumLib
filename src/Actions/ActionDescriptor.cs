using System;
using Newtonsoft.Json;

namespace ArcanumLib.Actions
{
    /// <summary>
    /// JSON-friendly descriptor for an action. Loaded from assets and executed
    /// through <see cref="ActionRegistry" />.
    /// </summary>
    public class ActionDescriptor
    {
        /// <summary>
        /// Action identifier used to select the handler in <see cref="ActionRegistry" />.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// Optional string arguments passed to the action handler.
        /// </summary>
        [JsonProperty("args")]
        public string[] Args { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Optional cooldown in milliseconds before the same action can be executed
        /// again by the same player. Zero or negative means no cooldown.
        /// </summary>
        [JsonProperty("cooldownMs")]
        public int CooldownMs { get; set; }

        /// <summary>
        /// Optional permission required to execute the action. If set, the player
        /// must have this privilege. Empty means no permission check.
        /// </summary>
        [JsonProperty("requiredPermission")]
        public string RequiredPermission { get; set; } = "";

        /// <summary>
        /// Optional declarative condition evaluated before the handler runs.
        /// If the condition evaluates to false, the action returns
        /// <see cref="ActionOutcome.NotAvailable" /> without calling the handler.
        /// </summary>
        [JsonProperty("condition")]
        public ActionCondition? Condition { get; set; }
    }
}
