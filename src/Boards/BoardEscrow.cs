using System;
using System.Collections.Generic;
using ArcanumLib.Inventory;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ArcanumLib.Boards
{
    /// <summary>
    /// Escrow of player-posted rewards: the stacks are frozen in the board state while the contract is on the board,
    /// paid to the claimer, or returned to the author on cancel / expiry. Nothing is ever destroyed: a stack that cannot
    /// be serialized stays in the poster's slot, a refund for an offline author goes to
    /// <see cref="BoardOptions.OfflineRefund" />.
    /// </summary>
    public sealed class BoardEscrow
    {
        private readonly ContractBoard board;

        internal BoardEscrow(ContractBoard board)
        {
            this.board = board;
        }

        /// <summary>The contract has escrowed stacks.</summary>
        public bool Holds(BoardContract contract) => board.State.EscrowStacksByContract.ContainsKey(contract.Id);

        /// <summary>"name ×count, …" of the stacks in the inventory, or null when it is empty.</summary>
        public static string? Summarize(IInventory? inv)
        {
            if (inv == null) return null;
            var parts = new List<string>();
            foreach (var slot in inv)
            {
                var stack = slot?.Itemstack;
                if (stack == null) continue;
                parts.Add($"{stack.GetName()} ×{stack.StackSize}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>
        /// Moves every stack of the inventory into the contract's escrow and lists them (summed per code) in
        /// <see cref="BoardContract.Rewards" />. Returns false when nothing could be taken.
        /// </summary>
        public bool Deposit(BoardContract contract, IInventory? inv)
        {
            if (inv == null) return false;
            try
            {
                var stacks = new List<byte[]>();
                var aggregated = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var order = new List<string>();
                var failedSlots = new HashSet<ItemSlot>();

                foreach (var slot in inv)
                {
                    if (slot?.Empty != false) continue;
                    var stack = slot.Itemstack;
                    if (stack?.Collectible == null) continue;

                    try
                    {
                        stacks.Add(stack.ToBytes());
                    }
                    catch (Exception ex)
                    {
                        board.Sapi.Logger.Error("[Boards] Failed to serialize escrow stack for contract {0}: {1}", contract.Id, ex);
                        failedSlots.Add(slot);
                        continue;
                    }

                    if (stack.Collectible.Code != null)
                    {
                        string code = stack.Collectible.Code.ToString();
                        if (!aggregated.TryGetValue(code, out int prev)) order.Add(code);
                        aggregated[code] = prev + stack.StackSize;
                    }
                }

                if (stacks.Count == 0) return false;

                board.State.EscrowStacksByContract[contract.Id] = stacks;

                contract.Rewards ??= new List<BoardRewardEntry>();
                contract.Rewards.RemoveAll(r => !string.IsNullOrEmpty(r.ItemCode));
                foreach (var code in order)
                    contract.Rewards.Add(new BoardRewardEntry { ItemCode = code, ItemAmount = aggregated[code] });

                foreach (var slot in inv)
                {
                    if (slot?.Empty != false || failedSlots.Contains(slot)) continue;
                    slot.Itemstack = null;
                    slot.MarkDirty();
                }

                return true;
            }
            catch (Exception ex)
            {
                board.Sapi.Logger.Error("[Boards] Escrow deposit failed for contract {0}: {1}", contract.Id, ex);
                return false;
            }
        }

        /// <summary>Gives the escrowed stacks to the claimer.</summary>
        public void Payout(BoardContract contract, IServerPlayer player)
        {
            if (!board.State.EscrowStacksByContract.TryGetValue(contract.Id, out var stacks)) return;
            board.State.EscrowStacksByContract.Remove(contract.Id);

            foreach (var bytes in stacks)
            {
                var stack = board.DeserializeStack(bytes);
                if (stack != null) player.TryGiveOrDrop(stack);
            }
        }

        /// <summary>Returns the escrowed stacks to the author (or the offline refund sink).</summary>
        public void Refund(BoardContract contract)
        {
            if (!board.State.EscrowStacksByContract.TryGetValue(contract.Id, out var stacks)) return;
            board.State.EscrowStacksByContract.Remove(contract.Id);

            var author = contract.AuthorUid != null ? board.FindOnline(contract.AuthorUid) : null;
            foreach (var bytes in stacks)
            {
                if (author != null)
                {
                    var stack = board.DeserializeStack(bytes);
                    if (stack != null) author.TryGiveOrDrop(stack);
                }
                else
                {
                    board.ReturnOffline(contract, bytes);
                }
            }
        }
    }
}
