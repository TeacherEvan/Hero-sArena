using Godot;

namespace HeroArena
{
    /// <summary>
    /// Tracks environmental destruction count and calculates the Collateral Karma amplifier.
    /// Every 10 destructions increases ThreatLevel by 1.
    ///
    /// The counting state lives in the Godot-free <see cref="DestructionTracker"/>
    /// (covered headlessly in DestructionTrackerTests); this Node only wires the
    /// EventBus subscription.
    /// </summary>
    public partial class CollateralKarma : Node
    {
        private readonly DestructionTracker _tracker = new();

        public int DestructionCount => _tracker.DestructionCount;
        public float KarmaAmplifier => _tracker.KarmaAmplifier;

        public override void _Ready()
        {
            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
        }

        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
        {
            _tracker.Record();
            // Threat level escalation is handled centrally by GameManager
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
        }
    }
}
