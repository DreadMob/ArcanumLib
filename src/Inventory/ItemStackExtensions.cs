using Vintagestory.API.Common;

namespace ArcanumLib.Inventory
{
    /// <summary>
    /// Common helpers for <see cref="ItemStack"/> comparisons and checks.
    /// </summary>
    public static class ItemStackExtensions
    {
        /// <summary>
        /// Returns true when the stack's collectible code equals the given code.
        /// Comparison is case-insensitive, matching Vintage Story asset code conventions.
        /// </summary>
        public static bool HasCollectibleCode(this ItemStack? stack, string code)
        {
            if (stack?.Collectible?.Code == null || string.IsNullOrWhiteSpace(code)) return false;
            return stack.Collectible.Code.ToString().Equals(code, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true when the stack's collectible code equals the given code.
        /// </summary>
        public static bool HasCollectibleCode(this ItemStack? stack, AssetLocation code)
            => stack?.Collectible?.Code?.Equals(code) == true;

        /// <summary>
        /// Returns true when two stacks have the same collectible code.
        /// </summary>
        public static bool IsSameCollectible(this ItemStack? a, ItemStack? b)
            => a?.Collectible?.Code != null && a.Collectible.Code.Equals(b?.Collectible?.Code);

        /// <summary>
        /// Returns true for null or empty stacks.
        /// </summary>
        public static bool IsEmptyOrNull(this ItemStack? stack) => stack == null || stack.StackSize <= 0;
    }
}
