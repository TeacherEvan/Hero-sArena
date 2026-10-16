namespace HeroArena
{
    /// <summary>
    /// Collateral Karma counting state, extracted from the CollateralKarma
    /// Node so xUnit can cover the destruction-counting behavior headlessly
    /// (previously it needed the SceneTree and a live EventBus). The Node
    /// subscribes to OnEnvironmentDestroyed and forwards events here; the
    /// amplifier delegates to ProgressionFormulas, unchanged.
    /// </summary>
    public class DestructionTracker
    {
        public int DestructionCount { get; private set; } = 0;

        public float KarmaAmplifier => ProgressionFormulas.CalcKarmaAmplifier(DestructionCount);

        /// <summary>Records one environmental destruction.</summary>
        public void Record() => DestructionCount++;

        public void Reset() => DestructionCount = 0;
    }
}
