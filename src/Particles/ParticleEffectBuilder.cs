using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ArcanumLib.Particles
{
    /// <summary>
    /// Fluent builder for creating custom particle effects.
    /// Wraps <see cref="SimpleParticleProperties"/> with a chainable API
    /// for count, color, position, velocity, life, gravity, size, and model.
    /// </summary>
    public class ParticleEffectBuilder
    {
        private int minCount = 5;
        private int maxCount = 10;
        private int color = ParticleUtils.Colors.White;
        private Vec3d minPos = new Vec3d();
        private Vec3d maxPos = new Vec3d();
        private Vec3f minVelocity = new Vec3f(0, 0, 0);
        private Vec3f maxVelocity = new Vec3f(0, 0.1f, 0);
        private float lifeLength = 1.0f;
        private float gravityEffect = 0f;
        private float minSize = 0.3f;
        private float maxSize = 0.3f;
        private EnumParticleModel model = EnumParticleModel.Quad;

        /// <summary>Sets the minimum and maximum particle count.</summary>
        public ParticleEffectBuilder Count(int min, int max)
        {
            minCount = min;
            maxCount = max;
            return this;
        }

        /// <summary>Sets the RGBA color of the particles.</summary>
        public ParticleEffectBuilder Color(int rgba)
        {
            color = rgba;
            return this;
        }

        /// <summary>Sets the spawn area as a cube centered on <paramref name="center"/> with the given <paramref name="spread"/>.</summary>
        public ParticleEffectBuilder Position(Vec3d center, float spread = 0.5f)
        {
            minPos = center.AddCopy(-spread, -spread, -spread);
            maxPos = center.AddCopy(spread, spread, spread);
            return this;
        }

        /// <summary>Sets the spawn area as an explicit min/max bounding box.</summary>
        public ParticleEffectBuilder Position(Vec3d min, Vec3d max)
        {
            minPos = min;
            maxPos = max;
            return this;
        }

        /// <summary>Sets the spawn area around an entity's midpoint with the given <paramref name="spread"/>.</summary>
        public ParticleEffectBuilder AtEntity(Entity entity, float spread = 0.5f)
        {
            if (entity != null)
            {
                var center = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 * 0.5, 0);
                minPos = center.AddCopy(-spread, -spread * 0.5, -spread);
                maxPos = center.AddCopy(spread, spread, spread);
            }
            return this;
        }

        /// <summary>Sets the min and max velocity of the particles.</summary>
        public ParticleEffectBuilder Velocity(Vec3f min, Vec3f max)
        {
            minVelocity = min;
            maxVelocity = max;
            return this;
        }

        /// <summary>Sets an upward velocity range with slight horizontal jitter.</summary>
        public ParticleEffectBuilder VelocityUp(float min = 0.1f, float max = 0.4f)
        {
            minVelocity = new Vec3f(-0.05f, min, -0.05f);
            maxVelocity = new Vec3f(0.05f, max, 0.05f);
            return this;
        }

        /// <summary>Sets an outward velocity range with the given <paramref name="speed"/>.</summary>
        public ParticleEffectBuilder VelocityOutward(float speed = 0.3f)
        {
            minVelocity = new Vec3f(-speed, -speed * 0.3f, -speed);
            maxVelocity = new Vec3f(speed, speed, speed);
            return this;
        }

        /// <summary>Sets the particle lifetime in seconds.</summary>
        public ParticleEffectBuilder Life(float seconds)
        {
            lifeLength = seconds;
            return this;
        }

        /// <summary>Sets the gravity effect on the particles.</summary>
        public ParticleEffectBuilder Gravity(float gravity)
        {
            gravityEffect = gravity;
            return this;
        }

        /// <summary>Sets the min and max particle size.</summary>
        public ParticleEffectBuilder Size(float min, float max)
        {
            minSize = min;
            maxSize = max;
            return this;
        }

        /// <summary>Sets a uniform particle size.</summary>
        public ParticleEffectBuilder Size(float size)
        {
            minSize = size;
            maxSize = size;
            return this;
        }

        /// <summary>Sets the particle model.</summary>
        public ParticleEffectBuilder Model(EnumParticleModel particleModel)
        {
            model = particleModel;
            return this;
        }

        /// <summary>Sets the particle model to <see cref="EnumParticleModel.Cube"/>.</summary>
        public ParticleEffectBuilder Cube()
        {
            model = EnumParticleModel.Cube;
            return this;
        }

        /// <summary>Sets the particle model to <see cref="EnumParticleModel.Quad"/>.</summary>
        public ParticleEffectBuilder Quad()
        {
            model = EnumParticleModel.Quad;
            return this;
        }

        /// <summary>Builds the <see cref="SimpleParticleProperties"/> object.</summary>
        public SimpleParticleProperties Build()
        {
            return new SimpleParticleProperties(
                minCount, maxCount, color,
                minPos, maxPos,
                minVelocity, maxVelocity,
                lifeLength, gravityEffect,
                minSize, maxSize,
                model
            );
        }

        /// <summary>Builds and immediately spawns the particles on the server.</summary>
        public void Spawn(ICoreServerAPI sapi)
        {
            if (sapi == null) return;
            sapi.World.SpawnParticles(Build());
        }

        /// <summary>Builds and immediately spawns the particles on the given world.</summary>
        public void Spawn(IWorldAccessor world)
        {
            if (world == null) return;
            world.SpawnParticles(Build());
        }
    }
}
