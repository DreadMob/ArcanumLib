using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Focused factory for <see cref="DamageSource" /> instances with the most common
    /// field combinations used across combat abilities, effects, and projectiles.
    /// </summary>
    public static class DamageHelper
    {
        /// <summary>Player-sourced damage (items/anchors).</summary>
        /// <param name="source">The source value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The player.</returns>
        public static DamageSource CreatePlayer(Entity source, EnumDamageType type, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Player,
                SourceEntity = source,
                Type = type,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Entity-sourced damage with the most common defaults.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, EnumDamageType type, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                Type = type,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Entity-sourced damage with a different cause entity (e.g. a projectile).</summary>
        /// <param name="source">The source value.</param>
        /// <param name="cause">The cause value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, Entity cause, EnumDamageType type, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                CauseEntity = cause,
                Type = type,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Entity-sourced damage with an explicit damage tier.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, EnumDamageType type, int damageTier, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                Type = type,
                DamageTier = damageTier,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Weather-sourced damage (e.g., random lightning, environmental hazards).</summary>
        /// <param name="sourcePos">The three-dimensional vector.</param>
        /// <param name="type">The type value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The weather.</returns>
        public static DamageSource CreateWeather(Vec3d sourcePos, EnumDamageType type, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Weather,
                SourcePos = sourcePos,
                Type = type,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Entity-sourced damage with cause and explicit damage tier.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="cause">The cause value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, Entity cause, EnumDamageType type, int damageTier, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                CauseEntity = cause,
                Type = type,
                DamageTier = damageTier,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Player-sourced damage with an explicit damage tier.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The player.</returns>
        public static DamageSource CreatePlayer(Entity source, EnumDamageType type, int damageTier, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Player,
                SourceEntity = source,
                Type = type,
                DamageTier = damageTier,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Player-sourced projectile/area damage with a cause entity.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="cause">The cause value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The player.</returns>
        public static DamageSource CreatePlayer(Entity source, Entity cause, EnumDamageType type, int damageTier, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Player,
                SourceEntity = source,
                CauseEntity = cause,
                Type = type,
                DamageTier = damageTier,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Player-sourced damage with explicit tier and custom knockback.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The player.</returns>
        public static DamageSource CreatePlayer(Entity source, EnumDamageType type, int damageTier, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Player,
                SourceEntity = source,
                Type = type,
                DamageTier = damageTier,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Player-sourced damage with custom knockback strength.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The player.</returns>
        public static DamageSource CreatePlayer(Entity source, EnumDamageType type, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Player,
                SourceEntity = source,
                Type = type,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Internal damage (self-damage, costs, etc.).</summary>
        /// <param name="type">The type value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The internal.</returns>
        public static DamageSource CreateInternal(EnumDamageType type, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = type,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Internal damage with tier and knockback control.</summary>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The internal.</returns>
        public static DamageSource CreateInternal(EnumDamageType type, int damageTier, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = type,
                DamageTier = damageTier,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Entity-sourced damage with explicit tier and custom knockback.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, EnumDamageType type, int damageTier, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                Type = type,
                DamageTier = damageTier,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Entity-sourced damage with custom knockback strength.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, EnumDamageType type, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                Type = type,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Entity-sourced damage with cause, tier and knockback.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="cause">The cause value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, Entity cause, EnumDamageType type, int damageTier, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                CauseEntity = cause,
                Type = type,
                DamageTier = damageTier,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Entity-sourced damage with both cause and knockback.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="cause">The cause value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="knockbackStrength">The knockback strength value.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, Entity cause, EnumDamageType type, float knockbackStrength, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                CauseEntity = cause,
                Type = type,
                KnockbackStrength = knockbackStrength,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Healing damage source (negative / heal).</summary>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The heal.</returns>
        public static DamageSource CreateHeal(bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Type = EnumDamageType.Heal,
                IgnoreInvFrames = ignoreInvFrames
            };
        }

        /// <summary>Projectile-style damage with tier, source and hit positions.</summary>
        /// <param name="source">The source value.</param>
        /// <param name="cause">The cause value.</param>
        /// <param name="type">The type value.</param>
        /// <param name="damageTier">The damage tier value.</param>
        /// <param name="sourcePos">The three-dimensional vector.</param>
        /// <param name="hitPosition">The three-dimensional vector.</param>
        /// <param name="ignoreInvFrames">The ignore inv frames value.</param>
        /// <returns>The value.</returns>
        public static DamageSource Create(Entity source, Entity cause, EnumDamageType type, int damageTier, Vec3d sourcePos, Vec3d hitPosition, bool ignoreInvFrames = true)
        {
            return new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = source,
                CauseEntity = cause,
                SourcePos = sourcePos,
                HitPosition = hitPosition,
                Type = type,
                DamageTier = damageTier,
                IgnoreInvFrames = ignoreInvFrames
            };
        }
    }
}
