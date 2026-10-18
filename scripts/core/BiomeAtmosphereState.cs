using System;

namespace HeroArena
{
    /// <summary>One of the five arena environments (resolved from the map scene path).</summary>
    public enum BiomeKind : byte
    {
        RuinedMetropolis,
        VolcanoCrater,
        OrbitalPlatform,
        SubterraneanLab,
        AlienWasteland,
    }

    /// <summary>Signatures for the code-generated ambient particle fields.</summary>
    public enum AmbientFieldKind : byte
    {
        NeonRain,
        AshFall,
        RisingEmbers,
        StarDust,
        Spores,
        DustHaze,
    }

    /// <summary>
    /// Godot-free identity table for the five arena biomes: each map scene
    /// gets a signature ambience (particle weather, scene tint, wind scale)
    /// and a wildlife roster. The node layer
    /// (<c>scripts/vfx/BiomeAtmosphere.cs</c>, <c>AmbientWildlife.cs</c>,
    /// <c>LivingGround.cs</c>) resolves the biome once from the loaded map's
    /// scene path, then configures everything from the single profile — so
    /// tripling the map count later costs one table row, not three systems.
    ///
    /// Profile values are all plain floats/ints so the xUnit suite can pin
    /// their sanity ranges headlessly (BiomeAtmosphereStateTests).
    /// </summary>
    public static class BiomeAtmosphereState
    {
        /// <summary>Wave/threat ceiling used by <see cref="FieldIntensity"/> growth.</summary>
        public const float MaxFieldIntensity = 1.5f;

        /// <summary>Multiplier applied to field intensity while a boss wave holds its breath.</summary>
        public const float BossStormMultiplier = 1.35f;

        /// <summary>Description of one code-generated ambient particle field.</summary>
        public struct ParticleField
        {
            public AmbientFieldKind Kind;
            public int Amount;
            public float R;
            public float G;
            public float B;
            public float A;
            public float SpeedMin;      // initial speed, px/s
            public float SpeedMax;
            public float SizeScale;     // texture scale multiplier
            public float Lifetime;      // seconds
            public float GravityY;      // px/s^2; negative rises
            public float WindInfluence01; // 0..1 how much the wind slants/drifts it
        }

        /// <summary>Complete identity of an arena biome.</summary>
        public struct BiomeProfile
        {
            public BiomeKind Kind;
            public string DisplayName;
            public float TintR;         // scene tint the ground/veil mixes in
            public float TintG;
            public float TintB;
            public float WindScale;     // scales AmbientWindState for this biome
            public int WildlifePopulation;
            public WildlifeConfig.SpeciesProfile PrimaryCritter;
            public WildlifeConfig.SpeciesProfile SecondaryCritter;
            public ParticleField PrimaryField;
            public ParticleField SecondaryField;
        }

        private static readonly BiomeProfile RuinedMetropolis = new BiomeProfile
        {
            Kind = BiomeKind.RuinedMetropolis,
            DisplayName = "Ruined Metropolis",
            TintR = 0.55f,
            TintG = 0.62f,
            TintB = 0.78f,
            WindScale = 1.0f,
            WildlifePopulation = 26,
            PrimaryCritter = Critter("NeonMoth", r: 0.45f, g: 0.9f, b: 1f, speed: 34f, panic: 150f, size: 0.5f, glow: 0.85f, height: 0.7f, flee: 0.35f),
            SecondaryCritter = Critter("StreetSkitter", r: 0.85f, g: 0.62f, b: 0.38f, speed: 26f, panic: 120f, size: 0.4f, glow: 0.0f, height: 0.1f, flee: 0.6f),
            PrimaryField = Field(AmbientFieldKind.NeonRain, amount: 90, r: 0.45f, g: 0.62f, b: 0.95f, a: 0.5f,
                speedMin: 380f, speedMax: 560f, size: 0.35f, lifetime: 1.6f, gravityY: 900f, wind: 0.55f),
            SecondaryField = Field(AmbientFieldKind.DustHaze, amount: 30, r: 0.5f, g: 0.52f, b: 0.7f, a: 0.16f,
                speedMin: 8f, speedMax: 24f, size: 1.2f, lifetime: 8f, gravityY: 0f, wind: 1f),
        };

        private static readonly BiomeProfile VolcanoCrater = new BiomeProfile
        {
            Kind = BiomeKind.VolcanoCrater,
            DisplayName = "Volcano Crater",
            TintR = 0.72f,
            TintG = 0.45f,
            TintB = 0.35f,
            WindScale = 1.15f,
            WildlifePopulation = 18,
            PrimaryCritter = Critter("EmberMoth", r: 1f, g: 0.55f, b: 0.22f, speed: 40f, panic: 170f, size: 0.45f, glow: 1f, height: 0.8f, flee: 0.3f),
            SecondaryCritter = Critter("AshCrawler", r: 0.72f, g: 0.38f, b: 0.3f, speed: 22f, panic: 100f, size: 0.5f, glow: 0.15f, height: 0.05f, flee: 0.85f),
            PrimaryField = Field(AmbientFieldKind.AshFall, amount: 110, r: 0.62f, g: 0.58f, b: 0.55f, a: 0.5f,
                speedMin: 60f, speedMax: 120f, size: 0.3f, lifetime: 4f, gravityY: 60f, wind: 0.9f),
            SecondaryField = Field(AmbientFieldKind.RisingEmbers, amount: 34, r: 1f, g: 0.55f, b: 0.2f, a: 0.9f,
                speedMin: 30f, speedMax: 80f, size: 0.28f, lifetime: 2.8f, gravityY: -40f, wind: 0.5f),
        };

        private static readonly BiomeProfile OrbitalPlatform = new BiomeProfile
        {
            Kind = BiomeKind.OrbitalPlatform,
            DisplayName = "Orbital Platform",
            TintR = 0.6f,
            TintG = 0.7f,
            TintB = 0.9f,
            WindScale = 0.85f,
            WildlifePopulation = 24,
            PrimaryCritter = Critter("StarMote", r: 0.85f, g: 0.92f, b: 1f, speed: 20f, panic: 80f, size: 0.4f, glow: 0.9f, height: 1f, flee: 0.25f),
            SecondaryCritter = Critter("HullSkitter", r: 0.72f, g: 0.84f, b: 0.95f, speed: 30f, panic: 140f, size: 0.35f, glow: 0.05f, height: 0.05f, flee: 0.5f),
            PrimaryField = Field(AmbientFieldKind.StarDust, amount: 70, r: 0.75f, g: 0.85f, b: 1f, a: 0.5f,
                speedMin: 4f, speedMax: 18f, size: 0.22f, lifetime: 9f, gravityY: 0f, wind: 1.2f),
            SecondaryField = Field(AmbientFieldKind.StarDust, amount: 28, r: 0.55f, g: 0.7f, b: 1f, a: 0.22f,
                speedMin: 2f, speedMax: 10f, size: 0.6f, lifetime: 12f, gravityY: 0f, wind: 1.2f),
        };

        private static readonly BiomeProfile SubterraneanLab = new BiomeProfile
        {
            Kind = BiomeKind.SubterraneanLab,
            DisplayName = "Subterranean Lab",
            TintR = 0.55f,
            TintG = 0.75f,
            TintB = 0.62f,
            WindScale = 0.7f,
            WildlifePopulation = 30,
            PrimaryCritter = Critter("SporeMite", r: 0.55f, g: 1f, b: 0.65f, speed: 24f, panic: 110f, size: 0.35f, glow: 0.7f, height: 0.6f, flee: 0.45f),
            SecondaryCritter = Critter("VentCrawler", r: 0.78f, g: 0.82f, b: 0.42f, speed: 42f, panic: 130f, size: 0.4f, glow: 0f, height: 0f, flee: 0.55f),
            PrimaryField = Field(AmbientFieldKind.Spores, amount: 80, r: 0.55f, g: 0.85f, b: 0.6f, a: 0.42f,
                speedMin: 6f, speedMax: 20f, size: 0.3f, lifetime: 7f, gravityY: -6f, wind: 1f),
            SecondaryField = Field(AmbientFieldKind.Spores, amount: 40, r: 0.4f, g: 0.7f, b: 0.5f, a: 0.25f,
                speedMin: 3f, speedMax: 12f, size: 0.5f, lifetime: 9f, gravityY: -3f, wind: 1f),
        };

        private static readonly BiomeProfile AlienWasteland = new BiomeProfile
        {
            Kind = BiomeKind.AlienWasteland,
            DisplayName = "Alien Wasteland",
            TintR = 0.75f,
            TintG = 0.68f,
            TintB = 0.5f,
            WindScale = 1.4f,
            WildlifePopulation = 20,
            PrimaryCritter = Critter("DustHopper", r: 1f, g: 0.78f, b: 0.45f, speed: 55f, panic: 180f, size: 0.5f, glow: 0.1f, height: 0.15f, flee: 0.75f),
            SecondaryCritter = Critter("WastelandGnat", r: 0.95f, g: 0.9f, b: 0.55f, speed: 30f, panic: 140f, size: 0.3f, glow: 0.35f, height: 0.5f, flee: 0.4f),
            PrimaryField = Field(AmbientFieldKind.DustHaze, amount: 100, r: 0.75f, g: 0.65f, b: 0.45f, a: 0.35f,
                speedMin: 30f, speedMax: 80f, size: 0.5f, lifetime: 5f, gravityY: 8f, wind: 1.6f),
            SecondaryField = Field(AmbientFieldKind.DustHaze, amount: 30, r: 0.7f, g: 0.6f, b: 0.4f, a: 0.2f,
                speedMin: 10f, speedMax: 40f, size: 1.4f, lifetime: 8f, gravityY: 4f, wind: 1.6f),
        };

        /// <summary>
        /// Resolves the biome from a map scene path (e.g.
        /// "res://scenes/maps/VolcanoCrater.tscn"). Unknown or empty paths fall
        /// back to the default home arena, RuinedMetropolis — matching today's
        /// Main.tscn wiring, so unlisted future maps still run.
        /// </summary>
        public static BiomeKind BiomeForMapPath(string? sceneFilePath)
        {
            if (string.IsNullOrWhiteSpace(sceneFilePath)) return BiomeKind.RuinedMetropolis;

            int slash = sceneFilePath!.LastIndexOf('/');
            string stem = slash >= 0 ? sceneFilePath.Substring(slash + 1) : sceneFilePath;
            int dot = stem.LastIndexOf('.');
            if (dot > 0) stem = stem.Substring(0, dot);

            return stem.ToLowerInvariant() switch
            {
                "ruinedmetropolis" => BiomeKind.RuinedMetropolis,
                "volcanocrater" => BiomeKind.VolcanoCrater,
                "orbitalplatform" => BiomeKind.OrbitalPlatform,
                "subterraneanlab" => BiomeKind.SubterraneanLab,
                "alienwasteland" => BiomeKind.AlienWasteland,
                _ => BiomeKind.RuinedMetropolis,
            };
        }

        /// <summary>Looks up the full profile for a biome.</summary>
        public static BiomeProfile ProfileFor(BiomeKind kind) => kind switch
        {
            BiomeKind.VolcanoCrater => VolcanoCrater,
            BiomeKind.OrbitalPlatform => OrbitalPlatform,
            BiomeKind.SubterraneanLab => SubterraneanLab,
            BiomeKind.AlienWasteland => AlienWasteland,
            _ => RuinedMetropolis,
        };

        /// <summary>
        /// Ambient weather intensity for the current run: the air thickens as
        /// the waves and Collateral-Karma threat pile up, capped by
        /// <see cref="MaxFieldIntensity"/>.
        /// </summary>
        public static float FieldIntensity(int wave, int threatLevel)
        {
            float v = 0.85f + 0.05f * Math.Clamp(wave, 0, 10) + 0.05f * Math.Clamp(threatLevel, 0, 5);
            return Math.Clamp(v, 0.8f, MaxFieldIntensity);
        }

        private static ParticleField Field(
            AmbientFieldKind kind, int amount, float r, float g, float b, float a,
            float speedMin, float speedMax, float size, float lifetime, float gravityY, float wind)
            => new ParticleField
            {
                Kind = kind,
                Amount = amount,
                R = r,
                G = g,
                B = b,
                A = a,
                SpeedMin = speedMin,
                SpeedMax = speedMax,
                SizeScale = size,
                Lifetime = lifetime,
                GravityY = gravityY,
                WindInfluence01 = wind,
            };

        private static WildlifeConfig.SpeciesProfile Critter(
            string name, float r, float g, float b,
            float speed, float panic, float size, float glow, float height, float flee)
            => new WildlifeConfig.SpeciesProfile
            {
                Name = name,
                AccentR = r,
                AccentG = g,
                AccentB = b,
                Speed = speed,
                PanicSpeed = panic,
                Size = size,
                Glow01 = glow,
                Height01 = height,
                FleeSensitivity01 = flee,
            };
    }
}
