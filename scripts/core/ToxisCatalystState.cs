using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Pure logic state machine for Toxis's Corrosive Catalyst and Rupture Siphon.
    /// Free of Godot Node dependencies; tested headlessly via xUnit.
    /// </summary>
    public class ToxisCatalystState
    {
        public const int MAX_CORROSION_STACKS = 5;
        public const float DAMAGE_BONUS_PER_STACK = 0.10f;
        public const float SLOW_PER_STACK = 0.08f;
        public const float SIPHON_HEALING_PER_STACK = 3.0f;

        private readonly Dictionary<int, int> _corrosion = new();

        public int AddCorrosion(int entityId, int stacksToAdd = 1)
        {
            if (stacksToAdd <= 0) return GetCorrosion(entityId);

            _corrosion.TryGetValue(entityId, out int current);
            int updated = Math.Clamp(current + stacksToAdd, 0, MAX_CORROSION_STACKS);
            _corrosion[entityId] = updated;
            return updated;
        }

        public int GetCorrosion(int entityId)
        {
            return _corrosion.TryGetValue(entityId, out int stacks) ? stacks : 0;
        }

        public void RemoveTarget(int entityId)
        {
            _corrosion.Remove(entityId);
        }

        public float GetDamageAmplification(int entityId)
        {
            int stacks = GetCorrosion(entityId);
            return 1.0f + (stacks * DAMAGE_BONUS_PER_STACK);
        }

        public float GetSpeedMultiplier(int entityId)
        {
            int stacks = GetCorrosion(entityId);
            return Math.Max(0.5f, 1.0f - (stacks * SLOW_PER_STACK));
        }

        public List<KeyValuePair<int, int>> DetonateAll()
        {
            var results = new List<KeyValuePair<int, int>>(_corrosion);
            _corrosion.Clear();
            return results;
        }

        public static float CalcRuptureDamage(float baseDamage, int stacks)
        {
            return baseDamage + (stacks * 25f);
        }

        public static float CalcSiphonHealing(int totalDetonatedStacks)
        {
            return totalDetonatedStacks * SIPHON_HEALING_PER_STACK;
        }
    }
}
