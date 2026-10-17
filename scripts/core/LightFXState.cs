using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free math for the LivingLightRig (scripts/vfx/LivingLightRig.cs):
    /// a fixed pool of transient light flashes (impact strobes, explosion
    /// flares, hero-damage pulses) plus ambient-mood smoothing and flicker.
    /// Extracted per the AGENTS.md pattern so the xUnit suite covers slot
    /// recycling, decay envelopes, rate limiting and mood curves headlessly;
    /// the Node wrapper only maps slots onto PointLight2D instances.
    ///
    /// The illusion this drives: the arena is lit BY the fight — every
    /// impact, kill and blast briefly carves light out of the darkened
    /// canvas, and the ambient glow escalates with the Collateral Karma
    /// threat level.
    /// </summary>
    public sealed class LightFXState
    {
        /// <summary>Pool size for transient flashes.</summary>
        public const int SlotCount = 12;

        /// <summary>Minimum seconds between non-bypass flashes (rate limit).</summary>
        public const float MinFlashInterval = 0.05f;

        /// <summary>Flashes at/above this peak energy skip the rate limit (explosions).</summary>
        public const float BypassEnergy = 1.2f;

        /// <summary>One pooled flash. Positions are world-space X/Y floats.</summary>
        public struct Slot
        {
            public bool Active;
            public float X;
            public float Y;
            public float PeakEnergy;
            public float Radius;
            public float Life;
            public float Age;

            /// <summary>Decay factor 1 → 0 over the flash lifetime (0 when inactive).</summary>
            public float Envelope01 => Active ? LightFXState.Envelope(Age, Life) : 0f;

            /// <summary>Current brightness for the PointLight2D.</summary>
            public float Energy => PeakEnergy * Envelope01;

            /// <summary>Current light texture scale factor (lights grow slightly as they fade).</summary>
            public float RadiusScale => Active ? 1f + 0.35f * (1f - Envelope01) : 1f;
        }

        private readonly Slot[] _slots = new Slot[SlotCount];
        private float _ambientTarget = MoodTarget(false, 0);

        /// <summary>Flashes currently alive.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Smoothed ambient light energy (drives the rig's base glow).</summary>
        public float AmbientEnergy { get; private set; } = 0.9f;

        public Slot GetSlot(int index) => _slots[index];

        /// <summary>
        /// Spawns a flash. Reuses the first free slot, else overwrites the most
        /// consumed (highest age/life) slot — a new explosion always wins over
        /// a fading spark.
        /// </summary>
        public int Spawn(float x, float y, float peakEnergy, float radius, float life)
        {
            int idx = 0;
            float worst = -1f;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].Active)
                {
                    idx = i;
                    break;
                }
                float consumed = _slots[i].Age / MathF.Max(_slots[i].Life, 0.0001f);
                if (consumed > worst)
                {
                    worst = consumed;
                    idx = i;
                }
            }

            _slots[idx] = new Slot
            {
                Active = true,
                X = x,
                Y = y,
                PeakEnergy = peakEnergy,
                Radius = radius,
                Life = life < 0.01f ? 0.01f : life,
                Age = 0f,
            };
            RecomputeActiveCount();
            return idx;
        }

        /// <summary>Ages every active flash and retires the expired ones.</summary>
        public void Update(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < SlotCount; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Active) continue;
                s.Age += dt;
                if (s.Age >= s.Life) s.Active = false;
            }
            RecomputeActiveCount();
        }

        /// <summary>Sets the ambient-mood target energy (from MoodTarget policy).</summary>
        public void SetAmbientTarget(float target) => _ambientTarget = Math.Clamp(target, 0f, 2f);

        /// <summary>Exponential approach of AmbientEnergy toward its target.</summary>
        public void UpdateAmbient(float dt)
        {
            if (dt <= 0f) return;
            float k = 1f - MathF.Exp(-dt * 3f);
            AmbientEnergy += (_ambientTarget - AmbientEnergy) * k;
        }

        /// <summary>
        /// Flash energy envelope: instant attack (light snaps on), quadratic
        /// ease-out decay (strobe dies fast at first, then tails off).
        /// </summary>
        public static float Envelope(float age, float life)
        {
            if (life <= 0f || age >= life) return 0f;
            float k = 1f - (age / life);
            return k * k;
        }

        /// <summary>Deterministic flicker multiplier in [0.85, 1.15] for ambient lights.</summary>
        public static float Flicker(float time, float seed)
        {
            float a = MathF.Sin(time * 13.37f + seed);
            float b = MathF.Sin(time * 7.31f + seed * 1.7f + 1.3f);
            return 1f + 0.15f * (0.6f * a + 0.4f * b);
        }

        /// <summary>
        /// Rate-limit policy: a flash is allowed if enough time passed since the
        /// last one, or if it is big enough to bypass (explosions, boss deaths).
        /// </summary>
        public static bool AllowsFlash(float timeSinceLastFlash, float peakEnergy)
            => timeSinceLastFlash >= MinFlashInterval || peakEnergy >= BypassEnergy;

        /// <summary>Ambient-mood target: combat glow rises with threat, boss adds more.</summary>
        public static float MoodTarget(bool bossWave, int threatLevel)
        {
            float energy = 0.9f + 0.08f * Math.Min(threatLevel, 5);
            if (bossWave) energy += 0.15f;
            return Math.Clamp(energy, 0.6f, 1.4f);
        }

        /// <summary>Flash parameters per damage type (impact strobe).</summary>
        public static (float Energy, float Radius, float Life) ImpactFlash(DamageType type) => type switch
        {
            DamageType.Explosive => (1.3f, 96f, 0.28f),
            DamageType.Fire => (0.9f, 80f, 0.25f),
            DamageType.Lightning => (1.1f, 88f, 0.18f),
            DamageType.Energy => (0.8f, 72f, 0.22f),
            DamageType.Acid => (0.6f, 64f, 0.24f),
            _ => (0.7f, 68f, 0.20f),
        };

        /// <summary>Flash parameters for an environmental destruction (big, warm flare).</summary>
        public static (float Energy, float Radius, float Life) ExplosionFlash(float blastRadius)
            => (1.6f, Math.Clamp(blastRadius * 1.6f, 96f, 320f), 0.5f);

        /// <summary>Flash parameters for the hero taking damage (red pulse at the hero).</summary>
        public static (float Energy, float Radius, float Life) HeroDamageFlash()
            => (1.4f, 140f, 0.35f);

        private void RecomputeActiveCount()
        {
            int n = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i].Active) n++;
            }
            ActiveCount = n;
        }
    }
}
