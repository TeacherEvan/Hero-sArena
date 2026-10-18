namespace HeroArena
{
    /// <summary>
    /// Configuration data for the arena's ambient wildlife simulation:
    /// pool size, timing, movement constants, and the per-species profile
    /// struct. Kept separate from <see cref="WildlifeDirectorState"/> so the
    /// data layer is easy to tune without touching the simulation logic.
    /// </summary>
    public static class WildlifeConfig
    {
        /// <summary>Fixed critter pool size. The arena is a stage, not a zoo.</summary>
        public const int MaxCritters = 64;

        /// <summary>Seconds between spawn fades while a deficit exists.</summary>
        public const float SpawnInterval = 0.35f;

        /// <summary>Fade durations for spawning and despawning critters.</summary>
        public const float FadeInSeconds = 1.6f;
        public const float FadeOutSeconds = 1.2f;

        /// <summary>Margin from the arena edges that spawns and motion respect.</summary>
        public const float EdgeMargin = 120f;

        /// <summary>Critters are not spawned within this radius of the hero.</summary>
        public const float HeroSpawnClearance = 220f;

        /// <summary>Distance the curious keep from the hero (stand-off, then back off).</summary>
        public const float CuriosityStandoff = 140f;

        /// <summary>Species with flee sensitivity at/below this approach the idle hero.</summary>
        public const float CuriosityBoldnessThreshold = 0.5f;

        /// <summary>Radius around home within which wander targets are picked.</summary>
        public const float WanderRadius = 180f;

        /// <summary>Critters push apart when closer than this (soft separation, px).</summary>
        public const float SeparationRadius = 26f;

        /// <summary>How firmly separation steers a critter (px/s at contact).</summary>
        public const float SeparationWeight = 26f;

        /// <summary>Wind influence on panicked critters (barely notice the wind).</summary>
        public const float WindPanicFactor = 0.1f;

        /// <summary>Wind influence on curious/wandering critters.</summary>
        public const float WindWanderFactor = 0.3f;

        /// <summary>Curiosity back-off speed fraction of cruise speed.</summary>
        public const float CuriosityBackOffSpeed = 0.5f;

        /// <summary>Curiosity max speed multiplier.</summary>
        public const float CuriositySpeedMult = 1.5f;

        /// <summary>Wander max speed multiplier.</summary>
        public const float WanderSpeedMult = 1.4f;

        /// <summary>Wander target arrival threshold (squared px).</summary>
        public const float WanderArrivalDistSq = 14f * 14f;

        /// <summary>Steering damping rate (per-second, scaled by dt * 60 for frame independence).</summary>
        public const float SteerDampingRate = 6f;

        /// <summary>Firefly flash duty cycle: fraction of the period spent flashing.</summary>
        public const float FlashDuty = 0.06f;

        /// <summary>Firefly flash period in seconds (research: real fireflies flash every ~2–7 s).</summary>
        public const float FlashPeriod = 4.4f;

        /// <summary>Dim baseline so unlit fireflies are still faintly trackable.</summary>
        public const float FlashDimBase = 0.1f;

        /// <summary>Wander targets at or above this scar strength are re-rolled.</summary>
        public const float ScarAvoidThreshold = 0.35f;

        /// <summary>Max wander-target re-rolls under scar pressure.</summary>
        public const int ScarAttempts = 4;

        /// <summary>
        /// Per-species parameters. Biomes pick 1–2 of these (see
        /// <see cref="BiomeAtmosphereState"/>); the distinction is purely data.
        /// </summary>
        public struct SpeciesProfile
        {
            public string Name;
            public float Speed;             // wander cruise speed, px/s
            public float PanicSpeed;        // flee speed, px/s
            public float Size;              // draw scale factor for the node
            public float Glow01;            // additive glow strength for the node
            public float Height01;          // 0 = ground skitter, 1 = air drift
            public float FleeSensitivity01; // scales the panic radius they notice
            public float AccentR;           // accent/glow colour (0..1) for the node
            public float AccentG;
            public float AccentB;
        }
    }
}
