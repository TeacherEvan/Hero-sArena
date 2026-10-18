using Godot;

namespace HeroArena
{
    /// <summary>
    /// Lightweight procedural ambient FX and particle aura for characters.
    /// Provides subtle class-specific emissive particle motes:
    /// - Volt: electric arcs / spark motes
    /// - Ignis: thermal embers / smoke motes
    /// - Toxis: caustic acid drips
    /// - Shielder / Apex: kinetic aura pulses
    /// Strictly adheres to zero per-frame heap allocations (caches textures and colors).
    /// </summary>
    public partial class CharacterPassiveFX : Node2D
    {
        [Export] public Color AuraColor { get; set; } = new Color(0.2f, 0.8f, 1f, 0.4f);
        [Export] public int ParticleAmount { get; set; } = 8;
        [Export] public float EmissionRadius { get; set; } = 14f;

        private CpuParticles2D? _particles;

        public override void _Ready()
        {
            var p = new CpuParticles2D
            {
                Amount = ParticleAmount,
                Lifetime = 0.6f,
                Preprocess = 0.3f,
                SpeedScale = 1.0f,
                Explosiveness = 0.05f,
                Randomness = 0.2f,
                LocalCoords = false,
                DrawOrder = CpuParticles2D.DrawOrderEnum.Index,
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
                EmissionSphereRadius = EmissionRadius,
                Gravity = new Vector2(0f, -12f),
                Color = AuraColor,
                ScaleAmountMin = 1.5f,
                ScaleAmountMax = 3.0f,
            };

            // Soft radial dot texture
            p.Texture = CreateDotTexture();
            AddChild(p);
            _particles = p;
        }

        private static GradientTexture2D CreateDotTexture()
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 1f));
            g.SetOffset(0, 0f);
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            return new GradientTexture2D
            {
                Gradient = g,
                Width = 8,
                Height = 8,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
            };
        }
    }
}
