using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ArcanumLib.Blocks;

/// <summary>
/// One-per-stack items (patterns, blueprints, diapositives) piled on the ground: each put-down lies on the previous one
/// instead of four per block. Vanilla <c>Stacking</c> needs one merged stack, but every such item is its own stack
/// (max 1), so the pile is a <c>SingleCenter</c> ground storage given more slots.
/// <para>Opt-in per item via attribute <c>groundPile: { group, capacity, step, rows, fan, openOnDisplay }</c>:
/// <c>step</c> = height of one layer in blocks; only items of one <c>group</c> share a pile (no group = the shared
/// one); <c>fan</c> turns each layer a little; round items give <c>rows: [4, 3, 1]</c> and lie side by side like a
/// log pile, each row in the grooves of the one below (step = roll diameter). An item with a <c>displayedShape</c>
/// lies with its own shape while alone and with the displayed one from the second on; with <c>openOnDisplay</c> it
/// keeps its own shape on shelves and in display cases too.</para>
/// </summary>
public static class GroundPiles
{
    public const string AttrKey = "groundPile";
    private const string HarmonyId = "arcanumlib.groundpiles";

    /// <summary>Slots a pile grows to; every other ground storage keeps vanilla's 4.</summary>
    public const int MaxSlots = 12;
    private const int VanillaSlots = 4;
    /// <summary>The pit kiln copies the ground storage slot by slot into its own 10.</summary>
    private const int KilnSlots = 10;

    private static Harmony? _harmony;
    private static int _users;

    private static readonly AccessTools.FieldRef<BlockEntityGroundStorage, Cuboidf[]> ColBoxes =
        AccessTools.FieldRefAccess<BlockEntityGroundStorage, Cuboidf[]>("colBoxes");
    private static readonly AccessTools.FieldRef<BlockEntityGroundStorage, Cuboidf[]> SelBoxes =
        AccessTools.FieldRefAccess<BlockEntityGroundStorage, Cuboidf[]>("selBoxes");
    private static readonly AccessTools.FieldRef<InventoryGeneric, ItemSlot[]> InvSlots =
        AccessTools.FieldRefAccess<InventoryGeneric, ItemSlot[]>("slots");

    // Small fixed sideways shifts so a flat pile reads as hand-stacked sheets, not one block.
    private static readonly float[] JitterX = { 0f, 0.012f, -0.008f, 0.016f, -0.014f, 0.006f, -0.004f, 0.014f, -0.012f, 0.008f, -0.016f, 0.004f };
    private static readonly float[] JitterZ = { 0f, -0.010f, 0.012f, 0.004f, -0.006f, 0.014f, -0.012f, -0.002f, 0.010f, -0.014f, 0.006f, -0.008f };
    /// <summary>Per-layer turn (degrees) of a <c>fan</c> pile.</summary>
    public static readonly float[] FanYaw = { 0f, 14f, -10f, 22f, -18f, 6f, 26f, -4f, -14f, 10f, -22f, 18f };
    // Log pile: rolls pushed in and out along their length, every other one turned end for end,
    // so the ribbons do not line up into one band.
    private static readonly float[] RollShift = { 0f, 0.09f, -0.06f, 0.04f, -0.1f, 0.07f, -0.03f, 0.05f };
    public static readonly bool[] RollFlip = { false, true, true, false, true, false, false, true };

    /// <summary>Patches once per process; each side (singleplayer runs both) holds a use until its dispose.</summary>
    internal static void Apply()
    {
        if (_users++ > 0) return;
        _harmony = new Harmony(HarmonyId);
        var t = typeof(BlockEntityGroundStorage);
        var self = typeof(GroundPiles);
        HarmonyMethod Hook(string name) => new(AccessTools.Method(self, name));
        _harmony.Patch(AccessTools.Method(t, nameof(BlockEntityGroundStorage.FromTreeAttributes)), prefix: Hook(nameof(LoadPrefix)), postfix: Hook(nameof(LoadPostfix)));
        _harmony.Patch(AccessTools.PropertyGetter(t, nameof(BlockEntityGroundStorage.Capacity)), postfix: Hook(nameof(CapacityPostfix)));
        _harmony.Patch(AccessTools.PropertyGetter(t, nameof(BlockEntityGroundStorage.DisplayedItems)), postfix: Hook(nameof(DisplayedPostfix)));
        _harmony.Patch(AccessTools.Method(t, nameof(BlockEntityGroundStorage.GetLayoutOffset)), postfix: Hook(nameof(LayoutPostfix)));
        _harmony.Patch(AccessTools.Method(t, "genTransformationMatrices"), postfix: Hook(nameof(MatricesPostfix)));
        _harmony.Patch(AccessTools.Method(t, nameof(BlockEntityGroundStorage.putOrGetItemSingle)), prefix: Hook(nameof(PutOrGetPrefix)));
        _harmony.Patch(AccessTools.Method(t, "regenCollisionSelectionBox"), postfix: Hook(nameof(BoxesPostfix)));
        _harmony.Patch(AccessTools.Method(t, "UpdateLegacyStorageLayouts"), prefix: Hook(nameof(LegacyLayoutsPrefix)));
        // Ground storage overrides both since 1.22, so the display's own getOrCreateMesh is hooked as well: shelves,
        // cases and mod displays go through it. Older games resolve both lookups to the same method.
        _harmony.Patch(AccessTools.Method(t, "getMeshCacheKey"), postfix: Hook(nameof(MeshKeyPostfix)));
        var groundMesh = AccessTools.Method(t, "getOrCreateMesh");
        var displayMesh = AccessTools.DeclaredMethod(typeof(BlockEntityDisplay), "getOrCreateMesh");
        _harmony.Patch(groundMesh, prefix: Hook(nameof(MeshPrefix)));
        if (displayMesh != groundMesh) _harmony.Patch(displayMesh, prefix: Hook(nameof(MeshPrefix)));
        _harmony.Patch(AccessTools.Method(typeof(BlockPitkiln), nameof(BlockPitkiln.TryCreateKiln)), prefix: Hook(nameof(PitKilnPrefix)));
    }

    internal static void Dispose()
    {
        if (_users == 0 || --_users > 0) return;
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
    }

    public static JsonObject? PileOf(ItemStack? stack)
    {
        var pile = stack?.Collectible?.Attributes?[AttrKey];
        return pile != null && pile.Exists ? pile : null;
    }

    private static JsonObject? PileOf(BlockEntityGroundStorage be) => PileOf(be.Inventory.FirstNonEmptySlot?.Itemstack);

    private static string GroupOf(JsonObject pile) => pile["group"].AsString("");

    private static float StepOf(JsonObject pile) => pile["step"].AsFloat(0.0625f);

    private static int CapacityOf(BlockEntityGroundStorage be, JsonObject pile) =>
        Math.Clamp(pile["capacity"].AsInt(MaxSlots), 1, Math.Min(MaxSlots, be.Inventory.Count));

    private static int Filled(BlockEntityGroundStorage be)
    {
        int n = 0;
        foreach (var slot in be.Inventory) if (!slot.Empty) n++;
        return n;
    }

    /// <summary>
    /// Grows a plain ground storage to <paramref name="n"/> slots. Vanilla sizes the inventory once in the
    /// constructor and never resizes it on load, so piles grow on demand: when an item is put down and
    /// before a saved/synced pile is read (else slots past 4 would be dropped).
    /// </summary>
    private static void Grow(BlockEntityGroundStorage be, int n)
    {
        if (be.GetType() != typeof(BlockEntityGroundStorage) || be.Inventory is not InventoryGeneric inv) return;
        n = Math.Min(n, MaxSlots);
        if (inv.Count >= n) return;
        int from = inv.Count;
        inv.AddSlots(n - from);
        for (int i = from; i < n; i++) inv[i].StorageType |= EnumItemStorageFlags.Backpack;
        if (be.MeshRefs.Length < n) Array.Resize(ref be.MeshRefs, n);
        if (be.ModelTransformsRenderer.Length < n) Array.Resize(ref be.ModelTransformsRenderer, n);

        // Vanilla's ground-stored bag behaviour keeps one workspace per slot, sized once in its ctor;
        // its SlotModified handler indexes that array, so it has to grow with the inventory.
        if (be.GetBehavior<BEBehaviorContainedBagInventory>() is { } bags && bags.BagInventories.Length < n)
        {
            var ws = bags.BagInventories;
            Array.Resize(ref ws, n);
            if (be.Api != null)
                for (int i = from; i < n; i++) ws[i] ??= new BlockEntityContainedBagWorkspace(be, i);
            SetBagInventories(bags, ws);
        }
    }

    private static readonly Action<BEBehaviorContainedBagInventory, BlockEntityContainedBagWorkspace[]> SetBagInventories =
        AccessTools.MethodDelegate<Action<BEBehaviorContainedBagInventory, BlockEntityContainedBagWorkspace[]>>(
            AccessTools.PropertySetter(typeof(BEBehaviorContainedBagInventory), nameof(BEBehaviorContainedBagInventory.BagInventories)));

    public static void LoadPrefix(BlockEntityGroundStorage __instance, ITreeAttribute tree) =>
        Grow(__instance, tree.GetTreeAttribute("inventory")?.GetInt("qslots") ?? 0);

    /// <summary>A grown storage that no longer holds a pile drops back to vanilla's 4 slots.</summary>
    public static void LoadPostfix(BlockEntityGroundStorage __instance)
    {
        if (__instance.GetType() != typeof(BlockEntityGroundStorage) || __instance.Inventory is not InventoryGeneric inv
            || inv.Count <= VanillaSlots || PileOf(__instance) != null) return;
        for (int i = VanillaSlots; i < inv.Count; i++)
            if (!inv[i].Empty) return;
        InvSlots(inv) = InvSlots(inv)[..VanillaSlots];
    }

    /// <summary>
    /// A grown storage would overflow the pit kiln's 10 slots. Drop the empty extra slots first (vanilla count, or up
    /// to the last filled one); a storage with something past the kiln's slots can't become a kiln at all.
    /// </summary>
    public static bool PitKilnPrefix(IWorldAccessor world, BlockPos pos, ref bool __result)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityGroundStorage be
            || be.GetType() != typeof(BlockEntityGroundStorage)
            || be.Inventory is not InventoryGeneric inv
            || inv.Count <= VanillaSlots) return true;

        int used = VanillaSlots;
        for (int i = inv.Count - 1; i >= VanillaSlots; i--)
            if (!inv[i].Empty) { used = i + 1; break; }
        if (used > KilnSlots) { __result = false; return false; }
        if (used < inv.Count) InvSlots(inv) = InvSlots(inv)[..used];
        return true;
    }

    public static void CapacityPostfix(BlockEntityGroundStorage __instance, ref int __result)
    {
        if (PileOf(__instance) is { } pile) __result = CapacityOf(__instance, pile);
    }

    public static void DisplayedPostfix(BlockEntityGroundStorage __instance, ref int __result)
    {
        if (__result > 0 && PileOf(__instance) is { } pile) __result = CapacityOf(__instance, pile);
    }

    public static int[]? RowsOf(JsonObject pile) => pile["rows"].Exists ? pile["rows"].AsArray<int>() : null;

    /// <summary>
    /// Offset of slot <paramref name="i"/> in a log pile: row r starts half a roll further in than row r-1, so its
    /// rolls sit in the grooves below (rows above may hold fewer and stay centred on the free grooves).
    /// </summary>
    public static Vec3f RowOffset(int[] rows, float d, int i)
    {
        int row = 0, k = i;
        while (row < rows.Length - 1 && k >= rows[row]) { k -= rows[row]; row++; }
        int grooves = rows[0] - row;
        int start = Math.Max(0, (grooves - rows[row]) / 2);
        float z = (-(rows[0] - 1) / 2f + row / 2f + start + k) * d;
        float y = row * d * MathF.Sqrt(3f) / 2f;
        return new Vec3f(RollShift[i % RollShift.Length], y, z);
    }

    /// <summary>Height of the top of a pile holding <paramref name="filled"/> items, in blocks.</summary>
    public static float PileHeight(JsonObject pile, int filled)
    {
        float step = StepOf(pile);
        if (filled <= 0) return 0f;
        return RowsOf(pile) is { } rows ? RowOffset(rows, step, filled - 1).Y + step : filled * step;
    }

    public static void LayoutPostfix(BlockEntityGroundStorage __instance, Vec3f[] offs)
    {
        if (PileOf(__instance) is not { } pile) return;
        float step = StepOf(pile);
        var rows = RowsOf(pile);
        bool lone = Filled(__instance) < 2;   // a lone roll lies open in the middle
        for (int i = 0; i < offs.Length; i++)
            offs[i] = rows == null ? new Vec3f(JitterX[i % JitterX.Length], i * step, JitterZ[i % JitterZ.Length])
                : lone ? new Vec3f() : RowOffset(rows, step, i);
    }

    /// <summary>Vanilla turns every slot by the storage's MeshAngle only; a <c>fan</c> pile adds its turn per layer,
    /// a log pile turns every other roll end for end (they stay parallel, else they would cross).</summary>
    public static void MatricesPostfix(BlockEntityGroundStorage __instance, float[][] __result)
    {
        if (PileOf(__instance) is not { } pile || Filled(__instance) < 2) return;
        bool rows = RowsOf(pile) != null;
        if (!rows && !pile["fan"].AsBool(false)) return;
        var offs = new Vec3f[__result.Length];
        __instance.GetLayoutOffset(offs);
        for (int i = 0; i < __result.Length; i++)
        {
            var o = new Matrixf().RotateY(__instance.MeshAngle).TransformVector((offs[i] ?? new Vec3f()).ToVec4f(0f)).XYZ;
            float yaw = __instance.MeshAngle + (!rows ? FanYaw[i % FanYaw.Length] * GameMath.DEG2RAD
                : RollFlip[i % RollFlip.Length] ? GameMath.PI : 0f);
            __result[i] = new Matrixf().Translate(o.X + 0.5f, o.Y, o.Z + 0.5f).RotateY(yaw).Translate(-0.5f, 0f, -0.5f).Values;
        }
    }

    /// <summary>Putting adds on top of the pile, taking removes the top item; only the pile's own group joins it.</summary>
    public static bool PutOrGetPrefix(BlockEntityGroundStorage __instance, ref ItemSlot ourSlot, IPlayer player, ref bool __result)
    {
        var inv = __instance.Inventory;
        var held = player.InventoryManager.ActiveHotbarSlot;
        var own = PileOf(__instance);
        var pile = own ?? (inv.Empty ? PileOf(held.Itemstack) : null);
        if (pile == null) return true;

        bool putting = !held.Empty && player.Entity.Controls.ShiftKey;
        if (putting)
        {
            var heldPile = PileOf(held.Itemstack);
            if (heldPile == null || (own != null && GroupOf(own) != GroupOf(heldPile)))
            { __result = false; return false; }
            Grow(__instance, pile["capacity"].AsInt(MaxSlots));
            int cap = CapacityOf(__instance, pile);
            for (int i = 0; i < cap; i++)
                if (inv[i].Empty) { ourSlot = inv[i]; return true; }
            __result = false;
            return false;
        }
        for (int i = inv.Count - 1; i >= 0; i--)
            if (!inv[i].Empty) { ourSlot = inv[i]; return true; }
        return true;
    }

    private static readonly MethodInfo GetDefaultMesh = AccessTools.Method(typeof(BlockEntityDisplay), "getDefaultMesh");
    private static readonly MethodInfo ApplyTransforms = AccessTools.Method(typeof(BlockEntityDisplay), "applyDefaultTranforms");
    private static readonly MethodInfo MeshKey = AccessTools.Method(typeof(BlockEntityGroundStorage), "getMeshCacheKey");
    private static readonly MethodInfo MeshCacheGetter = AccessTools.PropertyGetter(typeof(BlockEntityDisplay), "MeshCache");

    private static bool HasDisplayedShape(ItemStack stack) => stack.Collectible.Attributes["displayedShape"].Exists;

    /// <summary>A lone item with a displayedShape lies with its own shape; two or more use the displayed one.</summary>
    private static bool IsLoneOpen(BlockEntityGroundStorage be, ItemSlot slot) =>
        !slot.Empty && PileOf(slot.Itemstack) != null && HasDisplayedShape(slot.Itemstack) && Filled(be) == 1;

    /// <summary>On a shelf, in a case or any other display an <c>openOnDisplay</c> item is its own shape with its own
    /// transform: vanilla would apply that transform to the displayedShape mesh, which is cached once for every pile
    /// and every display, so it would keep shrinking.</summary>
    private static bool IsOpenOnDisplay(BlockEntityDisplay display, ItemSlot slot) =>
        display is not BlockEntityGroundStorage && !slot.Empty
        && PileOf(slot.Itemstack) is { } pile && pile["openOnDisplay"].AsBool(false);

    // MeshPrefix sits on both the ground storage override and BlockEntityDisplay's own method (which the override
    // calls as base): take the base type and filter.
    public static void MeshKeyPostfix(BlockEntityDisplay __instance, ItemSlot slot, ref string __result)
    {
        if (__instance is BlockEntityGroundStorage gs && IsLoneOpen(gs, slot)) __result = "pileopen-" + __result;
    }

    public static bool MeshPrefix(BlockEntityDisplay __instance, ItemSlot slot, ref MeshData __result)
    {
        bool onDisplay = IsOpenOnDisplay(__instance, slot);
        if (!onDisplay && (__instance is not BlockEntityGroundStorage gs || !IsLoneOpen(gs, slot))) return true;
        var cache = (Dictionary<string, MeshData>)MeshCacheGetter.Invoke(__instance, null)!;
        string key = (string)MeshKey.Invoke(__instance, new object[] { slot })!;   // same key OnTesselation looks up
        if (!cache.TryGetValue(key, out var mesh))
        {
            mesh = (MeshData)GetDefaultMesh.Invoke(__instance, new object?[] { slot.Itemstack })!;
            if (onDisplay) ApplyTransforms.Invoke(__instance, new object[] { slot.Itemstack!, mesh });
            cache[key] = mesh;
        }
        __result = mesh;
        return false;
    }

    /// <summary>Vanilla "migrates" a SingleCenter storage holding 2+ items to Quadrants, which then refuses the 3rd item.</summary>
    public static bool LegacyLayoutsPrefix(BlockEntityGroundStorage __instance, ref EnumGroundStorageLayout? ___overrideLayout)
    {
        if (PileOf(__instance) == null) return true;
        ___overrideLayout = null;
        return false;
    }

    public static void BoxesPostfix(BlockEntityGroundStorage __instance)
    {
        if (PileOf(__instance) is not { } pile) return;
        float top = Math.Max(0.0625f, PileHeight(pile, Filled(__instance)));
        var col = ColBoxes(__instance);
        var sel = SelBoxes(__instance);
        if (col?.Length > 0 && col[0] != null) { col[0] = col[0].Clone(); col[0].Y2 = top; }
        if (sel?.Length > 0 && sel[0] != null) { sel[0] = sel[0].Clone(); sel[0].Y2 = top; }
    }
}
