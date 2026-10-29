#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace ArcanumLib.PlayerAnimations
{
    /// <summary>
    /// Client half of <see cref="PlayerAnimationSystem"/> on every player entity: injects the custom keyframes
    /// into the player's shape and replays animation commands other players sent through the server.
    /// </summary>
    public class EntityBehaviorPlayerAnimations : EntityBehavior
    {
        public const string BehaviorCode = "alegacyplayeranimations";

        private PlayerAnimationSystem system;
        private int lastSeq = -1;

        public EntityBehaviorPlayerAnimations(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => BehaviorCode;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            system = entity.Api.ModLoader.GetModSystem<PlayerAnimationSystem>();
            if (entity.Api.Side != EnumAppSide.Client || system == null) return;

            // PlayerAnimationManager looks "-fp"/"-ifp" variants up by meta code here.
            var byCode = properties.Client?.AnimationsByMetaCode;
            if (byCode != null)
                foreach (var kv in system.Metas)
                    if (!byCode.ContainsKey(kv.Key)) byCode[kv.Key] = kv.Value;

            // Commands that were already set before this client saw the entity are stale (a one-shot that ended long ago).
            lastSeq = entity.WatchedAttributes.GetTreeAttribute(PlayerAnimationSystem.SyncAttribute)?.GetInt("seq") ?? -1;
            entity.WatchedAttributes.RegisterModifiedListener(PlayerAnimationSystem.SyncAttribute, OnCommand);
        }

        private bool clearedWhileDead;

        public override void OnGameTick(float deltaTime)
        {
            if (entity.Api.Side != EnumAppSide.Client || system == null) return;
            if (entity.Alive) { clearedWhileDead = false; return; }
            if (clearedWhileDead) return;
            // Dying mid-cast must not leave the cast pose looping on the body or after respawn.
            clearedWhileDead = true;
            var anim = entity.AnimManager;
            if (anim == null) return;
            foreach (var code in system.Metas.Keys)
                if (anim.IsAnimationActive(code)) anim.StopAnimation(code);
        }

        private void OnCommand()
        {
            if (entity is not EntityPlayer player || system == null) return;

            var tree = entity.WatchedAttributes.GetTreeAttribute(PlayerAnimationSystem.SyncAttribute);
            if (tree == null) return;

            int seq = tree.GetInt("seq");
            if (seq == lastSeq) return;
            lastSeq = seq;

            // Client-initiated commands: the caster already played it locally the moment the button was pressed.
            if (!tree.GetBool("self") && (entity.Api as ICoreClientAPI)?.World?.Player?.Entity?.EntityId == entity.EntityId) return;

            system.Apply(player, tree.GetString("start"), (tree.GetString("stop") ?? "").Split(','));
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
            if (entity.Api.Side != EnumAppSide.Client || entityShape == null || system == null) return;

            var custom = system.Animations;
            if (custom.Length == 0) return;

            var existing = new HashSet<string>((entityShape.Animations ?? Array.Empty<Animation>()).Select(a => a?.Code).Where(c => c != null), StringComparer.OrdinalIgnoreCase);
            var missing = custom.Where(a => !existing.Contains(a.Code)).ToArray();
            if (missing.Length == 0) return;

            if (!shapeIsCloned)
            {
                entityShape = entityShape.Clone();
                shapeIsCloned = true;
            }

            // Keyframes are authored against the vanilla seraph (animation version 0); a shape using another
            // version would interpret the same numbers differently, so follow whatever the shape uses.
            int version = entityShape.Animations?.FirstOrDefault()?.Version ?? 0;

            // Custom models (PlayerModelLib) may lack some seraph bones; a keyframe for a missing element is left
            // unresolved and breaks the animator, so those entries are dropped for this shape only.
            var elementNames = new HashSet<string>(StringComparer.Ordinal);
            CollectElementNames(entityShape.Elements, elementNames);

            var injected = missing.Select(a =>
            {
                var c = a.Clone();
                c.Version = version;
                c.PrevNextKeyFrameByFrame = null;
                foreach (var kf in c.KeyFrames ?? Array.Empty<AnimationKeyFrame>())
                {
                    if (kf.Elements == null) continue;
                    foreach (var name in kf.Elements.Keys.Where(n => !elementNames.Contains(n)).ToList())
                        kf.Elements.Remove(name);
                }
                return c;
            });

            entityShape.Animations = (entityShape.Animations ?? Array.Empty<Animation>()).Concat(injected).ToArray();
        }

        private static void CollectElementNames(ShapeElement[] elements, HashSet<string> into)
        {
            if (elements == null) return;
            foreach (var e in elements)
            {
                if (e?.Name == null) continue;
                into.Add(e.Name);
                CollectElementNames(e.Children, into);
            }
        }
    }
}
