using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free state for the ScreenDistortionFX CanvasLayer
    /// (scripts/vfx/ScreenDistortionFX.cs): a fixed-capacity ripple buffer
    /// that feeds the screen_distortion.gdshader — refraction rings that
    /// make explosions look like they are bending the air — plus the karma
    /// vignette (threat-driven baseline + hero-damage pulse).
    ///
    /// Extracted per the AGENTS.md pattern so the xUnit suite covers the
    /// ring buffer, rate limiting / duplicate coalescing and vignette math
    /// headlessly; the Node wrapper owns only the
    /// CanvasLayer/ColorRect/ShaderMaterial plumbing and world→UV mapping.
    /// </summary>
    public sealed class ScreenRippleState
    {
        /// <summary>Simultaneous ripples the shader uniform carries.</summary>
        public const int MaxRipples = 8;

        /// <summary>Minimum seconds between non-bypass ripples.</summary>
        public const float MinAddInterval = 0.045f;

        /// <summary>Ripples at/above this strength skip the rate limit (explosions).</summary>
        public const float BypassStrength = 0.5f;

        /// <summary>UV distance within which a fresh ripple counts as a duplicate.</summary>
        public const float DuplicateRadius = 0.08f;

        /// <summary>Age below which an existing ripple can suppress a duplicate add.</summary>
        public const float DuplicateMaxAge = 0.12f;

        /// <summary>One screen ripple in UV space (0..1 across the viewport).</summary>
        public struct Ripple
        {
            public bool Active;
            public float U;
            public float V;
            public float Strength;
            public float Radius01;
            public float Life;
            public float Age;

            public float Progress01 => Life <= 0f ? 1f : Math.Clamp(Age / Life, 0f, 1f);

            /// <summary>Wave amplitude for the shader (quadratic ease-out decay).</summary>
            public float Amplitude => Strength * (1f - Progress01) * (1f - Progress01);

            /// <summary>Expanding ring radius in UV units (ease-out expansion).</summary>
            public float RingRadius01
            {
                get
                {
                    float p = Progress01;
                    float easeOut = 1f - (1f - p) * (1f - p);
                    return Radius01 * easeOut;
                }
            }
        }

        private readonly Ripple[] _ripples = new Ripple[MaxRipples];
        private float _vignetteTarget;

        /// <summary>Active ripples currently in the buffer.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Threat-driven vignette baseline, smoothed toward its target.</summary>
        public float BaseVignette { get; private set; }

        /// <summary>Short-lived extra vignette from hero damage; decays to 0.</summary>
        public float VignetteBoost { get; private set; }

        /// <summary>Vignette value to feed the shader (baseline + pulse, clamped).</summary>
        public float Vignette => Math.Clamp(BaseVignette + VignetteBoost, 0f, 1f);

        /// <summary>
        /// True while anything still needs per-frame work (ripples alive, pulse
        /// decaying, or baseline still moving toward its target). When false the
        /// Node can stop processing (and hide the overlay once ShouldRender
        /// also goes false).
        /// </summary>
        public bool AnyWork =>
            ActiveCount > 0
            || VignetteBoost > 0.001f
            || MathF.Abs(BaseVignette - _vignetteTarget) > 0.002f;

        /// <summary>Vignette values at/below this are visually negligible.</summary>
        public const float VignetteHideThreshold = 0.004f;

        /// <summary>
        /// True while the overlay must stay visible: per-frame work remains, or
        /// the settled karma vignette is still showing. The vignette is a
        /// persistent mood, so it must not pop off screen the moment it stops
        /// moving. When false the Node hides the overlay entirely.
        /// </summary>
        public bool ShouldRender => AnyWork || Vignette > VignetteHideThreshold;

        public Ripple GetRipple(int index) => _ripples[index];

        /// <summary>
        /// Tries to add a ripple. Big ripples bypass the rate limit and the
        /// duplicate check; small ones are suppressed when the last add was too
        /// recent or when an equally-strong fresh ripple already sits at the
        /// same spot (impact spam on one enemy collapses to a single ring).
        /// </summary>
        public bool TryAdd(float u, float v, float strength, float radius01, float life, float timeSinceLastAdd)
        {
            bool bypass = strength >= BypassStrength;
            if (!bypass)
            {
                if (timeSinceLastAdd < MinAddInterval) return false;
                for (int i = 0; i < MaxRipples; i++)
                {
                    ref readonly var r = ref _ripples[i];
                    if (!r.Active || r.Age > DuplicateMaxAge) continue;
                    float du = r.U - u;
                    float dv = r.V - v;
                    if (du * du + dv * dv < DuplicateRadius * DuplicateRadius && r.Strength >= strength)
                        return false;
                }
            }

            int idx = -1;
            float worst = -1f;
            for (int i = 0; i < MaxRipples; i++)
            {
                if (!_ripples[i].Active)
                {
                    idx = i;
                    break;
                }
                float consumed = _ripples[i].Age / MathF.Max(_ripples[i].Life, 0.0001f);
                if (consumed > worst)
                {
                    worst = consumed;
                    idx = i;
                }
            }

            _ripples[idx] = new Ripple
            {
                Active = true,
                U = u,
                V = v,
                Strength = Math.Clamp(strength, 0f, 1f),
                Radius01 = Math.Clamp(radius01, 0.01f, 1.5f),
                Life = life < 0.05f ? 0.05f : life,
                Age = 0f,
            };
            RecomputeActiveCount();
            return true;
        }

        /// <summary>Ages ripples, the vignette baseline and the damage pulse.</summary>
        public void Update(float dt)
        {
            if (dt <= 0f) return;

            for (int i = 0; i < MaxRipples; i++)
            {
                ref var r = ref _ripples[i];
                if (!r.Active) continue;
                r.Age += dt;
                if (r.Age >= r.Life) r.Active = false;
            }
            RecomputeActiveCount();

            float k = 1f - MathF.Exp(-dt * 4f);
            BaseVignette += (_vignetteTarget - BaseVignette) * k;

            VignetteBoost *= MathF.Exp(-dt * 1.8f);
            if (VignetteBoost < 0.001f) VignetteBoost = 0f;
        }

        /// <summary>Sets the vignette baseline target (from GameManager threat level).</summary>
        public void SetVignetteTarget(float target) => _vignetteTarget = Math.Clamp(target, 0f, 1f);

        /// <summary>Adds a transient vignette pulse (hero damage flash).</summary>
        public void PulseVignette(float amount)
            => VignetteBoost = Math.Clamp(VignetteBoost + amount, 0f, 1f);

        /// <summary>Saturation curve for the karma vignette at a given threat level.</summary>
        public static float ThreatVignette(int threatLevel)
        {
            if (threatLevel <= 0) return 0f;
            float v = 0.45f * (1f - MathF.Exp(-threatLevel / 6f));
            return Math.Clamp(v, 0f, 0.45f);
        }

        private void RecomputeActiveCount()
        {
            int n = 0;
            for (int i = 0; i < MaxRipples; i++)
            {
                if (_ripples[i].Active) n++;
            }
            ActiveCount = n;
        }
    }
}
