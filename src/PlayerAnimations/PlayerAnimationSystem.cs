#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace ArcanumLib.PlayerAnimations
{
    /// <summary>One file in <c>config/playeranimations/</c>: a list of player animations.</summary>
    public class PlayerAnimationFile
    {
        public List<PlayerAnimationDef> animations { get; set; } = new List<PlayerAnimationDef>();
    }

    /// <summary>
    /// A custom player animation: <see cref="meta"/> is what player.json would hold (blend modes, weights, ease speeds),
    /// <see cref="animation"/> is what the seraph shape would hold (keyframes). An entry whose code ends with "-fp"
    /// is the first-person variant of the entry without it and is picked by the game automatically.
    /// </summary>
    public class PlayerAnimationDef
    {
        public AnimationMetaData meta { get; set; }
        public Animation animation { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PlayerAnimationPacket
    {
        /// <summary>Animation code to start ("" = none).</summary>
        public string Start = "";

        /// <summary>Comma-separated animation codes to stop before starting.</summary>
        public string Stop = "";
    }

    /// <summary>
    /// Custom player animations without patching the seraph shape: every mod/pack can drop
    /// <c>config/playeranimations/*.json</c>, <see cref="EntityBehaviorPlayerAnimations"/> injects the keyframes
    /// into whatever shape the player uses (vanilla seraph, PlayerModelLib models) on tesselation.
    /// Sync: the casting client plays the animation at once and tells the server, the server stores the command
    /// in a watched attribute, every other client replays it. The server itself never animates.
    /// </summary>
    public class PlayerAnimationSystem : ModSystem
    {
        public const string SyncAttribute = "alegacyvsquest:playeranim";
        private const string ChannelName = "alegacyvsquest-playeranim";

        private readonly Dictionary<string, AnimationMetaData> metas = new Dictionary<string, AnimationMetaData>(StringComparer.OrdinalIgnoreCase);
        private Animation[] animations = Array.Empty<Animation>();
        private IClientNetworkChannel clientChannel;
        private ICoreAPI api;

        public IReadOnlyDictionary<string, AnimationMetaData> Metas => metas;
        public Animation[] Animations => animations;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.RegisterEntityBehaviorClass(EntityBehaviorPlayerAnimations.BehaviorCode, typeof(EntityBehaviorPlayerAnimations));
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            metas.Clear();
            var anims = new List<Animation>();

            // All asset domains, not per mod id: a mod's asset folder may differ from its modid (alegacy_patches / alegacypatches).
            foreach (var asset in api.Assets.GetMany<PlayerAnimationFile>(api.Logger, "config/playeranimations"))
            {
                foreach (var def in asset.Value?.animations ?? new List<PlayerAnimationDef>())
                {
                    if (def?.meta == null || def.animation == null || string.IsNullOrWhiteSpace(def.meta.Code))
                    {
                        api.Logger.Warning("[PlayerAnimations] {0}: entry without meta.code or animation, skipped", asset.Key);
                        continue;
                    }

                    // The animator matches meta.Animation against animation.Code — keep them in sync so a typo cannot silently do nothing.
                    def.animation.Code = def.meta.Animation;
                    def.animation.CodeCrc32 = AnimationMetaData.GetCrc32(def.animation.Code);
                    if (metas.ContainsKey(def.meta.Code))
                        api.Logger.Warning("[PlayerAnimations] {0}: animation '{1}' defined twice, the later one wins", asset.Key, def.meta.Code);

                    metas[def.meta.Code] = def.meta.Init();
                    anims.RemoveAll(a => string.Equals(a.Code, def.animation.Code, StringComparison.OrdinalIgnoreCase));
                    anims.Add(def.animation);
                }
            }

            animations = anims.ToArray();
            if (metas.Count > 0) api.Logger.Notification("[PlayerAnimations] loaded {0} player animations", metas.Count);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            sapi.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<PlayerAnimationPacket>()
                .SetMessageHandler<PlayerAnimationPacket>(OnClientPacket);
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            clientChannel = capi.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<PlayerAnimationPacket>();
        }

        private void OnClientPacket(IServerPlayer fromPlayer, PlayerAnimationPacket packet)
        {
            if (fromPlayer?.Entity == null || packet == null) return;

            // Only known codes are relayed: a client cannot make others play arbitrary vanilla animations.
            string start = Known(packet.Start) ? packet.Start : "";
            string stop = string.Join(",", (packet.Stop ?? "").Split(',').Where(Known));
            SetServer(fromPlayer.Entity, start, stop, includeSelf: false);   // the sender already plays it locally
        }

        private bool Known(string code) => !string.IsNullOrWhiteSpace(code) && metas.ContainsKey(code);

        /// <summary>
        /// Server: make clients start/stop animations on this player (server-driven actions: teleport charge, scripted scenes).
        /// <paramref name="includeSelf"/> = false when the player's own client already plays it locally.
        /// </summary>
        public static void SetServer(EntityPlayer player, string start, string stop, bool includeSelf = true)
        {
            if (player?.WatchedAttributes == null) return;
            var tree = player.WatchedAttributes.GetOrAddTreeAttribute(SyncAttribute);
            tree.SetString("start", start ?? "");
            tree.SetString("stop", stop ?? "");
            tree.SetBool("self", includeSelf);
            tree.SetInt("seq", tree.GetInt("seq") + 1);   // the same code twice in a row must still re-trigger
            player.WatchedAttributes.MarkPathDirty(SyncAttribute);
        }

        /// <summary>Client: play on the local player right away and relay to everyone else.</summary>
        public void PlayLocal(string start, params string[] stop)
        {
            var player = (api as ICoreClientAPI)?.World?.Player?.Entity;
            if (player == null) return;

            Apply(player, start, stop);
            clientChannel?.SendPacket(new PlayerAnimationPacket { Start = start ?? "", Stop = string.Join(",", stop ?? Array.Empty<string>()) });
        }

        public void Apply(EntityPlayer player, string start, IEnumerable<string> stop)
        {
            var anim = player?.AnimManager;
            if (anim == null) return;

            foreach (var code in stop ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(code)) anim.StopAnimation(code);

            if (!string.IsNullOrWhiteSpace(start) && metas.TryGetValue(start, out var meta))
            {
                anim.StopAnimation(start);   // restart one-shots from frame 0
                anim.StartAnimation(meta.Clone());
            }
        }
    }
}
