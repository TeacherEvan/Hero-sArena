using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free scar memory for the arena floor (R-LA6, "the ground keeps
    /// history"): a fixed pool of fading stamps — hero footprints, blast
    /// cracks and scorch patches — plus <see cref="SampleScar"/>, a sampler
    /// answering "how scarred is this spot?" (0 = clean .. 1 = scarred) for
    /// the wildlife director's avoidance and the node's draw pass.
    ///
    /// Stamps age out deterministically; when the pool is full the least
    /// visible stamp is replaced, so memory is bounded and the same inputs
    /// always produce the same field. Pure class: no Godot types, no RNG,
    /// fully covered by DisturbanceMemoryStateTests.
    /// </summary>
    public sealed class DisturbanceMemoryState
    {
        public const int DefaultCapacity = 96;

        /// <summary>Footprint: small, short-lived.</summary>
        public const float PrintLifetime = 30f;
        /// <summary>Crack from a heavy impact: medium, lingers.</summary>
        public const float CrackLifetime = 75f;
        /// <summary>Scorch from a blast: large, fades slow.</summary>
        public const float ScorchLifetime = 150f;

        /// <summary>Fade-in time for a fresh stamp, seconds.</summary>
        public const float FadeInSeconds = 1.2f;

        public enum ScarKind : byte { Print, Crack, Scorch }

        public struct Scar
        {
            public bool Active;
            public ScarKind Kind;
            public float X, Y;
            public float Radius;
            public float Strength01;
            public float Age;
            public float Lifetime;
            public float Heading; // radians; print orientation / crack fan axis
        }

        private readonly Scar[] _scars;
        private int _activeCount;

        public DisturbanceMemoryState(int capacity = DefaultCapacity)
        {
            _scars = new Scar[Math.Max(8, capacity)];
        }

        /// <summary>Currently active (not yet aged out) stamps.</summary>
        public int ActiveCount => _activeCount;

        /// <summary>
        /// Adds a stamp; returns its slot index. When the pool is full,
        /// replaces the stamp with the lowest remaining visibility
        /// (strength × envelope), ties broken by lowest index.
        /// </summary>
        public int Stamp(ScarKind kind, float x, float y, float radius, float strength01, float heading = 0f)
        {
            if (radius <= 0f) radius = 1f;
            float strength = Math.Clamp(strength01, 0f, 1f);
            float lifetime = LifetimeFor(kind);

            int best = -1;
            float worstVisibility = float.MaxValue;
            for (int i = 0; i < _scars.Length; i++)
            {
                var s = _scars[i];
                if (!s.Active)
                {
                    best = i; // first free slot wins
                    break;
                }
                float visibility = s.Strength01 * EnvelopeFor(s.Age, s.Lifetime);
                if (visibility < worstVisibility)
                {
                    worstVisibility = visibility;
                    best = i;
                }
            }

            ref var slot = ref _scars[best];
            if (!slot.Active) _activeCount++;
            slot = new Scar
            {
                Active = true,
                Kind = kind,
                X = x,
                Y = y,
                Radius = radius,
                Strength01 = strength,
                Age = 0f,
                Lifetime = lifetime,
                Heading = heading,
            };
            return best;
        }

        public void Update(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _scars.Length; i++)
            {
                ref var s = ref _scars[i];
                if (!s.Active) continue;
                s.Age += dt;
                if (s.Age >= s.Lifetime)
                {
                    s.Active = false;
                    _activeCount--;
                }
            }
        }

        /// <summary>
        /// Scar strength at a world point, 0 (clean) to 1 (scarred), summed
        /// over all covering stamps and clamped.
        /// </summary>
        public float SampleScar(float x, float y)
        {
            float sum = 0f;
            for (int i = 0; i < _scars.Length; i++)
            {
                var s = _scars[i];
                if (!s.Active) continue;
                float dx = x - s.X;
                float dy = y - s.Y;
                float d2 = dx * dx + dy * dy;
                float r2 = s.Radius * s.Radius;
                if (d2 >= r2) continue;
                float falloff = 1f - d2 / r2;
                sum += s.Strength01 * EnvelopeFor(s.Age, s.Lifetime) * falloff;
                if (sum >= 1f) return 1f;
            }
            return Math.Clamp(sum, 0f, 1f);
        }

        /// <summary>Read-only snapshot of slot <paramref name="index"/>.</summary>
        public Scar GetScar(int index) => _scars[index];

        public static float LifetimeFor(ScarKind kind) => kind switch
        {
            ScarKind.Print => PrintLifetime,
            ScarKind.Crack => CrackLifetime,
            ScarKind.Scorch => ScorchLifetime,
            _ => PrintLifetime,
        };

        /// <summary>
        /// Visibility envelope of a stamp: fades in over
        /// <see cref="FadeInSeconds"/>, holds, then fades out over the last
        /// 30 % of its lifetime.
        /// </summary>
        public static float EnvelopeFor(float age, float lifetime)
        {
            if (lifetime <= 0f || age <= 0f || age >= lifetime) return 0f;
            float fadeIn = Smooth01(Math.Clamp(age / FadeInSeconds, 0f, 1f));
            float fadeOut = Smooth01(Math.Clamp((lifetime - age) / (lifetime * 0.3f), 0f, 1f));
            return MathF.Min(fadeIn, fadeOut);
        }

        private static float Smooth01(float t) => t * t * (3f - 2f * t);
    }
}
