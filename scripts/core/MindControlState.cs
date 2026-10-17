using System;

namespace HeroArena
{
    /// <summary>
    /// Pure, Godot-free mind-control state for enemies (Synapse Overclock).
    /// Owns the remaining-control timer and the contact-hit cooldown so the
    /// rules are headlessly testable; <see cref="EnemyBase"/> delegates 1:1
    /// (same pattern as InputBufferState / PowerupBannerSlots).
    /// </summary>
    public class MindControlState
    {
        /// <summary>Range at which a mind-controlled enemy lands a contact hit.</summary>
        public const float ContactHitRange = 40f;

        /// <summary>Seconds between contact hits — melee-style cadence, never per-frame.</summary>
        public const float ContactHitCooldown = 1f;

        public bool IsControlled { get; private set; }
        public float Remaining { get; private set; }

        private float _contactCooldown;

        /// <summary>Starts (or refreshes) mind control for <paramref name="duration"/>
        /// seconds and re-arms the contact-hit cooldown.</summary>
        public void Apply(float duration)
        {
            IsControlled = true;
            Remaining = duration;
            _contactCooldown = 0f;
        }

        /// <summary>Advances timers by <paramref name="delta"/> seconds. Returns
        /// true exactly on the tick control expires (the caller reverts its
        /// visual state); returns false otherwise.</summary>
        public bool Tick(float delta)
        {
            if (_contactCooldown > 0f) _contactCooldown -= delta;
            if (!IsControlled) return false;

            Remaining -= delta;
            if (Remaining > 0f) return false;

            IsControlled = false;
            Remaining = 0f;
            return true;
        }

        /// <summary>True exactly when a contact hit is allowed: controlled, in
        /// range, and off cooldown. Consumes the cooldown when it returns true.</summary>
        public bool TryConsumeContactHit(float distance)
        {
            if (!IsControlled) return false;
            if (distance > ContactHitRange) return false;
            if (_contactCooldown > 0f) return false;

            _contactCooldown = ContactHitCooldown;
            return true;
        }
    }
}
