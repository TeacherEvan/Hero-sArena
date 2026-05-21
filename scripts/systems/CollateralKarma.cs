using Godot;

namespace HeroArena
{
    /// <summary>
    /// Tracks environmental destruction count and calculates the Collateral Karma amplifier.
    /// Every 10 destructions increases ThreatLevel by 1.
    /// </summary>
    public partial class CollateralKarma : Node
    {
        public int DestructionCount { get; private set; } = 0;
        public float KarmaAmplifier => Mathf.Log(Mathf.E + 0.05f * DestructionCount);

        public override void _Ready()
        {
            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
        }

        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
        {
            DestructionCount++;
            int newThreat = DestructionCount / 10;
            GameManager.Instance.SetThreatLevel(newThreat);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
        }
    }
}
