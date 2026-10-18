using Godot;

namespace HeroArena
{
    /// <summary>
    /// Tracks environmental destruction count and calculates the Collateral Karma amplifier.
    /// Every 10 destructions increases ThreatLevel by 1.
    ///
    /// Destruction counting is the single source of truth in GameManagerState
    /// (RecordEnvironmentDestroyed, wired by GameManager.HandleEnvironmentDestroyed).
    /// This Node derives KarmaAmplifier from that count so the amplifier and
    /// ThreatLevel can never desync (audit P2-1). The Godot-free
    /// DestructionTracker remains for headless unit testing of the counting
    /// primitive; CollateralKarma no longer duplicates its state.
    /// </summary>
    public partial class CollateralKarma : Node
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style", "CA1822:Mark members as static",
            Justification = "Instance property kept for Node API parity and scene/script binding")]
        public int DestructionCount => GameManager.Instance?.DestructionCount ?? 0;
        public float KarmaAmplifier => ProgressionFormulas.CalcKarmaAmplifier(DestructionCount);

        public override void _Ready()
        {
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
        }

        /// <summary>Event consumer: the actual increment happens in
        /// GameManager.HandleEnvironmentDestroyed → GameManagerState.RecordEnvironmentDestroyed,
        /// the single source of truth. This handler exists to keep the
        /// EventBus subscription balanced and to mirror the destruction event
        /// into the karma layer without maintaining a second counter.</summary>
        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
        {
            // Counting is done by GameManagerState; KarmaAmplifier reads it
            // via the DestructionCount property above.
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
        }
    }
}
