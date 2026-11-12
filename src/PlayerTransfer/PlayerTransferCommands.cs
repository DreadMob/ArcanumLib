using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace ArcanumLib.PlayerTransfer
{
    /// <summary>
    /// <c>/arcanum transfer &lt;from&gt; &lt;to&gt; [confirm]</c> — moves all progress of one account onto another (an
    /// account change) through every registered participant, then the after-transfer steps (website database and the
    /// like). Mod data moves at once, the progress kept on the player entities when each account is next online; the
    /// source ends up with none. <c>/arcanum transfer status</c> lists transfers still waiting for an account to join.
    /// </summary>
    public static class PlayerTransferCommands
    {
        public static void Register(ICoreServerAPI sapi, PlayerTransferModSystem system)
        {
            sapi.ChatCommands.GetOrCreate("arcanum")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("transfer")
                    .WithDescription("Moves all progress from one account to another (merged into the target). Usage: /arcanum transfer <from> <to> confirm | status")
                    .RequiresPrivilege(Privilege.controlserver)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("from"),
                        sapi.ChatCommands.Parsers.OptionalWord("to"),
                        sapi.ChatCommands.Parsers.OptionalWord("confirm"))
                    .HandleWith(args => Handle(sapi, system, args))
                .EndSubCommand();
        }

        private static TextCommandResult Handle(ICoreServerAPI sapi, PlayerTransferModSystem system, TextCommandCallingArgs args)
        {
            string? fromArg = args[0] as string;
            string? toArg = args[1] as string;
            string? confirm = args[2] as string;

            if (string.Equals(fromArg, "status", StringComparison.OrdinalIgnoreCase))
            {
                var pending = system.Pending;
                if (pending.Count == 0) return TextCommandResult.Success("No transfers are waiting.");
                return TextCommandResult.Success("Waiting:\n" + string.Join("\n", pending.Select(p =>
                    $"{p.FromName} -> {p.ToName}: " + (p.Extracted ? $"waiting for {p.ToName} to join" : $"waiting for {p.FromName} to join"))));
            }

            if (string.IsNullOrWhiteSpace(fromArg) || string.IsNullOrWhiteSpace(toArg))
                return TextCommandResult.Error("Usage: /arcanum transfer <from> <to> confirm");

            if (!TryResolve(sapi, fromArg!, out string fromUid, out string fromName))
                return TextCommandResult.Error($"Player '{fromArg}' has never joined this server.");
            if (!TryResolve(sapi, toArg!, out string toUid, out string toName))
                return TextCommandResult.Error($"Player '{toArg}' has never joined this server — log in once with the new account first.");
            if (string.Equals(fromUid, toUid, StringComparison.OrdinalIgnoreCase))
                return TextCommandResult.Error("Source and target are the same account.");

            if (!string.Equals(confirm, "confirm", StringComparison.OrdinalIgnoreCase))
            {
                var what = PlayerDataTransfer.All.Select(p => p.Id)
                    .Concat(PlayerDataTransfer.AfterTransferSteps.Select(s => s.id))
                    .Append("player attributes").Distinct(StringComparer.OrdinalIgnoreCase);
                return TextCommandResult.Success(
                    $"This moves ALL progress of {fromName} onto {toName} and leaves {fromName} with none: {string.Join(", ", what)}. " +
                    $"Numbers add up with what {toName} already has. It cannot be undone.\nRun: /arcanum transfer {fromName} {toName} confirm");
            }

            PlayerTransferContext ctx;
            try
            {
                ctx = system.Execute(fromUid, toUid, fromName, toName);
            }
            catch (Exception ex)
            {
                sapi.Logger.Error("[PlayerTransfer] {0} -> {1} failed: {2}", fromName, toName, ex);
                return TextCommandResult.Error("Transfer failed: " + ex.Message);
            }

            var caller = args.Caller.Player as IServerPlayer;
            void Reply(string message)
            {
                sapi.Logger.Notification("[PlayerTransfer] {0} -> {1}: {2}", fromName, toName, message);
                sapi.Event.EnqueueMainThreadTask(() => caller?.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.CommandSuccess), "arcanum-transfer");
            }
            foreach (var (id, step) in PlayerDataTransfer.AfterTransferSteps)
            {
                try { step(ctx, Reply); }
                catch (Exception ex)
                {
                    ctx.Failures.Add(id);
                    sapi.Logger.Error("[PlayerTransfer] after-transfer step '{0}' failed ({1} -> {2}): {3}", id, fromName, toName, ex);
                }
            }

            string report = string.Join("\n", ctx.Report);
            string failures = ctx.Failures.Count > 0 ? $"\nFAILED (see server log): {string.Join(", ", ctx.Failures)}" : "";
            return TextCommandResult.Success($"Transferred {fromName} -> {toName}:\n{report}{failures}");
        }

        /// <summary>An online player by name, else the last known name or the uid of anyone who ever joined.</summary>
        public static bool TryResolve(ICoreServerAPI sapi, string nameOrUid, out string uid, out string name)
        {
            var online = sapi.World.AllOnlinePlayers.FirstOrDefault(p => string.Equals(p.PlayerName, nameOrUid, StringComparison.OrdinalIgnoreCase));
            if (online != null)
            {
                uid = online.PlayerUID;
                name = online.PlayerName;
                return true;
            }

            var data = sapi.PlayerData.GetPlayerDataByLastKnownName(nameOrUid) ?? sapi.PlayerData.GetPlayerDataByUid(nameOrUid);
            uid = data?.PlayerUID ?? "";
            name = data?.LastKnownPlayername ?? nameOrUid;
            return !string.IsNullOrWhiteSpace(uid);
        }
    }
}
