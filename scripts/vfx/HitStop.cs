using Godot;

namespace HeroArena
{
    /// <summary>
    /// Hit-stop system. Sets Engine.TimeScale briefly to create impactful freeze frames.
    /// </summary>
    public partial class HitStop : Node
    {
        private double _originalTimeScale = 1.0;
        private float _remaining = 0f;
        private bool _active = false;

        public void TriggerHitStop(float duration = 0.07f, double frozenTimeScale = 0.05)
        {
            if (_active) return;
            _originalTimeScale = Engine.TimeScale;
            Engine.TimeScale = frozenTimeScale;
            _remaining = duration;
            _active = true;
        }

        public override void _Process(double delta)
        {
            if (!_active) return;
            // Use real (unscaled) delta to count down
            _remaining -= (float)(delta / Engine.TimeScale);
            if (_remaining <= 0f)
            {
                Engine.TimeScale = _originalTimeScale;
                _active = false;
            }
        }
    }
}
