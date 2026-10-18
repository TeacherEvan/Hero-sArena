using System;

namespace HeroArena
{
    /// <summary>
    /// Pure C# procedural animation and juice calculation for game characters.
    /// Manages walk-bobbing, tilt angles, dash squash-and-stretch, and attack recoil.
    /// Strictly zero heap allocations inside update loops.
    /// </summary>
    public sealed class CharacterJuiceState
    {
        // Spring constants for returning to baseline (1, 1)
        public const float SpringStiffness = 180f;
        public const float SpringDamping = 18f;

        public float ScaleX { get; private set; } = 1f;
        public float ScaleY { get; private set; } = 1f;
        public float WalkBob { get; private set; } = 0f;
        public float TiltAngle { get; private set; } = 0f;

        private float _walkPhase;
        private float _velScaleX;
        private float _velScaleY;

        /// <summary>
        /// Updates the animation juice state over delta time.
        /// </summary>
        /// <param name="dt">Frame delta time in seconds.</param>
        /// <param name="isMoving">Whether character is currently moving.</param>
        /// <param name="speedRatio">Movement speed normalized [0, 1].</param>
        public void Update(float dt, bool isMoving, float speedRatio = 1f)
        {
            if (dt <= 0f) return;
            // Cap delta time to prevent explosion on severe lag spikes
            if (dt > 0.1f) dt = 0.1f;

            if (isMoving && speedRatio > 0.05f)
            {
                _walkPhase += dt * 14f * MathF.Min(speedRatio, 1.5f);
                WalkBob = MathF.Sin(_walkPhase) * 2.2f;
                TiltAngle = MathF.Cos(_walkPhase * 0.5f) * 0.08f;
            }
            else
            {
                // Smoothly decay walk bob and tilt back to zero
                WalkBob = MathF.Max(0f, WalkBob - dt * 12f);
                TiltAngle *= MathF.Max(0f, 1f - dt * 14f);
            }

            // Spring physics update for ScaleX and ScaleY toward target 1.0f
            UpdateSpring(ref _velScaleX, ref _scaleX, 1f, dt);
            UpdateSpring(ref _velScaleY, ref _scaleY, 1f, dt);

            // Clamp scales within reasonable bounds to prevent distortion glitches
            ScaleX = Math.Clamp(_scaleX, 0.4f, 2.2f);
            ScaleY = Math.Clamp(_scaleY, 0.4f, 2.2f);
        }

        private float _scaleX = 1f;
        private float _scaleY = 1f;

        /// <summary>
        /// Applies squash-and-stretch deformation for a dodge or sprint dash.
        /// Elongates along forward axis (X) and compresses transverse axis (Y).
        /// </summary>
        public void ApplyDashStretch(float stretchFactor = 1.35f, float squashFactor = 0.72f)
        {
            _scaleX = stretchFactor;
            _scaleY = squashFactor;
            _velScaleX = 0f;
            _velScaleY = 0f;
            ScaleX = _scaleX;
            ScaleY = _scaleY;
        }

        /// <summary>
        /// Applies recoil kickback deformation when attacking or firing weapons.
        /// Compresses along forward axis (X) and expands transverse axis (Y).
        /// </summary>
        public void ApplyAttackRecoil(float compressFactor = 0.86f, float expandFactor = 1.14f)
        {
            _scaleX = compressFactor;
            _scaleY = expandFactor;
            _velScaleX = 0f;
            _velScaleY = 0f;
            ScaleX = _scaleX;
            ScaleY = _scaleY;
        }

        /// <summary>
        /// Applies impact flinch deformation when taking damage.
        /// </summary>
        public void ApplyImpactFlinch()
        {
            _scaleX = 1.15f;
            _scaleY = 1.15f;
            _velScaleX = -2f;
            _velScaleY = -2f;
            ScaleX = _scaleX;
            ScaleY = _scaleY;
        }

        public void Reset()
        {
            _scaleX = 1f;
            _scaleY = 1f;
            _velScaleX = 0f;
            _velScaleY = 0f;
            _walkPhase = 0f;
            ScaleX = 1f;
            ScaleY = 1f;
            WalkBob = 0f;
            TiltAngle = 0f;
        }

        private static void UpdateSpring(ref float velocity, ref float position, float target, float dt)
        {
            float force = -SpringStiffness * (position - target) - SpringDamping * velocity;
            velocity += force * dt;
            position += velocity * dt;
        }
    }
}
