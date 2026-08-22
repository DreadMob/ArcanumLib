using System.Collections.Generic;
using ProtoBuf;

namespace ArcanumLib.Progression
{
    /// <summary>
    /// Serializable pity counters for one (player, definitionId) pair.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PityCounters
    {
        /// <summary>
        /// Total number of opens recorded.
        /// </summary>
        public int totalOpens;

        /// <summary>
        /// Key = quality tier index, Value = opens since last drop of that tier.
        /// </summary>
        public Dictionary<int, int> opensSinceQuality = new();
    }

    /// <summary>
    /// Serializable pity data for a single player.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PityPlayerData
    {
        /// <summary>
        /// Key = "playerUid::definitionId", Value = counters.
        /// </summary>
        public Dictionary<string, PityCounters> counters = new();
    }
}
