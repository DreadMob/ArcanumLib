using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ArcanumLib.Gui
{
    /// <summary>
    /// Extension methods for <see cref="GuiComposer" /> to add common GUI elements
    /// with less boilerplate.
    /// </summary>
    public static class GuiComposerSlotExtensions
    {
        /// <summary>
        /// Adds a single item slot from an inventory to the GUI composer.
        /// </summary>
        /// <param name="composer">The composer to add the slot to.</param>
        /// <param name="inventory">The inventory that owns the slot.</param>
        /// <param name="slot">The specific slot to render.</param>
        /// <param name="bounds">Element bounds for the slot.</param>
        /// <param name="key">Optional unique key for the slot element.</param>
        /// <returns>The composer, for chaining.</returns>
        public static GuiComposer AddItemSlot(
            this GuiComposer composer,
            IInventory inventory,
            ItemSlot slot,
            ElementBounds bounds,
            string? key = null)
        {
            if (composer.Composed) return composer;

            int index = inventory.GetSlotId(slot);
            if (index < 0) index = 0;

            composer.AddItemSlotGrid(
                inventory,
                packet => composer.Api.Network.SendPacketClient(packet),
                columns: 1,
                selectiveSlots: new[] { index },
                bounds: bounds,
                key: key);

            return composer;
        }
    }
}
