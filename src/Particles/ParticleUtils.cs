using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Particles
{
    /// <summary>
    /// Utility class for creating and spawning particle effects.
    /// Provides preset particle configurations and a builder pattern for custom effects.
    /// </summary>
    public static class ParticleUtils
    {
        // ========================================
        // PRESET COLORS
        // ========================================

        /// <summary>Named RGBA color presets for common particle effects.</summary>
        public static class Colors
        {
            /// <summary>Gets the fire.</summary>
            public static int Fire => ColorUtil.ToRgba(255, 255, 120, 40);
            /// <summary>Gets the fire dark.</summary>
            public static int FireDark => ColorUtil.ToRgba(255, 180, 60, 20);
            /// <summary>Gets the poison.</summary>
            public static int Poison => ColorUtil.ToRgba(200, 50, 200, 50);
            /// <summary>Gets the poison green.</summary>
            public static int PoisonGreen => ColorUtil.ToRgba(255, 60, 200, 60);
            /// <summary>Gets the poison bright.</summary>
            public static int PoisonBright => ColorUtil.ToRgba(255, 80, 255, 60);
            /// <summary>Gets the ice.</summary>
            public static int Ice => ColorUtil.ToRgba(220, 180, 220, 255);
            /// <summary>Gets the ice bright.</summary>
            public static int IceBright => ColorUtil.ToRgba(255, 200, 240, 255);
            /// <summary>Gets the nile.</summary>
            public static int Nile => ColorUtil.ToRgba(220, 20, 140, 180);
            /// <summary>Gets the nile bright.</summary>
            public static int NileBright => ColorUtil.ToRgba(255, 120, 220, 255);
            /// <summary>Gets the nile foam.</summary>
            public static int NileFoam => ColorUtil.ToRgba(220, 200, 240, 255);
            /// <summary>Gets the shadow.</summary>
            public static int Shadow => ColorUtil.ToRgba(200, 40, 0, 60);
            /// <summary>Gets the shadow deep.</summary>
            public static int ShadowDeep => ColorUtil.ToRgba(180, 20, 0, 40);
            /// <summary>Gets the lightning.</summary>
            public static int Lightning => ColorUtil.ToRgba(255, 255, 255, 180);
            /// <summary>Gets the lightning blue.</summary>
            public static int LightningBlue => ColorUtil.ToRgba(255, 180, 200, 255);
            /// <summary>Gets the holy.</summary>
            public static int Holy => ColorUtil.ToRgba(255, 255, 240, 200);
            /// <summary>Gets the holy gold.</summary>
            public static int HolyGold => ColorUtil.ToRgba(255, 255, 200, 80);
            /// <summary>Gets the blood.</summary>
            public static int Blood => ColorUtil.ToRgba(220, 180, 20, 20);
            /// <summary>Gets the blood dark.</summary>
            public static int BloodDark => ColorUtil.ToRgba(200, 120, 10, 10);
            /// <summary>Gets the arcane.</summary>
            public static int Arcane => ColorUtil.ToRgba(200, 120, 50, 200);
            /// <summary>Gets the arcane bright.</summary>
            public static int ArcaneBright => ColorUtil.ToRgba(255, 160, 80, 255);
            /// <summary>Gets the smoke.</summary>
            public static int Smoke => ColorUtil.ToRgba(140, 60, 60, 60);
            /// <summary>Gets the smoke dark.</summary>
            public static int SmokeDark => ColorUtil.ToRgba(140, 30, 30, 30);
            /// <summary>Gets the void.</summary>
            public static int Void => ColorUtil.ToRgba(180, 10, 0, 20);
            /// <summary>Gets the nature.</summary>
            public static int Nature => ColorUtil.ToRgba(200, 60, 180, 40);
            /// <summary>Gets the nature bright.</summary>
            public static int NatureBright => ColorUtil.ToRgba(255, 100, 220, 80);
            /// <summary>Gets the chain.</summary>
            public static int Chain => ColorUtil.ToRgba(200, 150, 100, 200);
            /// <summary>Gets the shield.</summary>
            public static int Shield => ColorUtil.ToRgba(200, 100, 150, 220);
            /// <summary>Gets the shield gold.</summary>
            public static int ShieldGold => ColorUtil.ToRgba(220, 220, 180, 80);
            /// <summary>Gets the white.</summary>
            public static int White => ColorUtil.ToRgba(255, 255, 255, 255);
            /// <summary>Gets the black.</summary>
            public static int Black => ColorUtil.ToRgba(255, 10, 10, 10);

            // Themed color presets for common visual styles.
            // Mechanical / industrial — sparks, molten orange, smoke
            /// <summary>Gets the mecha spark.</summary>
            public static int MechaSpark => ColorUtil.ToRgba(255, 255, 200, 60);
            /// <summary>Gets the mecha orange.</summary>
            public static int MechaOrange => ColorUtil.ToRgba(240, 255, 140, 20);
            /// <summary>Gets the mecha smoke.</summary>
            public static int MechaSmoke => ColorUtil.ToRgba(160, 80, 70, 60);
            /// <summary>Gets the mecha core.</summary>
            public static int MechaCore => ColorUtil.ToRgba(255, 255, 80, 0);

            // Bone / skeletal — bone white, marrow green, primal rage
            /// <summary>Gets the bone white.</summary>
            public static int BoneWhite => ColorUtil.ToRgba(220, 230, 220, 200);
            /// <summary>Gets the bone marrow.</summary>
            public static int BoneMarrow => ColorUtil.ToRgba(200, 160, 200, 80);
            /// <summary>Gets the bone rage.</summary>
            public static int BoneRage => ColorUtil.ToRgba(240, 200, 40, 30);
            /// <summary>Gets the bone dust.</summary>
            public static int BoneDust => ColorUtil.ToRgba(150, 180, 170, 140);

            // Stone / crypt — stone grey, crypt purple, ancient dust
            /// <summary>Gets the stone grey.</summary>
            public static int StoneGrey => ColorUtil.ToRgba(180, 120, 110, 100);
            /// <summary>Gets the crypt purple.</summary>
            public static int CryptPurple => ColorUtil.ToRgba(200, 100, 40, 140);
            /// <summary>Gets the crypt deep.</summary>
            public static int CryptDeep => ColorUtil.ToRgba(180, 60, 20, 100);
            /// <summary>Gets the ancient dust.</summary>
            public static int AncientDust => ColorUtil.ToRgba(140, 140, 130, 110);

            // Toxic / miasma — toxic green, miasma purple, corruption
            /// <summary>Gets the toxic green.</summary>
            public static int ToxicGreen => ColorUtil.ToRgba(220, 40, 220, 30);
            /// <summary>Gets the miasma.</summary>
            public static int Miasma => ColorUtil.ToRgba(180, 80, 60, 120);
            /// <summary>Gets the miasma bright.</summary>
            public static int MiasmaBright => ColorUtil.ToRgba(220, 120, 100, 180);
            /// <summary>Gets the corruption.</summary>
            public static int Corruption => ColorUtil.ToRgba(200, 60, 30, 80);
        }

        // ========================================
        // BUILDER
        // ========================================

        /// <summary>Creates a new <see cref="ParticleEffectBuilder" />.</summary>
        /// <returns>The value.</returns>
        public static ParticleEffectBuilder Create() => new ParticleEffectBuilder();

        // ========================================
        // PRESET EFFECTS - EXPLOSIONS
        // ========================================

        /// <summary>Fire explosion with smoke and flash.</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="intensity">The intensity.</param>
        public static void SpawnFireExplosion(ICoreServerAPI sapi, Vec3d center, float radius, int intensity = 1)
        {
            if (sapi == null) return;

            int smokeMin = Math.Max(40, (int)(radius * 30f * intensity));
            int smokeMax = Math.Max(smokeMin + 20, (int)(radius * 50f * intensity));

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(smokeMin, smokeMax)
                .Color(Colors.SmokeDark)
                .Position(center.AddCopy(-radius * 0.5, -0.25, -radius * 0.5), center.AddCopy(radius * 0.5, radius * 0.4, radius * 0.5))
                .Velocity(new Vec3f(-0.6f, 0.05f, -0.6f), new Vec3f(0.6f, 0.4f, 0.6f))
                .Life(0.15f)
                .Gravity(-0.06f)
                .Size(0.5f, 0.5f)
                .Quad()
                .Build());

            int flashMin = Math.Max(10, (int)(radius * 8f * intensity));
            int flashMax = Math.Max(flashMin + 8, (int)(radius * 14f * intensity));

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(flashMin, flashMax)
                .Color(Colors.Fire)
                .Position(center.AddCopy(-radius * 0.3, 0, -radius * 0.3), center.AddCopy(radius * 0.3, radius * 0.2, radius * 0.3))
                .Velocity(new Vec3f(-0.3f, 0.3f, -0.3f), new Vec3f(0.3f, 0.8f, 0.3f))
                .Life(0.04f)
                .Gravity(0f)
                .Size(0.15f, 0.08f)
                .Quad()
                .Build());
        }

        /// <summary>Poison explosion with green mist.</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="intensity">The intensity.</param>
        public static void SpawnPoisonExplosion(ICoreServerAPI sapi, Vec3d center, float radius, int intensity = 1)
        {
            if (sapi == null) return;

            int min = Math.Max(30, (int)(radius * 20f * intensity));
            int max = Math.Max(min + 15, (int)(radius * 35f * intensity));

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(min, max)
                .Color(Colors.Poison)
                .Position(center.AddCopy(-radius, 0, -radius), center.AddCopy(radius, 1, radius))
                .Velocity(new Vec3f(-0.2f, 0.02f, -0.2f), new Vec3f(0.2f, 0.12f, 0.2f))
                .Life(2.0f)
                .Gravity(0.05f)
                .Size(0.4f, 0.6f)
                .Quad()
                .Build());
        }

        /// <summary>Generic colored explosion with particles of the given color.</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="color">The color value.</param>
        /// <param name="intensity">The intensity.</param>
        public static void SpawnExplosion(ICoreServerAPI sapi, Vec3d center, float radius, int color, int intensity = 1)
        {
            if (sapi == null) return;

            int min = Math.Max(35, (int)(radius * 25f * intensity));
            int max = Math.Max(min + 15, (int)(radius * 40f * intensity));

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(min, max)
                .Color(color)
                .Position(center.AddCopy(-radius * 0.6, -0.2, -radius * 0.6), center.AddCopy(radius * 0.6, radius * 0.5, radius * 0.6))
                .Velocity(new Vec3f(-0.4f, -0.1f, -0.4f), new Vec3f(0.4f, 0.3f, 0.4f))
                .Life(1.5f)
                .Gravity(-0.02f)
                .Size(0.6f, 0.4f)
                .Quad()
                .Build());
        }

        /// <summary>
        /// Shadow/void explosion with dark particles.
        /// If <paramref name="color" /> is 0, uses the default shadow/void colors.
        /// Otherwise tints the explosion with the supplied color.
        /// </summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="intensity">The intensity.</param>
        /// <param name="color">The color value.</param>
        public static void SpawnShadowExplosion(ICoreServerAPI sapi, Vec3d center, float radius, int intensity = 1, int color = 0)
        {
            if (sapi == null) return;

            int min = Math.Max(35, (int)(radius * 25f * intensity));
            int max = Math.Max(min + 15, (int)(radius * 40f * intensity));

            int outer = color == 0 ? Colors.Shadow : ColorUtil.ColorMultiply3(color, 0.5f);
            int inner = color == 0 ? Colors.Void : ColorUtil.ColorMultiply3(color, 0.25f);

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(min, max)
                .Color(outer)
                .Position(center.AddCopy(-radius * 0.6, -0.2, -radius * 0.6), center.AddCopy(radius * 0.6, radius * 0.5, radius * 0.6))
                .Velocity(new Vec3f(-0.4f, -0.1f, -0.4f), new Vec3f(0.4f, 0.3f, 0.4f))
                .Life(1.5f)
                .Gravity(-0.02f)
                .Size(0.6f, 0.4f)
                .Quad()
                .Build());

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(min / 3, max / 3)
                .Color(inner)
                .Position(center.AddCopy(-0.3, 0, -0.3), center.AddCopy(0.3, 0.5, 0.3))
                .Velocity(new Vec3f(-0.1f, 0.1f, -0.1f), new Vec3f(0.1f, 0.5f, 0.1f))
                .Life(0.8f)
                .Gravity(0f)
                .Size(0.3f, 0.2f)
                .Quad()
                .Build());
        }

        // ========================================
        // PRESET EFFECTS - AURAS
        // ========================================

        /// <summary>Spawn a ring of particles around a position (aura effect).</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnAuraRing(ICoreServerAPI sapi, Vec3d center, float radius, int color, int count = 12, float size = 0.3f)
        {
            if (sapi == null) return;

            for (int i = 0; i < count; i++)
            {
                double angle = (Math.PI * 2.0 / count) * i;
                double x = center.X + Math.Cos(angle) * radius;
                double z = center.Z + Math.Sin(angle) * radius;
                var pos = new Vec3d(x, center.Y, z);

                sapi.World.SpawnParticles(new ParticleEffectBuilder()
                    .Count(1, 2)
                    .Color(color)
                    .Position(pos.AddCopy(-0.1, 0, -0.1), pos.AddCopy(0.1, 0.3, 0.1))
                    .Velocity(new Vec3f(0, 0.1f, 0), new Vec3f(0, 0.3f, 0))
                    .Life(0.8f)
                    .Gravity(0f)
                    .Size(size, size * 0.8f)
                    .Quad()
                    .Build());
            }
        }

        /// <summary>Spawn a sphere of particles around a position.</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnAuraSphere(ICoreServerAPI sapi, Vec3d center, float radius, int color, int count = 16, float size = 0.25f)
        {
            if (sapi == null) return;

            var rand = sapi.World.Rand;
            for (int i = 0; i < count; i++)
            {
                double theta = rand.NextDouble() * Math.PI * 2;
                double phi = rand.NextDouble() * Math.PI;
                double x = center.X + Math.Sin(phi) * Math.Cos(theta) * radius;
                double y = center.Y + Math.Cos(phi) * radius;
                double z = center.Z + Math.Sin(phi) * Math.Sin(theta) * radius;
                var pos = new Vec3d(x, y, z);

                sapi.World.SpawnParticles(new ParticleEffectBuilder()
                    .Count(1, 1)
                    .Color(color)
                    .Position(pos, pos.AddCopy(0, 0.05, 0))
                    .Velocity(new Vec3f(0, 0.02f, 0), new Vec3f(0, 0.08f, 0))
                    .Life(0.6f)
                    .Gravity(0f)
                    .Size(size, size * 0.6f)
                    .Quad()
                    .Build());
            }
        }

        /// <summary>Spawn a sphere of particles around a position (client-side).</summary>
        /// <param name="capi">The client API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnAuraSphereClient(ICoreClientAPI capi, Vec3d center, float radius, int color, int count = 16, float size = 0.25f)
        {
            if (capi == null) return;

            var rand = capi.World.Rand;
            for (int i = 0; i < count; i++)
            {
                double theta = rand.NextDouble() * Math.PI * 2;
                double phi = rand.NextDouble() * Math.PI;
                double x = center.X + Math.Sin(phi) * Math.Cos(theta) * radius;
                double y = center.Y + Math.Cos(phi) * radius;
                double z = center.Z + Math.Sin(phi) * Math.Sin(theta) * radius;
                var pos = new Vec3d(x, y, z);

                capi.World.SpawnParticles(new ParticleEffectBuilder()
                    .Count(1, 1)
                    .Color(color)
                    .Position(pos, pos.AddCopy(0, 0.05, 0))
                    .Velocity(new Vec3f(0, 0.02f, 0), new Vec3f(0, 0.08f, 0))
                    .Life(0.6f)
                    .Gravity(0f)
                    .Size(size, size * 0.6f)
                    .Quad()
                    .Build());
            }
        }

        /// <summary>Spawn a column/pillar of particles rising from a position.</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="basePos">The three-dimensional vector.</param>
        /// <param name="height">The height.</param>
        /// <param name="width">The width.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        public static void SpawnPillar(ICoreServerAPI sapi, Vec3d basePos, float height, float width, int color, int count = 20)
        {
            if (sapi == null) return;

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(count, count + 8)
                .Color(color)
                .Position(basePos.AddCopy(-width * 0.5, 0, -width * 0.5), basePos.AddCopy(width * 0.5, height * 0.3, width * 0.5))
                .Velocity(new Vec3f(-0.05f, 0.3f, -0.05f), new Vec3f(0.05f, 0.8f, 0.05f))
                .Life(1.2f)
                .Gravity(-0.02f)
                .Size(0.3f, 0.2f)
                .Quad()
                .Build());
        }

        // ========================================
        // PRESET EFFECTS - TRAILS & LINES
        // ========================================

        /// <summary>Spawn particles along a line between two points (chain/beam effect).</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="from">The three-dimensional vector.</param>
        /// <param name="to">The three-dimensional vector.</param>
        /// <param name="color">The color value.</param>
        /// <param name="segments">The segments value.</param>
        /// <param name="size">The size.</param>
        public static void SpawnLine(ICoreServerAPI sapi, Vec3d from, Vec3d to, int color, int segments = 8, float size = 0.2f)
        {
            if (sapi == null) return;

            Vec3d dir = to.SubCopy(from);
            double length = dir.Length();
            if (length < 0.01) return;
            dir.Normalize();

            double step = length / segments;
            for (int i = 0; i <= segments; i++)
            {
                Vec3d pos = from.AddCopy(dir.X * step * i, dir.Y * step * i, dir.Z * step * i);

                sapi.World.SpawnParticles(new ParticleEffectBuilder()
                    .Count(1, 2)
                    .Color(color)
                    .Position(pos.AddCopy(-0.05, -0.05, -0.05), pos.AddCopy(0.05, 0.05, 0.05))
                    .Velocity(new Vec3f(-0.02f, -0.02f, -0.02f), new Vec3f(0.02f, 0.02f, 0.02f))
                    .Life(0.3f)
                    .Gravity(0f)
                    .Size(size, size * 0.7f)
                    .Quad()
                    .Build());
            }
        }

        /// <summary>Spawn a spiral of particles around a position.</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnSpiral(ICoreServerAPI sapi, Vec3d center, float radius, float height, int color, int count = 24, float size = 0.2f)
        {
            if (sapi == null) return;

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / count;
                double angle = t * Math.PI * 4;
                double y = center.Y + t * height;
                double x = center.X + Math.Cos(angle) * radius * (1 - t * 0.3);
                double z = center.Z + Math.Sin(angle) * radius * (1 - t * 0.3);
                var pos = new Vec3d(x, y, z);

                sapi.World.SpawnParticles(new ParticleEffectBuilder()
                    .Count(1, 1)
                    .Color(color)
                    .Position(pos, pos.AddCopy(0, 0.02, 0))
                    .Velocity(new Vec3f(0, 0.1f, 0), new Vec3f(0, 0.2f, 0))
                    .Life(0.5f)
                    .Gravity(0f)
                    .Size(size, size * 0.5f)
                    .Quad()
                    .Build());
            }
        }

        // ========================================
        // PRESET EFFECTS - ENTITY-ATTACHED
        // ========================================

        /// <summary>Spawn particles around an entity (body glow/aura).</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        /// <param name="spread">The spread value.</param>
        public static void SpawnEntityAura(ICoreServerAPI sapi, Entity entity, int color, int count = 6, float size = 0.4f, float spread = 0.5f)
        {
            if (sapi == null || entity == null) return;

            var pos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 * 0.5, 0);

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(count, count + 3)
                .Color(color)
                .Position(pos.AddCopy(-spread, -spread * 0.5, -spread), pos.AddCopy(spread, spread, spread))
                .Velocity(new Vec3f(-0.1f, 0.1f, -0.1f), new Vec3f(0.1f, 0.4f, 0.1f))
                .Life(0.8f)
                .Gravity(0f)
                .Size(size, size * 0.6f)
                .Quad()
                .Build());
        }

        /// <summary>Spawn impact particles at an entity's position (hit effect).</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnImpact(ICoreServerAPI sapi, Entity entity, int color, int count = 10, float size = 0.3f)
        {
            if (sapi == null || entity == null) return;

            var pos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 * 0.5, 0);
            SpawnImpact(sapi, pos, color, count, size);
        }

        /// <summary>Spawn impact particles at a world position (hit effect).</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="pos">The position.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnImpact(ICoreServerAPI sapi, Vec3d pos, int color, int count = 10, float size = 0.3f)
        {
            if (sapi == null || pos == null) return;

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(count, count + 5)
                .Color(color)
                .Position(pos.AddCopy(-0.2, -0.2, -0.2), pos.AddCopy(0.2, 0.2, 0.2))
                .Velocity(new Vec3f(-0.8f, -0.3f, -0.8f), new Vec3f(0.8f, 0.8f, 0.8f))
                .Life(0.3f)
                .Gravity(0.5f)
                .Size(size, size * 0.4f)
                .Quad()
                .Build());
        }

        /// <summary>Spawn ground-level particles spreading outward (shockwave).</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnShockwave(ICoreServerAPI sapi, Vec3d center, float radius, int color, int count = 24, float size = 0.4f)
        {
            if (sapi == null) return;

            var rand = sapi.World.Rand;
            for (int i = 0; i < count; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                float speed = 0.3f + (float)rand.NextDouble() * 0.5f;
                float vx = (float)Math.Cos(angle) * speed;
                float vz = (float)Math.Sin(angle) * speed;

                sapi.World.SpawnParticles(new ParticleEffectBuilder()
                    .Count(1, 2)
                    .Color(color)
                    .Position(center.AddCopy(-0.2, 0, -0.2), center.AddCopy(0.2, 0.3, 0.2))
                    .Velocity(new Vec3f(vx * 0.8f, 0.05f, vz * 0.8f), new Vec3f(vx * 1.2f, 0.15f, vz * 1.2f))
                    .Life(0.6f)
                    .Gravity(0.2f)
                    .Size(size, size * 0.5f)
                    .Quad()
                    .Build());
            }
        }

        // ========================================
        // PRESET EFFECTS - WEATHER/AMBIENT
        // ========================================

        /// <summary>Spawn falling particles (rain, ash, embers).</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnFalling(ICoreServerAPI sapi, Vec3d center, float radius, float height, int color, int count = 15, float size = 0.2f)
        {
            if (sapi == null) return;

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(count, count + 5)
                .Color(color)
                .Position(center.AddCopy(-radius, height * 0.5, -radius), center.AddCopy(radius, height, radius))
                .Velocity(new Vec3f(-0.05f, -0.3f, -0.05f), new Vec3f(0.05f, -0.1f, 0.05f))
                .Life(1.5f)
                .Gravity(0.3f)
                .Size(size, size * 0.6f)
                .Quad()
                .Build());
        }

        /// <summary>Spawn rising embers/sparks.</summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        public static void SpawnEmbers(ICoreServerAPI sapi, Vec3d center, float radius, int count = 10, float size = 0.15f)
        {
            if (sapi == null) return;

            sapi.World.SpawnParticles(new ParticleEffectBuilder()
                .Count(count, count + 5)
                .Color(Colors.Fire)
                .Position(center.AddCopy(-radius, 0, -radius), center.AddCopy(radius, 0.5, radius))
                .Velocity(new Vec3f(-0.1f, 0.2f, -0.1f), new Vec3f(0.1f, 0.6f, 0.1f))
                .Life(1.0f)
                .Gravity(-0.1f)
                .Size(size, size * 0.4f)
                .Quad()
                .Build());
        }

        /// <summary>
        /// Spawn slow rising particles around a position (idle/ambient effect).
        /// Particles drift upward with slight horizontal spread.
        /// </summary>
        /// <param name="sapi">The server API instance.</param>
        /// <param name="center">The center position.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <param name="color">The color value.</param>
        /// <param name="count">The number of items.</param>
        /// <param name="size">The size.</param>
        /// <param name="riseSpeed">The rise speed value.</param>
        public static void SpawnRising(ICoreServerAPI sapi, Vec3d center, float radius, float height,
            int color, int count = 3, float size = 0.3f, float riseSpeed = 0.08f)
        {
            if (sapi == null) return;

            var rand = sapi.World.Rand;
            for (int i = 0; i < count; i++)
            {
                double x = center.X + (rand.NextDouble() - 0.5) * radius * 2;
                double y = center.Y + rand.NextDouble() * height;
                double z = center.Z + (rand.NextDouble() - 0.5) * radius * 2;

                sapi.World.SpawnParticles(new ParticleEffectBuilder()
                    .Count(1, 1)
                    .Color(color)
                    .Position(new Vec3d(x, y, z), new Vec3d(x, y + 0.1, z))
                    .Velocity(new Vec3f(0, riseSpeed * 0.6f, 0), new Vec3f(0, riseSpeed, 0))
                    .Life(1.0f + (float)rand.NextDouble() * 0.5f)
                    .Gravity(0f)
                    .Size(size, size * 0.7f)
                    .Quad()
                    .Build());
            }
        }
    }
}
