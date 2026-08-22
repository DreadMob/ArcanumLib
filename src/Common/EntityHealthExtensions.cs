using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace ArcanumLib.Common
{
    /// <summary>
    /// Helpers for reading and scaling entity health.
    /// </summary>
    public static class EntityHealthExtensions
    {
        /// <summary>
        /// Tries to get the current health as a fraction of max health.
        /// </summary>
        public static bool TryGetHealthFraction(this Entity entity, out float fraction)
        {
            fraction = 1f;
            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            var healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return false;

            float maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);

            float currentHealth = healthTree.GetFloat("currenthealth", 0f);
            if (maxHealth <= 0f || currentHealth <= 0f) return false;

            fraction = currentHealth / maxHealth;
            return true;
        }

        /// <summary>
        /// Tries to read the health tree and current/max values.
        /// </summary>
        public static bool TryGetHealth(this Entity entity, out ITreeAttribute? healthTree, out float currentHealth, out float maxHealth)
        {
            healthTree = null;
            currentHealth = 0f;
            maxHealth = 0f;

            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return false;

            maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);

            currentHealth = healthTree.GetFloat("currenthealth", 0f);
            return maxHealth > 0f && currentHealth > 0f;
        }

        /// <summary>
        /// Scales the entity's max health by the given multiplier and sets current health to the new max.
        /// Returns true when the health tree was found and updated.
        /// </summary>
        public static bool ScaleHealth(this Entity entity, float mult)
        {
            if (entity == null || mult <= 0f || mult == 1f) return false;

            var wa = entity.WatchedAttributes;
            if (wa == null) return false;

            var healthTree = wa.GetTreeAttribute("health");
            if (healthTree == null) return false;

            float maxHealth = healthTree.GetFloat("maxhealth", 0f);
            if (maxHealth <= 0f)
                maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
            if (maxHealth <= 0f) return false;

            float newMax = maxHealth * mult;
            healthTree.SetFloat("maxhealth", newMax);
            healthTree.SetFloat("currenthealth", newMax);
            wa.MarkPathDirty("health");

            return true;
        }
    }
}
