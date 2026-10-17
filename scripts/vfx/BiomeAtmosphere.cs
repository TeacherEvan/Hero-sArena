using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Per-biome atmosphere (the "place" half of the living arena): two
    /// code-generated ambient particle fields configured from the loaded
    /// map's <see cref="BiomeAtmosphereState"/> profile — a signature weather
    /// (neon rain, ash fall, rising embers, star-dust, spores, dust haze) plus
    /// a slower, larger haze layer for depth. Field gravity is re-aimed every
    /// frame from the shared <see cref="AmbientWindState"/> (sampled at the
    /// camera), so the air slants with the same wind that moves the ground
    /// and the wildlife; explosions briefly bend it.
    ///
    /// All textures are generated in code (streaks, soft dots) — zero new art
    /// assets. Wave moods thicken the weather as waves escalate and storm it
    /// while a boss has the world holding its breath. Node order in Main.tscn
    /// keeps fields above the ground and below living entities.
    /// </summary>
    public partial class BiomeAtmosphere : Node2D
    {
        private const int TextureSize = 32;

        /// <summary>Wind→acceleration factor when re-aiming field gravity.</summary>
        private const float WindGravityFactor = 1.4f;

        /// <summary>Mood smoothing rate (exponential approach).</summary>
        private const float MoodRate = 0.9f;

        private readonly AmbientWindState _wind = new();
        private CpuParticles2D _primary = null!;
        private CpuParticles2D _secondary = null!;
        private BiomeAtmosphereState.BiomeProfile _profile;
        private float _windScale = 1f;
        private float _mood = 1f;
        private float _moodTarget = 1f;
        private bool _bossStorm;
        private int _wave;
        private Camera2D? _camera;

        public override void _Ready()
        {
            _profile = BiomeAtmosphereState.ProfileFor(SceneAnchors.ResolveBiome(this));
            _windScale = _profile.WindScale;

            (Vector2 center, Vector2 halfExtents) = SceneAnchors.ResolveArena(this);
            GlobalPosition = center;
            _camera = GetTree().CurrentScene?.GetNodeOrNull<Camera2D>("Camera");

            _primary = MakeField(_profile.PrimaryField, halfExtents);
            _secondary = MakeField(_profile.SecondaryField, halfExtents);

            EventBus.Instance.OnWaveCompleted += OnWaveCompleted;
            EventBus.Instance.OnWaveIncoming += OnWaveIncoming;
            EventBus.Instance.OnThreatLevelChanged += OnThreatLevelChanged;
            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
            EventBus.Instance.OnProjectileHit += OnProjectileHit;

            RefreshMoodTarget();
            _mood = _moodTarget;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnWaveCompleted -= OnWaveCompleted;
                EventBus.Instance.OnWaveIncoming -= OnWaveIncoming;
                EventBus.Instance.OnThreatLevelChanged -= OnThreatLevelChanged;
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
                EventBus.Instance.OnProjectileHit -= OnProjectileHit;
            }
            base._ExitTree();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _wind.Update(dt);

            // Every consumer samples the same wind; the camera position is the
            // cheapest representative point for screen-filling fields.
            Vector2 at = _camera != null && IsInstanceValid(_camera)
                ? _camera.GlobalPosition
                : GlobalPosition;
            var (wx, wy) = _wind.Sample(at.X, at.Y);
            wx *= _windScale;
            wy *= _windScale;

            float k = 1f - MathF.Exp(-MoodRate * dt);
            _mood += (_moodTarget - _mood) * k;

            ApplyField(_primary, _profile.PrimaryField, wx, wy);
            ApplyField(_secondary, _profile.SecondaryField, wx, wy);
        }

        private void ApplyField(CpuParticles2D field, BiomeAtmosphereState.ParticleField spec, float wx, float wy)
        {
            // Wind becomes acceleration on top of the field's own gravity:
            // rain slants, embers stream, haze drifts — all in one direction.
            float influence = spec.WindInfluence01 * WindGravityFactor;
            field.Gravity = new Vector2(wx * influence, spec.GravityY + wy * influence * 0.4f);
            field.SpeedScale = _mood;
        }

        private CpuParticles2D MakeField(BiomeAtmosphereState.ParticleField spec, Vector2 halfExtents)
        {
            var emitter = new CpuParticles2D
            {
                Name = $"Field_{spec.Kind}",
                Texture = TextureFor(spec.Kind),
                Emitting = true,
                OneShot = false,
                Amount = spec.Amount,
                Lifetime = spec.Lifetime,
                LifetimeRandomness = 0.4f,
                Preprocess = spec.Lifetime * 0.5f, // fields are already dense on load
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = halfExtents + new Vector2(96f, 96f),
                Direction = spec.GravityY < 0f ? new Vector2(0f, -1f) : new Vector2(0f, 1f),
                Spread = spec.GravityY == 0f ? 180f : 14f,
                InitialVelocityMin = spec.SpeedMin,
                InitialVelocityMax = spec.SpeedMax,
                Gravity = new Vector2(0f, spec.GravityY),
                ScaleAmountMin = spec.SizeScale * 0.75f,
                ScaleAmountMax = spec.SizeScale * 1.25f,
                Color = new Color(spec.R, spec.G, spec.B, spec.A),
                ColorRamp = FadeRamp(),
                LocalCoords = false,
            };

            if (spec.Kind == AmbientFieldKind.RisingEmbers || spec.Kind == AmbientFieldKind.StarDust)
                emitter.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

            AddChild(emitter);
            return emitter;
        }

        private void OnWaveIncoming(int wave, bool isBoss)
        {
            _wave = Math.Max(_wave, wave);
            _bossStorm = isBoss;
            RefreshMoodTarget();
        }

        private void OnWaveCompleted(int wave)
        {
            _wave = Math.Max(_wave, wave);
            _bossStorm = false;
            RefreshMoodTarget();
        }

        private void OnThreatLevelChanged(int level) => RefreshMoodTarget();

        /// <summary>Explosions punch a transient impulse into the wind: the air bends, then settles.</summary>
        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
        {
            var (strength, r, life) = AmbientWindState.ExplosionImpulse(radius);
            _wind.AddImpulse(pos.X, pos.Y, strength, r, life);
        }

        private void OnProjectileHit(Vector2 pos, DamageType type)
        {
            var (strength, r, life) = AmbientWindState.ImpulseForHit(type);
            if (strength <= 0f) return;
            _wind.AddImpulse(pos.X, pos.Y, strength, r, life);
        }

        private void RefreshMoodTarget()
        {
            int threat = GameManager.Instance?.ThreatLevel ?? 0;
            // The wave number rides on the wave events themselves
            // (GameManagerState.CurrentWave is never incremented — dead state).
            float target = BiomeAtmosphereState.FieldIntensity(_wave, threat);
            if (_bossStorm) target *= BiomeAtmosphereState.BossStormMultiplier;
            _moodTarget = target;
        }

        private static GradientTexture2D TextureFor(AmbientFieldKind kind) => kind switch
        {
            AmbientFieldKind.NeonRain => StreakTexture(),
            AmbientFieldKind.AshFall => SoftDotTexture(8),
            AmbientFieldKind.RisingEmbers => SoftDotTexture(8),
            AmbientFieldKind.StarDust => SoftDotTexture(6),
            AmbientFieldKind.Spores => SoftDotTexture(10),
            _ => SoftDotTexture(24), // DustHaze: big soft puffs
        };

        /// <summary>Thin vertical streak for rain: a soft line that slants with the wind.</summary>
        private static GradientTexture2D StreakTexture()
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(0, 0f);
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            g.AddPoint(0.5f, new Color(1f, 1f, 1f, 1f)); // soft line: transparent → bright middle → transparent
            return new GradientTexture2D
            {
                Gradient = g,
                Width = 6,
                Height = TextureSize,
                Fill = GradientTexture2D.FillEnum.Linear,
                FillFrom = new Vector2(0.5f, 0f),
                FillTo = new Vector2(0.5f, 1f),
            };
        }

        /// <summary>Radial soft dot for every drifting/floating field material.</summary>
        private static GradientTexture2D SoftDotTexture(int size)
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 1f));
            g.SetOffset(0, 0f);
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            return new GradientTexture2D
            {
                Gradient = g,
                Width = size,
                Height = size,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
            };
        }

        /// <summary>Full → gone alpha fade over each particle's lifetime.</summary>
        private static Gradient FadeRamp()
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 1f));
            g.SetOffset(0, 0f);
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            return g;
        }
    }
}
