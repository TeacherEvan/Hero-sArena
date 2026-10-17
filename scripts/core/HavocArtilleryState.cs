using System.Collections.Generic;
using Godot;

namespace HeroArena
{
    public readonly record struct ArtilleryStrike(Vector2 TargetOffset, float RadiusMultiplier, float DamageMultiplier, float DelaySeconds);

    /// <summary>
    /// Pure logic calculations for Havoc's Rocket Recoil, Cluster Munitions, and Orbital Barrage.
    /// Free of Godot Node dependencies; tested headlessly via xUnit.
    /// </summary>
    public static class HavocArtilleryState
    {
        public const float DEFAULT_RECOIL_FORCE = 260f;

        public static Vector2 CalcRecoilImpulse(Vector2 aimDirection, float recoilForce = DEFAULT_RECOIL_FORCE)
        {
            if (aimDirection.LengthSquared() < 0.0001f) return Vector2.Zero;
            return -aimDirection.Normalized() * recoilForce;
        }

        public static Vector2[] CalcClusterSpreadDirections(Vector2 baseDirection, int clusterCount = 3, float spreadAngleRad = 0.45f)
        {
            if (clusterCount <= 1) return new[] { baseDirection };

            var dirs = new Vector2[clusterCount];
            float startAngle = -spreadAngleRad * 0.5f;
            float step = spreadAngleRad / (clusterCount - 1);

            for (int i = 0; i < clusterCount; i++)
            {
                float angle = startAngle + (i * step);
                dirs[i] = baseDirection.Rotated(angle).Normalized();
            }

            return dirs;
        }

        public static IReadOnlyList<ArtilleryStrike> GetSequentialStrikes()
        {
            return new ArtilleryStrike[]
            {
                new(new Vector2(-50f, -40f), 0.75f, 0.8f, 0.0f),
                new(new Vector2(60f, 35f), 0.85f, 0.9f, 0.22f),
                new(Vector2.Zero, 1.25f, 1.4f, 0.45f) // Direct center bunker buster
            };
        }
    }
}
