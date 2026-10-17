using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free wind field for the living arena (the illusion layer behind
    /// <c>scripts/vfx/BiomeAtmosphere.cs</c>, <c>scripts/vfx/AmbientWildlife.cs</c>
    /// and <c>scripts/vfx/LivingGround.cs</c>).
    ///
    /// One wind blows through everything: a slow prevailing drift plus
    /// periodic gusts (deterministic functions of elapsed time), and transient
    /// impulses punched by explosions and heavy impacts (positional, with
    /// radius falloff and expiring lifetimes). Consumers sample the same state
    /// so ground sway and particle slant always agree — the coherence that
    /// reads as "weather" rather than layered effects. (Wildlife consumes only
    /// the base gusting wind by design: blast reactions reach critters through
    /// panic, not impulses — see <c>scripts/vfx/AmbientWildlife.cs</c>.)
    ///
    /// Extracted per the AGENTS.md pattern so the xUnit suite covers gust
    /// envelopes, impulse pooling/decay and sampling bounds headlessly
    /// (WindFieldTests); the node wrappers only read values and set engine
    /// properties.
    /// </summary>
    public sealed class AmbientWindState
    {
        /// <summary>Pool size for transient wind impulses.</summary>
        public const int MaxImpulses = 8;

        /// <summary>Soft cap for base+gust wind speed (px/s). Sampling never exceeds this.</summary>
        public const float MaxBaseSpeed = 90f;

        /// <summary>Seconds between gust onsets.</summary>
        public const float GustPeriod = 11f;

        /// <summary>Rise and release durations of a single gust.</summary>
        public const float GustRise = 2.2f;
        public const float GustRelease = 4.5f;

        /// <summary>One pooled explosion impulse. Positions are world-space floats.</summary>
        public struct Impulse
        {
            public bool Active;
            public float X;
            public float Y;
            public float Strength;
            public float Radius;
            public float Life;
            public float Age;

            /// <summary>Decay factor 1 → 0 over the impulse lifetime (0 when inactive).</summary>
            public float Envelope01 => Active ? AmbientWindState.Envelope(Age, Life) : 0f;
        }

        private readonly Impulse[] _impulses = new Impulse[MaxImpulses];
        private float _time;

        /// <summary>Elapsed wind time in seconds (deterministic gust clock).</summary>
        public float Time => _time;

        /// <summary>Impulses currently alive.</summary>
        public int ActiveImpulseCount { get; private set; }

        /// <summary>Advances the gust clock. Impulses are aged by the same dt.</summary>
        public void Update(float dt)
        {
            if (dt <= 0f) return;
            _time += dt;
            for (int i = 0; i < MaxImpulses; i++)
            {
                ref var imp = ref _impulses[i];
                if (!imp.Active) continue;
                imp.Age += dt;
                if (imp.Age >= imp.Life) imp.Active = false;
            }
            RecomputeActiveCount();
        }

        /// <summary>
        /// The prevailing wind (px/s): two slow sine pairs per axis — a
        /// constant character without a random source, so every consumer and
        /// every replay agrees.
        /// </summary>
        public (float X, float Y) BaseWind()
        {
            float t = _time;
            float wx = 26f * MathF.Sin(t * 0.11f + 1.7f) + 14f * MathF.Sin(t * 0.31f + 0.4f);
            float wy = 18f * MathF.Sin(t * 0.09f + 4.2f) + 9f * MathF.Sin(t * 0.23f + 2.1f);
            return (wx, wy);
        }

        /// <summary>
        /// Gust envelope in [0, 1]: a fast swell and a long release, repeating
        /// every <see cref="GustPeriod"/> seconds. 0 outside the gust window.
        /// </summary>
        public float GustStrength()
        {
            float phase = _time - GustPeriod * MathF.Floor(_time / GustPeriod);
            if (phase < GustRise)
            {
                float k = phase / GustRise;
                return k * k;
            }
            float tail = phase - GustRise;
            if (tail < GustRelease)
                return 1f - tail / GustRelease;
            return 0f;
        }

        /// <summary>Speed multiplier the gust applies to the base wind.</summary>
        public static float GustGain(float gust01) => 1f + 1.35f * Math.Clamp(gust01, 0f, 1f);

        /// <summary>Base wind amplified by the current gust, capped to <see cref="MaxBaseSpeed"/>.</summary>
        public (float X, float Y) Wind()
        {
            var (bx, by) = BaseWind();
            float gain = GustGain(GustStrength());
            return ClampSpeed(bx * gain, by * gain, MaxBaseSpeed);
        }

        /// <summary>
        /// Full sample at a world position: gusting base wind plus the sum of
        /// all live impulse contributions there.
        /// </summary>
        public (float X, float Y) Sample(float x, float y)
        {
            var (wx, wy) = Wind();
            var (ix, iy) = ImpulseAt(x, y);
            return (wx + ix, wy + iy);
        }

        /// <summary>
        /// Spawns an impulse. Reuses the first free slot, else overwrites the
        /// most consumed (highest age/life) slot — a fresh explosion always
        /// wins over a dying gust.
        /// </summary>
        public void AddImpulse(float x, float y, float strength, float radius, float life)
        {
            int idx = 0;
            float worst = -1f;
            for (int i = 0; i < MaxImpulses; i++)
            {
                if (!_impulses[i].Active)
                {
                    idx = i;
                    break;
                }
                float consumed = _impulses[i].Age / MathF.Max(_impulses[i].Life, 0.0001f);
                if (consumed > worst)
                {
                    worst = consumed;
                    idx = i;
                }
            }

            _impulses[idx] = new Impulse
            {
                Active = true,
                X = x,
                Y = y,
                Strength = strength,
                Radius = radius < 1f ? 1f : radius,
                Life = life < 0.01f ? 0.01f : life,
                Age = 0f,
            };
            RecomputeActiveCount();
        }

        /// <summary>
        /// Sum of live impulses at a position: radial push away from each
        /// epicentre with quadratic falloff to its radius, scaled by the
        /// impulse's decay envelope. A push never exceeds the age-scaled
        /// strength, so drifting creatures cannot be launched absurdly.
        /// </summary>
        public (float X, float Y) ImpulseAt(float x, float y)
        {
            float sx = 0f;
            float sy = 0f;
            for (int i = 0; i < MaxImpulses; i++)
            {
                ref var imp = ref _impulses[i];
                if (!imp.Active) continue;

                float dx = x - imp.X;
                float dy = y - imp.Y;
                float distSq = dx * dx + dy * dy;
                float radiusSq = imp.Radius * imp.Radius;
                if (distSq >= radiusSq) continue;

                float dist = MathF.Sqrt(distSq);
                if (dist < 0.001f) continue; // epicentre: push direction undefined, stay safe

                float falloff = 1f - dist / imp.Radius;
                float magnitude = imp.Strength * imp.Envelope01 * falloff * falloff;
                sx += dx / dist * magnitude;
                sy += dy / dist * magnitude;
            }
            return (sx, sy);
        }

        /// <summary>Impulse decay envelope: linear 1 → 0 across the lifetime.</summary>
        public static float Envelope(float age, float life)
        {
            if (life <= 0f || age >= life) return 0f;
            return 1f - age / life;
        }

        /// <summary>Clamps a vector to a speed cap (deterministic, allocation-free).</summary>
        public static (float X, float Y) ClampSpeed(float x, float y, float maxSpeed)
        {
            float speedSq = x * x + y * y;
            float maxSq = maxSpeed * maxSpeed;
            if (speedSq <= maxSq || speedSq <= 0f) return (x, y);
            float scale = maxSpeed / MathF.Sqrt(speedSq);
            return (x * scale, y * scale);
        }

        /// <summary>
        /// Standard transient impulse for an explosion of the given blast
        /// radius (px): bigger blasts push harder, reach further and last a
        /// beat longer. Ground detail and atmosphere derive impulse parameters
        /// through this one function so independently-updated wind instances
        /// fed the same events stay numerically identical.
        /// </summary>
        public static (float Strength, float Radius, float Life) ExplosionImpulse(float sourceRadius)
        {
            float r = MathF.Max(24f, sourceRadius);
            return (110f + r * 0.9f, 96f + r * 2.2f, 0.7f + r / 300f);
        }

        /// <summary>Blast radius (px) the wind treats an explosive projectile hit as.</summary>
        public const float ExplosiveHitRadius = 48f;

        /// <summary>Blast radius (px) for an acid blast — currently an Exploder's detonation.</summary>
        public const float AcidHitRadius = 40f;

        /// <summary>
        /// Wind impulse for a projectile hit, or zero parameters when the
        /// impact is too small to move the air. Explosive and acid impacts are
        /// blast-like (the AoE hits gameplay emits today); bullet-weight
        /// schools stay silent so gunfire never shakes the weather.
        /// </summary>
        public static (float Strength, float Radius, float Life) ImpulseForHit(DamageType type) => type switch
        {
            DamageType.Explosive => ExplosionImpulse(ExplosiveHitRadius),
            DamageType.Acid => ExplosionImpulse(AcidHitRadius),
            _ => (0f, 0f, 0f),
        };

        private void RecomputeActiveCount()
        {
            int n = 0;
            for (int i = 0; i < MaxImpulses; i++)
            {
                if (_impulses[i].Active) n++;
            }
            ActiveImpulseCount = n;
        }
    }
}
