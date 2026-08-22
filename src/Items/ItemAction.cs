using Newtonsoft.Json;

namespace ArcanumLib.Items
{
    /// <summary>
    /// A single action that can be executed by an item or an item mode.
    /// </summary>
    public class ItemAction
    {
        /// <summary>
        /// Action identifier used to select the handler/implementation.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// Optional string arguments passed to the action handler.
        /// </summary>
        [JsonProperty("args")]
        public string[] Args { get; set; } = System.Array.Empty<string>();
    }
}
