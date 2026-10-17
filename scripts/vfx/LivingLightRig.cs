using Godot;

namespace HeroArena
{
    /// <summary>
    /// The arena is lit by the fight. Owns a CanvasModulate veil over the
    /// world (the HUD is a CanvasLayer and stays clear) plus a fixed pool of
    /// PointLight2D flashes whose radial textures are generated in code at
    /// startup — zero new art assets. Impact strobes use the damage-type
    /// palette, environmental destruction flares warm, the hero taking a hit
    /// pulses red, enemy kills shed a pale soul-light, and the ambient glow
    /// and flicker escalate with the Collateral Karma threat level and boss
    /// waves. All envelope/pool/mood math delegates to the Godot-free
    /// LightFXState (covered by LightFXStateTests); this node only maps
    /// slots onto lights and modulates the veil.
    /// </summary>
    public partial class LivingLightRig : Node2D
    {
        private const int TextureSize = 128;
        private const float KillFlashEnergy = 0.5f;
        private const float KillFlashRadius = 56f;
        private const float KillFlashLife = 0.22f;

        /// <summary>World darkness veil color; lights and the ambient pulse carve through it.</summary>
        [Export] public Color VeilColor { get; set; } = new Color(0.72f, 0.76f, 0.90f, 1f);

        private readonly LightFXState _state = new();
        private readonly PointLight2D[] _lights = new PointLight2D[LightFXState.SlotCount];
        private readonly Color[] _slotColors = new Color[LightFXState.SlotCount];

        private CanvasModulate _veil = null!;
        private float _sinceLastFlash = 10f;
        private float _time;
        private bool _bossWave;

        public override void _Ready()
        {
            // The radial textures are code-generated gradients drawn far past
            // their 128px source; the project-wide Nearest filter would band
            // them into rings. Children inherit this filter.
            TextureFilter = CanvasItem.TextureFilterEnum.Linear;

            _veil = new CanvasModulate { Name = "ArenaVeil" };
            _veil.Color = VeilColor;
            AddChild(_veil);

            var texture = BuildRadialTexture();
            for (int i = 0; i < _lights.Length; i++)
            {
                var light = new PointLight2D
                {
                    Name = $"Flash{i}",
                    Texture = texture,
                    Enabled = false,
                    BlendMode = Light2D.BlendModeEnum.Add,
                    Color = Colors.White,
                };
                AddChild(light);
                _lights[i] = light;
                _slotColors[i] = Colors.White;
            }

            EventBus.Instance.OnProjectileHit += OnProjectileHit;
            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
            EventBus.Instance.OnHeroDamaged += OnHeroDamaged;
            EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
            EventBus.Instance.OnThreatLevelChanged += OnThreatLevelChanged;
            EventBus.Instance.OnWaveIncoming += OnWaveIncoming;

            RefreshMoodTarget();
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnProjectileHit -= OnProjectileHit;
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
                EventBus.Instance.OnHeroDamaged -= OnHeroDamaged;
                EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
                EventBus.Instance.OnThreatLevelChanged -= OnThreatLevelChanged;
                EventBus.Instance.OnWaveIncoming -= OnWaveIncoming;
            }
            base._ExitTree();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            _sinceLastFlash += dt;
            _state.Update(dt);
            _state.UpdateAmbient(dt);

            for (int i = 0; i < _lights.Length; i++)
            {
                var slot = _state.GetSlot(i);
                var light = _lights[i];
                if (!slot.Active || slot.Energy <= 0.002f)
                {
                    if (light.Enabled) light.Enabled = false;
                    continue;
                }

                light.Enabled = true;
                light.GlobalPosition = new Vector2(slot.X, slot.Y);
                light.Color = _slotColors[i];
                light.Energy = Mathf.Min(slot.Energy, 2f);
                // Texture spans 128px at scale 1; radius is the world-space falloff.
                light.TextureScale = slot.Radius / TextureSize * 2f * slot.RadiusScale;
            }

            float flicker = LightFXState.Flicker(_time, 7.3f);
            float ambient = Mathf.Clamp(_state.AmbientEnergy * flicker, 0f, 1.35f);
            _veil.Color = new Color(
                Mathf.Min(VeilColor.R * ambient, 1f),
                Mathf.Min(VeilColor.G * ambient, 1f),
                Mathf.Min(VeilColor.B * ambient, 1f));
        }

        private void OnProjectileHit(Vector2 pos, DamageType type)
        {
            var (r, g, b) = HitFlashPalette.ColorFor(type);
            SpawnFlash(pos, new Color(r, g, b), LightFXState.ImpactFlash(type));
        }

        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
            => SpawnFlash(pos, new Color(1f, 0.62f, 0.28f), LightFXState.ExplosionFlash(radius));

        private void OnHeroDamaged(float damage)
        {
            var hero = GameManager.Instance?.ActiveHero;
            Vector2 pos = hero != null ? hero.GlobalPosition : Vector2.Zero;
            SpawnFlash(pos, new Color(1f, 0.22f, 0.22f), LightFXState.HeroDamageFlash());
        }

        private void OnEnemyKilled(EnemyBase enemy)
        {
            if (enemy == null || !IsInstanceValid(enemy)) return;
            SpawnFlash(enemy.GlobalPosition, new Color(0.72f, 0.88f, 1f), (KillFlashEnergy, KillFlashRadius, KillFlashLife));
        }

        private void OnThreatLevelChanged(int level) => RefreshMoodTarget();

        private void OnWaveIncoming(int wave, bool isBoss)
        {
            _bossWave = isBoss;
            RefreshMoodTarget();
        }

        private void RefreshMoodTarget()
        {
            int threat = GameManager.Instance?.ThreatLevel ?? 0;
            _state.SetAmbientTarget(LightFXState.MoodTarget(_bossWave, threat));
        }

        private void SpawnFlash(Vector2 pos, Color color, (float Energy, float Radius, float Life) p)
        {
            if (!LightFXState.AllowsFlash(_sinceLastFlash, p.Energy)) return;
            int idx = _state.Spawn(pos.X, pos.Y, p.Energy, p.Radius, p.Life);
            _slotColors[idx] = color;
            _sinceLastFlash = 0f;
        }

        private static GradientTexture2D BuildRadialTexture()
        {
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1f, 1f, 1f, 1f));
            gradient.SetOffset(0, 0f);
            gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
            gradient.SetOffset(1, 1f);
            return new GradientTexture2D
            {
                Gradient = gradient,
                Width = TextureSize,
                Height = TextureSize,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
            };
        }
    }
}
