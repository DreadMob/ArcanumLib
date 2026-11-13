using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Boards
{
    /// <summary>
    /// <c>/arcanum boards list | show &lt;board&gt; | cancel &lt;board&gt; &lt;contract&gt; | resolve &lt;board&gt; &lt;contract&gt; pay|refund</c> — contract boards for admins:
    /// what boards exist, what is on one, and removing a contract with everything returned to its owners.
    /// </summary>
    public static class BoardCommands
    {
        public static void Register(ICoreServerAPI sapi, BoardRegistry registry)
        {
            var parsers = sapi.ChatCommands.Parsers;
            sapi.ChatCommands.GetOrCreate("arcanum")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("boards")
                    .WithDescription("Contract boards. Usage: /arcanum boards list | show <board> | cancel <board> <contract> | resolve <board> <contract> pay|refund")
                    .RequiresPrivilege(Privilege.controlserver)
                    .BeginSubCommand("list")
                        .WithDescription("Lists the contract boards and how many contracts each has")
                        .HandleWith(_ => List(registry))
                    .EndSubCommand()
                    .BeginSubCommand("show")
                        .WithDescription("Lists the contracts of a board")
                        .WithArgs(parsers.Word("board"))
                        .HandleWith(args => Show(sapi, registry, (string)args[0]))
                    .EndSubCommand()
                    .BeginSubCommand("cancel")
                        .WithDescription("Removes a contract; escrow and delivered items go back to their owners")
                        .WithArgs(parsers.Word("board"), parsers.Word("contract"))
                        .HandleWith(args => Cancel(registry, (string)args[0], (string)args[1]))
                    .EndSubCommand()
                    .BeginSubCommand("resolve")
                        .WithDescription("Decides a done or disputed contract: pay the claimer or refund the author")
                        .WithArgs(parsers.Word("board"), parsers.Word("contract"), parsers.WordRange("decision", "pay", "refund"))
                        .HandleWith(args => Resolve(registry, (string)args[0], (string)args[1], (string)args[2]))
                    .EndSubCommand()
                .EndSubCommand();
        }

        private static TextCommandResult List(BoardRegistry registry)
        {
            if (registry.Boards.Count == 0) return TextCommandResult.Success("No contract boards.");
            var sb = new StringBuilder("Contract boards:");
            foreach (var board in registry.Boards.Values.OrderBy(b => b.Key, StringComparer.OrdinalIgnoreCase))
            {
                var contracts = board.State.Contracts;
                sb.Append($"\n{board.Key}: {contracts.Count} contract(s), {contracts.Count(c => c.State == BoardStates.Open)} open, "
                    + $"{contracts.Count(c => c.State == BoardStates.Claimed)} claimed, {contracts.Count(c => c.State == BoardStates.Fulfilled)} fulfilled, "
                    + $"{contracts.Count(c => c.State == BoardStates.Disputed)} disputed");
            }
            return TextCommandResult.Success(sb.ToString());
        }

        private static TextCommandResult Show(ICoreServerAPI sapi, BoardRegistry registry, string key)
        {
            var board = registry.Get(key);
            if (board == null) return TextCommandResult.Error($"No board '{key}'.");
            if (board.State.Contracts.Count == 0) return TextCommandResult.Success($"{board.Key}: empty.");

            double now = sapi.World.Calendar.TotalDays;
            var sb = new StringBuilder($"{board.Key}:");
            foreach (var c in board.State.Contracts)
            {
                string target = c.TargetPlayerName ?? c.TargetName ?? c.TargetCode ?? "?";
                string expires = c.ExpireTotalDays > 0 ? $", {Math.Max(0, c.ExpireTotalDays - now):0.#} d left" : "";
                string claimer = c.ClaimedByName != null ? $", by {c.ClaimedByName}" : "";
                string dispute = c.GetData(ContractBoard.DisputeKey) is string reason ? $", disputed: {reason}" : "";
                sb.Append($"\n{c.Id} [{c.State}] {c.Source} {c.AuthorName ?? "server"}: {c.Kind} {target} ×{c.Demand}{claimer}{expires}{dispute}");
            }
            return TextCommandResult.Success(sb.ToString());
        }

        private static TextCommandResult Cancel(BoardRegistry registry, string key, string contractId)
        {
            var board = registry.Get(key);
            if (board == null) return TextCommandResult.Error($"No board '{key}'.");
            return board.ForceCancel(contractId)
                ? TextCommandResult.Success($"Contract {contractId} removed, rewards returned.")
                : TextCommandResult.Error($"No contract '{contractId}' on {board.Key}.");
        }

        private static TextCommandResult Resolve(BoardRegistry registry, string key, string contractId, string decision)
        {
            var board = registry.Get(key);
            if (board == null) return TextCommandResult.Error($"No board '{key}'.");
            bool pay = decision == "pay";
            return board.Resolve(contractId, pay)
                ? TextCommandResult.Success(pay ? $"Contract {contractId} accepted, the claimer can collect." : $"Contract {contractId} refunded to the author.")
                : TextCommandResult.Error($"No done or disputed contract '{contractId}' on {board.Key}.");
        }
    }
}
