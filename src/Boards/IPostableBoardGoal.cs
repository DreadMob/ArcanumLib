using System.Collections.Generic;
using Vintagestory.API.Server;

namespace ArcanumLib.Boards
{
    /// <summary>
    /// A goal players can choose in the ArcanumLib post window. Goals posted through a mod's own window (or only made
    /// by the server) do not need it.
    /// </summary>
    public interface IPostableBoardGoal : IBoardGoal
    {
        /// <summary>Lang key of the goal's name in the window.</summary>
        string NameKey { get; }

        /// <summary>The goal has a count; otherwise posts get demand 1.</summary>
        bool HasDemand { get; }

        /// <summary>Targets the player may pick from; empty = free text the goal checks in <see cref="IBoardGoal.ValidatePost" />.</summary>
        IReadOnlyList<string> TargetOptions(ContractBoard board, IServerPlayer player);

        /// <summary>The free-text target is an item or block code: the window offers the held item and shows its name.</summary>
        bool TargetIsItem => false;

        /// <summary>
        /// Names of <see cref="TargetOptions" /> as this player sees them (same order), or null to name them from their
        /// item codes. For targets that are ids (a parcel, a player).
        /// </summary>
        IReadOnlyList<string>? TargetOptionNames(ContractBoard board, IServerPlayer player, IReadOnlyList<string> options) => null;
    }
}
