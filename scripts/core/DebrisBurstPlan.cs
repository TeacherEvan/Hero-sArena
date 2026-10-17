using System;

namespace HeroArena
{
    /// <summary>
    /// Deterministic debris-burst planning for the DestructionDrama node
    /// (scripts/vfx/DestructionDrama.cs). The same world position always
    /// produces the same shard spread (position is hashed into the seed), so
    /// destruction patterns look authored rather than noisy, and the xUnit
    /// suite can assert the plan + integration math headlessly.
    ///
    /// The illusion this drives: destroyed cover bursts into spinning chunks
    /// that arc, land with a damped bounce and fade — fake mass and fake
    /// physics from plain sprites.
    /// </summary>
    public static class DebrisBurstPlan
    {
        /// <summary>Hard cap on shards per burst (pool-friendly).</summary>
        public const int MaxShards = 12;

        /// <summary>One planned shard. Angles in radians, speeds in px/s.</summary>
        public struct Shard
        {
            public float Angle;
            public float Speed;
            public float Spin;
            public float Size;
            public float Life;
            public float Delay;
            /// <summary>Distance below the burst origin where this shard lands.</summary>
            public float GroundDrop;
        }

        /// <summary>
        /// Fills dst[0..count) with a deterministic spread seeded from
        /// <paramref name="seed"/>. Returns the number of shards written
        /// (clamped to <see cref="MaxShards"/> and dst.Length).
        /// </summary>
        public static int Generate(int seed, int count, float power, Shard[] dst)
        {
            if (count > dst.Length) count = dst.Length;
            if (count > MaxShards) count = MaxShards;
            if (count < 0) count = 0;

            uint s = (uint)seed * 747796405u + 2891336453u;
            float p = Math.Clamp(power, 0.25f, 3f);

            for (int i = 0; i < count; i++)
            {
                float r1 = Next01(ref s);
                float r2 = Next01(ref s);
                float r3 = Next01(ref s);
                float r4 = Next01(ref s);
                float r5 = Next01(ref s);
                float r6 = Next01(ref s);
                float r7 = Next01(ref s);

                dst[i] = new Shard
                {
                    Angle = r1 * MathF.Tau,
                    Speed = (90f + r2 * 260f) * p,
                    Spin = (r3 * 2f - 1f) * 9f,
                    Size = 0.55f + r4 * 0.85f,
                    Life = 0.6f + r5 * 0.7f,
                    Delay = r6 * 0.05f,
                    GroundDrop = 10f + r7 * 26f,
                };
            }
            return count;
        }

        /// <summary>Shard count for a destruction of the given radius.</summary>
        public static int CountForRadius(float radius)
            => Math.Clamp((int)(radius / 12f), 4, MaxShards);

        /// <summary>
        /// Stable seed from a world position (xorshift-style mix of the scaled
        /// coordinates) so the same crate always shatters the same way.
        /// </summary>
        public static int SeedFromPosition(float x, float y)
        {
            int ix = (int)MathF.Round(x * 4f);
            int iy = (int)MathF.Round(y * 4f);
            return unchecked(ix * 73856093 ^ iy * 19349663);
        }

        private static float Next01(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) * (1f / 16777216f);
        }
    }

    /// <summary>
    /// Per-shard ballistic state: gravity, drag, a fake ground plane with a
    /// damped bounce, and a fade over the second half of the life. Pure math —
    /// DestructionDrama only copies X/Y/Rotation/Scale/Alpha onto sprites.
    /// </summary>
    public struct DebrisShardState
    {
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Rotation;
        public float Spin;
        public float Scale;
        public float Age;
        public float Life;
        /// <summary>World Y where this shard rests (origin Y + planned drop).</summary>
        public float GroundY;
        public bool Resting;

        /// <summary>True once the shard has fully faded and can be recycled.</summary>
        public bool Finished => Life <= 0f || Age >= Life;

        /// <summary>Fully opaque for the first half of life, then fades to 0.</summary>
        public float Alpha
        {
            get
            {
                if (Life <= 0f || Age < 0f || Age >= Life) return 0f;
                float t = Age / Life;
                return t <= 0.5f ? 1f : Math.Clamp(1f - (t - 0.5f) * 2f, 0f, 1f);
            }
        }

        /// <summary>Builds a running shard from a plan entry (Age starts negative for the delay).</summary>
        public static DebrisShardState FromPlan(in DebrisBurstPlan.Shard shard, float x, float y)
        {
            return new DebrisShardState
            {
                X = x,
                Y = y,
                VX = MathF.Cos(shard.Angle) * shard.Speed,
                VY = MathF.Sin(shard.Angle) * shard.Speed - shard.Speed * 0.25f,
                Rotation = shard.Angle,
                Spin = shard.Spin,
                Scale = shard.Size,
                Age = -shard.Delay,
                Life = shard.Life,
                GroundY = y + shard.GroundDrop,
                Resting = false,
            };
        }

        /// <summary>
        /// Integrates one step. Gravity pulls down (px/s^2), drag damps
        /// velocity proportionally per second. Landing bounces once, then rests.
        /// </summary>
        public void Step(float dt, float gravity, float drag)
        {
            if (Life <= 0f || dt <= 0f) return;

            Age += dt;
            if (Age < 0f) return; // still delayed: hold position
            if (Age >= Life) return; // finished: hold last pose (caller recycles)
            if (Resting) return;

            VY += gravity * dt;
            float k = 1f - Math.Clamp(drag, 0f, 1f) * dt;
            VX *= k;
            VY *= k;

            X += VX * dt;
            Y += VY * dt;
            Rotation += Spin * dt;
            Spin *= k;

            if (Y >= GroundY)
            {
                Y = GroundY;
                if (MathF.Abs(VY) < 46f)
                {
                    VY = 0f;
                    VX *= 0.55f;
                    Spin *= 0.4f;
                    Resting = MathF.Abs(VX) < 8f;
                }
                else
                {
                    VY = -MathF.Abs(VY) * 0.35f;
                }
                VX *= 0.6f;
            }
        }
    }
}
