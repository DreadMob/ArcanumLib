using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace ArcanumLib.Inventory;

/// <summary>
/// Tracks inventory changes for a player and reports whether a dependent
/// recomputation should run. Fingerprinting is throttled and cached to avoid
/// expensive work on every tick.
/// </summary>
public class InventoryChangeTracker : IDisposable
{
    private readonly ICoreAPI _api;
    private readonly string _inventoryCode;
    private readonly int _checkIntervalMs;
    private readonly System.Func<ItemStack, int> _stackHash;
    private readonly System.Predicate<ItemSlot> _slotFilter;

    private readonly Dictionary<long, Fingerprint> _lastFingerprints = new();
    private readonly Dictionary<long, long> _lastCheckTimes = new();
    private readonly object _syncLock = new();

    private record Fingerprint(int Hash, int Count);

    /// <summary>
    /// Creates a tracker for the given inventory.
    /// </summary>
    /// <param name="api">An API instance for time and logging access.</param>
    /// <param name="inventoryCode">Inventory class to watch, e.g. "character".</param>
    /// <param name="checkIntervalMs">Minimum time between checks for one player.</param>
    /// <param name="stackHash">
    /// Optional hash for an <see cref="ItemStack" />. Defaults to
    /// <see cref="InventoryFingerprint.GetStableStackHash" />.
    /// </param>
    /// <param name="slotFilter">
    /// Optional predicate for which slots to include. Defaults to wearable items.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="api" /> is <see langword="null" />.</exception>
    public InventoryChangeTracker(
        ICoreAPI api,
        string inventoryCode = "character",
        int checkIntervalMs = 500,
        System.Func<ItemStack, int>? stackHash = null,
        System.Predicate<ItemSlot>? slotFilter = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _inventoryCode = inventoryCode ?? throw new ArgumentNullException(nameof(inventoryCode));
        _checkIntervalMs = checkIntervalMs;
        _stackHash = stackHash ?? InventoryFingerprint.GetStableStackHash;
        _slotFilter = slotFilter ?? IsWearableSlot;

        if (_api is ICoreServerAPI sapi)
        {
            sapi.Event.PlayerDisconnect += OnPlayerDisconnect;
        }
    }

    /// <summary>
    /// Returns true if the player's inventory has changed since the last check.
    /// Call before an expensive recalculation and skip the work when this returns false.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>true if the operation should recalculate; otherwise, false.</returns>
    public bool ShouldRecalculate(EntityPlayer player)
    {
        if (player?.Player?.InventoryManager == null) return false;

        long entityId = player.EntityId;
        long now = _api.World.ElapsedMilliseconds;

        var inv = player.Player.InventoryManager.GetOwnInventory(_inventoryCode);
        if (inv == null) return false;

        var current = BuildFingerprint(inv);

        lock (_syncLock)
        {
            // Throttle per-player checks.
            if (_lastCheckTimes.TryGetValue(entityId, out long lastCheck))
            {
                if ((now - lastCheck) < _checkIntervalMs)
                {
                    return false;
                }
            }
            _lastCheckTimes[entityId] = now;

            if (_lastFingerprints.TryGetValue(entityId, out var last))
            {
                if (last.Equals(current))
                {
                    return false;
                }
            }

            _lastFingerprints[entityId] = current;
            return true;
        }
    }

    /// <summary>
    /// Forces a recalculation on the next <see cref="ShouldRecalculate" /> call
    /// for the given entity by clearing its cached fingerprint.
    /// </summary>
    /// <param name="entityId">The entity id value.</param>
    public void Invalidate(long entityId)
    {
        lock (_syncLock)
        {
            _lastFingerprints.Remove(entityId);
            _lastCheckTimes.Remove(entityId);
        }
    }

    /// <summary>
    /// Clears all cached fingerprints and throttle timestamps.
    /// </summary>
    public void Clear()
    {
        lock (_syncLock)
        {
            _lastFingerprints.Clear();
            _lastCheckTimes.Clear();
        }
    }

    /// <summary>
    /// Releases the player disconnect handler. Call on world unload.
    /// </summary>
    public void Dispose()
    {
        if (_api is ICoreServerAPI sapi)
        {
            sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
        }
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        if (player?.Entity?.EntityId != null)
        {
            Invalidate(player.Entity.EntityId);
        }
    }

    private Fingerprint BuildFingerprint(IInventory inv)
    {
        int hash = 17;
        int count = 0;

        foreach (ItemSlot slot in inv)
        {
            if (slot?.Empty != false) continue;
            if (!_slotFilter(slot)) continue;

            var stack = slot.Itemstack;
            if (stack?.Collectible == null) continue;

            hash = hash * 31 + _stackHash(stack!);
            count++;
        }

        return new Fingerprint(hash, count);
    }

    private static bool IsWearableSlot(ItemSlot slot)
    {
        return slot?.Itemstack?.Collectible?.GetCollectibleInterface<IWearable>() != null;
    }
}
