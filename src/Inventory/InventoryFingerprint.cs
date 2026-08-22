using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ArcanumLib.Inventory;

/// <summary>
/// Helpers for creating stable fingerprints of inventories and item stacks.
/// Useful for skipping expensive recomputation when nothing meaningful changed.
/// </summary>
public static class InventoryFingerprint
{
    /// <summary>
    /// Computes a stable hash of an item stack that attempts to ignore
    /// floating-point jitter. Includes collectible code, remaining durability,
    /// condition and attributes.
    /// </summary>
    /// <param name="stack">The stack to fingerprint. May be null.</param>
    /// <returns>A hash value; 0 if the stack is null or has no collectible.</returns>
    public static int GetStableStackHash(ItemStack stack)
    {
        if (stack?.Collectible == null) return 0;

        int hash = stack.Collectible.Code?.GetHashCode() ?? 0;
        hash = hash * 31 + stack.Collectible.GetRemainingDurability(stack);

        if (stack.Attributes?.HasAttribute("condition") == true)
        {
            float condition = stack.Attributes.GetFloat("condition", 1f);
            hash = hash * 31 + (int)Math.Round(condition * 100);
        }

        hash = hash * 31 + GetStableAttributeHash(stack.Attributes);

        return hash;
    }

    /// <summary>
    /// Computes a stable hash of an attribute tree, rounding floats and doubles
    /// to two decimal places to avoid jitter from values that change every tick.
    /// </summary>
    /// <param name="tree">The attribute tree. May be null.</param>
    /// <returns>A hash value; 0 if the tree is null.</returns>
    public static int GetStableAttributeHash(ITreeAttribute? tree)
    {
        if (tree == null) return 0;

        int hash = 17;
        foreach (var kv in tree)
        {
            if (kv.Value == null) continue;

            hash = hash * 31 + (kv.Key?.GetHashCode() ?? 0);
            hash = hash * 31 + GetStableAttributeHash(kv.Value);
        }

        return hash;
    }

    private static int GetStableAttributeHash(IAttribute attr)
    {
        if (attr is TreeAttribute tree)
        {
            return GetStableAttributeHash(tree);
        }

        if (attr is TreeArrayAttribute arr && arr.value != null)
        {
            int hash = 17;
            foreach (var sub in arr.value)
            {
                if (sub is ITreeAttribute subTree)
                {
                    hash = hash * 31 + GetStableAttributeHash(subTree);
                }
                else if (sub != null)
                {
                    hash = hash * 31 + GetBytesHash(sub);
                }
            }
            return hash;
        }

        if (attr is FloatAttribute f)
        {
            return (int)Math.Round(f.value * 100);
        }

        if (attr is DoubleAttribute d)
        {
            return (int)Math.Round(d.value * 100);
        }

        if (attr is IntAttribute i)
        {
            return i.value;
        }

        if (attr is LongAttribute l)
        {
            return l.value.GetHashCode();
        }

        if (attr is StringAttribute s)
        {
            return s.value?.GetHashCode() ?? 0;
        }

        if (attr is BoolAttribute b)
        {
            return b.value ? 1 : 0;
        }

        if (attr is ByteArrayAttribute ba && ba.value != null)
        {
            int hash = 17;
            foreach (byte by in ba.value)
            {
                hash = hash * 31 + by;
            }
            return hash;
        }

        // Fallback for any other attribute type.
        return GetBytesHash(attr);
    }

    private static int GetBytesHash(IAttribute attr)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
        {
            attr.ToBytes(writer);
        }

        byte[] bytes = ms.ToArray();
        int hash = 17;
        foreach (byte b in bytes)
        {
            hash = hash * 31 + b;
        }
        return hash;
    }
}
