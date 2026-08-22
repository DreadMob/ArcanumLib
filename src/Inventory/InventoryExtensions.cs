using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Inventory
{
    /// <summary>
    /// Common bulk helpers for Vintage Story inventories and players.
    /// </summary>
    public static class InventoryExtensions
    {
        /// <summary>
        /// Tries to give the stack to the player, otherwise drops it in the world.
        /// </summary>
        /// <param name="player">The player receiving the item.</param>
        /// <param name="stack">The stack to give.</param>
        /// <param name="world">World used for spawning a dropped item.</param>
        /// <param name="dropPosition">Optional drop position. Defaults to the entity position.</param>
        /// <returns>True when the stack was placed in the inventory.</returns>
        public static bool TryGiveOrDrop(this IPlayer player, ItemStack stack, IWorldAccessor? world, Vec3d? dropPosition = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (stack == null) throw new ArgumentNullException(nameof(stack));

            if (player.InventoryManager?.TryGiveItemstack(stack) == true) return true;

            var pos = dropPosition ?? player.Entity?.Pos?.XYZ;
            if (pos != null) world?.SpawnItemEntity(stack, pos);
            return false;
        }

        /// <summary>
        /// Tries to give the stack to the server player, otherwise drops it at their feet.
        /// </summary>
        public static bool TryGiveOrDrop(this IServerPlayer player, ItemStack stack)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return TryGiveOrDrop(player, stack, player.Entity?.World, player.Entity?.Pos?.XYZ);
        }

        /// <summary>
        /// Counts stacks matching the predicate.
        /// </summary>
        public static int CountItems(this IInventory inventory, Predicate<ItemSlot> predicate)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            int total = 0;
            foreach (var slot in inventory)
            {
                if (slot?.Itemstack != null && predicate(slot)) total += slot.StackSize;
            }
            return total;
        }

        /// <summary>
        /// Counts how many items with the given code are in the inventory.
        /// </summary>
        public static int CountItem(this IInventory inventory, string code)
            => CountItem(inventory, new AssetLocation(code));

        /// <summary>
        /// Counts how many items with the given code are in the inventory.
        /// </summary>
        public static int CountItem(this IInventory inventory, AssetLocation code)
        {
            if (inventory == null || code == null) return 0;
            return CountItems(inventory, slot => slot?.Itemstack?.Collectible?.Code?.Equals(code) == true);
        }

        /// <summary>
        /// Returns the first slot that matches the predicate, or null.
        /// </summary>
        public static ItemSlot? FindFirst(this IInventory inventory, Predicate<ItemSlot> predicate)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            foreach (var slot in inventory)
            {
                if (slot != null && predicate(slot)) return slot;
            }
            return null;
        }

        /// <summary>
        /// Removes up to <paramref name="quantity"/> of items matching the code.
        /// Returns the number actually removed.
        /// </summary>
        public static int ConsumeItems(this IInventory inventory, AssetLocation code, int quantity)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (quantity <= 0) return 0;

            int remaining = quantity;
            foreach (var slot in inventory)
            {
                if (slot?.Itemstack?.Collectible?.Code?.Equals(code) != true) continue;

                int remove = Math.Min(slot.StackSize, remaining);
                slot.TakeOut(remove);
                slot.MarkDirty();
                remaining -= remove;
                if (remaining <= 0) break;
            }

            return quantity - remaining;
        }

        /// <summary>
        /// Removes up to <paramref name="quantity"/> of items matching the code.
        /// Returns the number actually removed.
        /// </summary>
        public static int ConsumeItems(this IInventory inventory, string code, int quantity)
            => ConsumeItems(inventory, new AssetLocation(code), quantity);

        /// <summary>
        /// Checks whether the inventory contains at least <paramref name="quantity"/> of the item.
        /// </summary>
        public static bool HasAtLeast(this IInventory inventory, AssetLocation code, int quantity)
            => CountItem(inventory, code) >= quantity;

        /// <summary>
        /// Checks whether the inventory contains at least <paramref name="quantity"/> of the item.
        /// </summary>
        public static bool HasAtLeast(this IInventory inventory, string code, int quantity)
            => CountItem(inventory, new AssetLocation(code)) >= quantity;
    }
}
