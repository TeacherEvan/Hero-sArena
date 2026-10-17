using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Nearest-target selection for Synapse's Overclock cast. Pure and
    /// Godot-free so the ordering and cap rules are headlessly testable;
    /// the Synapse hero Node supplies distances and delegates 1:1 (same
    /// pattern as ProgressionFormulas / WaveFormulas).
    /// </summary>
    public static class MindControlTargets
    {
        /// <summary>
        /// Indexes of the nearest up to <paramref name="maxTargets"/> entries in
        /// <paramref name="distancesSq"/>, ordered by ascending distance.
        /// Distance ties keep the lower index first so a cast is deterministic.
        /// Returns an empty array for a non-positive cap or empty input.
        /// </summary>
        public static int[] SelectNearest(IReadOnlyList<float> distancesSq, int maxTargets)
        {
            if (maxTargets <= 0 || distancesSq.Count == 0) return Array.Empty<int>();

            var index = new int[distancesSq.Count];
            for (int i = 0; i < index.Length; i++) index[i] = i;

            Array.Sort(index, (a, b) =>
            {
                int byDistance = distancesSq[a].CompareTo(distancesSq[b]);
                return byDistance != 0 ? byDistance : a.CompareTo(b);
            });

            int take = Math.Min(distancesSq.Count, maxTargets);
            var result = new int[take];
            Array.Copy(index, result, take);
            return result;
        }
    }
}
