using Godot;

namespace HeroArena
{
    /// <summary>
    /// Hit-stop system. Sets Engine.TimeScale briefly to create impactful freeze frames.
    /// Stacks durations if re-triggered while active. Only restores the time scale if
    /// it still matches the captured frozen value (so a concurrent Zephyr or pause
    /// change is not clobbered on expiry).
    /// </summary>
    public partial class HitStop : Node
    {
        private double _originalTimeScale = 1.0;
        private double _frozenTimeScale = 0.05;
        private float _remaining = 0f;
        private bool _active = false;

        public void TriggerHitStop(float duration = 0.07f, double frozenTimeScale = 0.05)
        {
            if (!_active)
            {
                _originalTimeScale = Engine.TimeScale;
                _frozenTimeScale = frozenTimeScale;
                Engine.TimeScale = frozenTimeScale;
                _active = true;
            }
            // Stack durations rather than dropping the new request on the floor.
            _remaining += duration;
        }

        public override void _Process(double delta)
        {
            if (!_active) return;
            // Use real (unscaled) delta to count down: guard against near-zero TimeScale
            double safeScale = System.Math.Max(Engine.TimeScale, 1e-6);
            _remaining -= (float)(delta / safeScale);
            if (_remaining <= 0f)
            {
                // Only restore if no other system has changed TimeScale since we froze it
                // (e.g. Zephyr's HyperDash or a pause menu that resumed mid-stop).
                if (System.Math.Abs(Engine.TimeScale - _frozenTimeScale) < 1e-3)
                    Engine.TimeScale = _originalTimeScale;
                _active = false;
            }
        }
    }
}
