using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArcanumLib.Core;
using ArcanumLib.Persistence;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace ArcanumLib.PlayerTransfer
{
    /// <summary>A transfer whose entity-attribute half is still waiting for one of the accounts to join.</summary>
    public class PendingPlayerTransfer
    {
        public string FromUid { get; set; } = "";
        public string ToUid { get; set; } = "";
        public string FromName { get; set; } = "";
        public string ToName { get; set; } = "";
        public long CreatedMs { get; set; }

        /// <summary>The source's progress attributes, lifted off its entity (TreeAttribute bytes, base64).</summary>
        public string? Snapshot { get; set; }

        /// <summary>The source's registered WorldData mod-data blobs (key -> base64).</summary>
        public Dictionary<string, string> Moddata { get; set; } = new();
        public bool Extracted { get; set; }
    }

    public class PendingPlayerTransferData
    {
        public List<PendingPlayerTransfer> Pending { get; set; } = new();
    }

    /// <summary>
    /// Runs account transfers: every registered <see cref="IPlayerDataTransfer" /> immediately,
    /// server-side player data (CustomPlayerData) immediately, and the progress WatchedAttributes
    /// of the player entities when each account is online (source lifted, then target merged).
    /// </summary>
    public class PlayerTransferModSystem : ModSystem
    {
        private ICoreServerAPI? sapi;
        private IModDataStore<PendingPlayerTransferData>? store;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        // After ArcanumDataModSystem, which registers the ModDataStore registry.
        public override double ExecuteOrder() => 0.15;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            store = ModDataStore.GetOrCreate<PendingPlayerTransferData>(api, "arcanumlib", "playertransfer", 1);
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
            ArcanumServices.Register(this, ArcanumServiceScope.Server);
        }

        public override void Dispose()
        {
            if (sapi != null) sapi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
            ArcanumServices.Unregister<PlayerTransferModSystem>(ArcanumServiceScope.Server);
        }

        /// <summary>Pending entity-attribute halves (for /avq datatransfer status).</summary>
        public IReadOnlyList<PendingPlayerTransfer> Pending => store?.Data.Pending.ToList() ?? new List<PendingPlayerTransfer>();

        /// <summary>
        /// Moves all progress of <paramref name="fromUid" /> onto <paramref name="toUid" />.
        /// Returns the report; entity attributes may still be pending (see <see cref="Pending" />).
        /// </summary>
        public PlayerTransferContext Execute(string fromUid, string toUid, string fromName, string toName)
        {
            if (sapi == null) throw new InvalidOperationException("PlayerTransferModSystem is not started");
            if (string.IsNullOrWhiteSpace(fromUid) || string.IsNullOrWhiteSpace(toUid))
                throw new ArgumentException("both uids are required");
            if (string.Equals(fromUid, toUid, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("source and target are the same account");

            var ctx = new PlayerTransferContext(sapi, fromUid, toUid, fromName, toName);

            foreach (var participant in PlayerDataTransfer.All)
            {
                try { participant.Transfer(ctx); }
                catch (Exception ex)
                {
                    ctx.Failures.Add(participant.Id);
                    sapi.Logger.Error("[PlayerTransfer] participant '{0}' failed ({1} -> {2}): {3}", participant.Id, fromUid, toUid, ex);
                }
            }

            try { TransferCustomPlayerData(ctx); }
            catch (Exception ex)
            {
                ctx.Failures.Add("customplayerdata");
                sapi.Logger.Error("[PlayerTransfer] CustomPlayerData failed: {0}", ex);
            }

            var pending = new PendingPlayerTransfer
            {
                FromUid = fromUid, ToUid = toUid, FromName = fromName, ToName = toName,
                CreatedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            store!.Data.Pending.Add(pending);
            store.MarkDirty();

            ProcessOnline(pending);
            ctx.Note("entity", pending.Extracted
                ? (store.Data.Pending.Contains(pending) ? $"lifted from {fromName}, waiting for {toName} to join" : "merged")
                : $"waiting for {fromName} to join");

            store.Save();
            sapi.Logger.Notification("[PlayerTransfer] {0} ({1}) -> {2} ({3}): {4}", fromName, fromUid, toName, toUid, string.Join("; ", ctx.Report));
            return ctx;
        }

        // ------------------------------------------------------------------ CustomPlayerData

        private void TransferCustomPlayerData(PlayerTransferContext ctx)
        {
            var from = sapi!.PlayerData.GetPlayerDataByUid(ctx.FromUid);
            var to = sapi.PlayerData.GetPlayerDataByUid(ctx.ToUid);
            if (from?.CustomPlayerData == null || to?.CustomPlayerData == null) return;

            int moved = 0;
            foreach (var key in from.CustomPlayerData.Keys.ToList())
            {
                if (!PlayerDataTransfer.IsTransferredCustomDataKey(key)) continue;
                string value = from.CustomPlayerData[key];
                from.CustomPlayerData.Remove(key);

                var custom = PlayerDataTransfer.GetCustomDataMerger(key);
                if (custom != null && to.CustomPlayerData.TryGetValue(key, out var existingCustom) && !string.IsNullOrEmpty(existingCustom))
                {
                    to.CustomPlayerData[key] = custom(existingCustom, value);
                }
                else if (to.CustomPlayerData.TryGetValue(key, out var existing) &&
                    double.TryParse(existing, NumberStyles.Float, CultureInfo.InvariantCulture, out double a) &&
                    double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double b))
                {
                    double merged = PlayerDataMerge.Combine(a, b, PlayerDataMerge.Classify(key));
                    bool integral = !existing.Contains('.') && !value.Contains('.');
                    to.CustomPlayerData[key] = integral ? ((long)merged).ToString(CultureInfo.InvariantCulture) : merged.ToString(CultureInfo.InvariantCulture);
                }
                else if (!to.CustomPlayerData.ContainsKey(key) || string.IsNullOrEmpty(to.CustomPlayerData[key]))
                {
                    to.CustomPlayerData[key] = value;
                }
                moved++;
            }
            if (moved > 0) ctx.Note("customplayerdata", $"{moved} keys");
        }

        // ------------------------------------------------------------------ entity attributes

        private void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (store == null || player?.Entity == null) return;
            bool changed = false;
            foreach (var p in store.Data.Pending.ToList())
            {
                if (string.Equals(p.FromUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.ToUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase))
                {
                    ProcessOnline(p);
                    changed = true;
                }
            }
            if (changed) store.Save();
        }

        private void ProcessOnline(PendingPlayerTransfer p)
        {
            if (!p.Extracted && Online(p.FromUid) is { Entity: not null } src)
            {
                p.Snapshot = Convert.ToBase64String(LiftAttributes(src).ToBytes());
                foreach (var key in PlayerDataTransfer.WorldModdata)
                {
                    var blob = src.WorldData.GetModdata(key);
                    if (blob == null) continue;
                    p.Moddata[key] = Convert.ToBase64String(blob);
                    src.WorldData.RemoveModdata(key);
                }
                p.Extracted = true;
                store!.MarkDirty();
            }

            if (p.Extracted && Online(p.ToUid) is { Entity: not null } dst)
            {
                var snapshot = new TreeAttribute();
                if (!string.IsNullOrEmpty(p.Snapshot)) snapshot.FromBytes(Convert.FromBase64String(p.Snapshot));
                MergeAttributes(dst, snapshot);
                foreach (var kv in p.Moddata ?? new Dictionary<string, string>())
                    if (dst.WorldData.GetModdata(kv.Key) == null)
                        dst.WorldData.SetModdata(kv.Key, Convert.FromBase64String(kv.Value));
                store!.Data.Pending.Remove(p);
                store.MarkDirty();
                sapi!.Logger.Notification("[PlayerTransfer] entity progress of {0} merged into {1}", p.FromName, p.ToName);
            }
        }

        private IServerPlayer? Online(string uid)
            => sapi!.World.AllOnlinePlayers.FirstOrDefault(pl => string.Equals(pl.PlayerUID, uid, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

        /// <summary>Copies the progress attributes into a snapshot and removes them from the entity.</summary>
        public static TreeAttribute LiftAttributes(IServerPlayer player)
        {
            var snapshot = new TreeAttribute();
            var wa = player.Entity.WatchedAttributes;
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var selector in PlayerDataTransfer.Selectors)
            {
                try { foreach (var k in selector(wa) ?? Enumerable.Empty<string>()) if (!string.IsNullOrEmpty(k)) selected.Add(k); }
                catch (Exception ex) { player.Entity.Api.Logger.Error("[PlayerTransfer] key selector failed: {0}", ex); }
            }
            foreach (var kv in wa.ToList())
            {
                if (!PlayerDataTransfer.IsTransferredKey(kv.Key) && !selected.Contains(kv.Key)) continue;
                snapshot[kv.Key] = kv.Value.Clone();
                wa.RemoveAttribute(kv.Key);
            }
            wa.MarkAllDirty();
            return snapshot;
        }

        /// <summary>Merges a snapshot into the player's attributes and runs the after-merge hooks.</summary>
        public static void MergeAttributes(IServerPlayer player, ITreeAttribute snapshot)
        {
            var wa = player.Entity.WatchedAttributes;
            foreach (var kv in snapshot.ToList())
            {
                if (PlayerDataTransfer.IsFillOnlyKey(kv.Key))
                {
                    if (!wa.HasAttribute(kv.Key)) wa[kv.Key] = kv.Value.Clone();
                    continue;
                }
                var existing = wa[kv.Key];
                var merged = PlayerDataMerge.MergeAttribute(kv.Key, existing, kv.Value);
                if (merged != null && !ReferenceEquals(merged, existing)) wa[kv.Key] = merged;
            }
            wa.MarkAllDirty();
            foreach (var hook in PlayerDataTransfer.AfterMergeHooks)
            {
                try { hook(player); }
                catch (Exception ex) { player.Entity.Api.Logger.Error("[PlayerTransfer] after-merge hook failed: {0}", ex); }
            }
        }
    }
}
