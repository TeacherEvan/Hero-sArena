using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// The arena's ambient wildlife (the "alive" half of the living arena):
    /// a pool of critters simulated entirely by the Godot-free
    /// <see cref="WildlifeDirectorState"/> — wandering around home points,
    /// drifting with the shared <see cref="AmbientWindState"/>, scattering
    /// from combat, holding their breath while a boss has the world waiting,
    /// and — when the arena is calm and the hero stands still — drifting
    /// curiously toward the player.
    ///
    /// Rendering: one dark silhouette plus one additive accent-glow sprite
    /// per critter (both pre-allocated, zero instantiation per event).
    /// Glowing species follow the real-firefly pattern — dark most of the
    /// time, one short flash per period — via
    /// <see cref="WildlifeDirectorState.FireflyFlash"/>. All textures are
    /// code-generated soft dots; zero new art assets. Wildlife stays well
    /// below projectile brightness so combat light remains the brightest
    /// thing on screen (see docs/external-research/living-world-wildlife-patterns.md).
    /// </summary>
    public partial class AmbientWildlife : Node2D
    {
        private const int MaxCritters = WildlifeDirectorState.MaxCritters;

        /// <summary>Fix integer seed: the same arena always opens the same way.</summary>
        private const int Seed = 20261017;

        /// <summary>Seconds a boss wave keeps wildlife hidden if the boss never starts.</summary>
        private const float BossHideSeconds = 30f;

        /// <summary>
        /// Safety valve for the boss hold: if the wave neither completes nor is
        /// followed by the next one within this long, the world stops holding
        /// its breath rather than staying silent for the rest of the run.
        /// </summary>
        private const float BossHoldMaxSeconds = 90f;

        /// <summary>Dark silhouette colour; accent colour is mixed in per species.</summary>
        private static readonly Color BodyColor = new(0.16f, 0.18f, 0.24f);

        private readonly WildlifeDirectorState _director = new();
        private readonly AmbientWindState _wind = new();
        private readonly Sprite2D[] _bodies = new Sprite2D[MaxCritters];
        private readonly Sprite2D[] _glows = new Sprite2D[MaxCritters];
        private readonly WildlifeDirectorState.SpeciesProfile[] _species =
            new WildlifeDirectorState.SpeciesProfile[2];

        private BiomeAtmosphereState.BiomeProfile _profile;
        private float _windScale = 1f;
        private float _time;
        private float _calm = 1f;
        private float _stillness = 1f;
        private float _curiosity;
        private bool _inWave;
        private bool _bossHold;
        private bool _nextWaveIsBoss;
        private float _bossHoldStartedAt;

        public override void _Ready()
        {
            _profile = BiomeAtmosphereState.ProfileFor(SceneAnchors.ResolveBiome(this));
            _windScale = _profile.WindScale;
            _species[0] = _profile.PrimaryCritter;
            _species[1] = _profile.SecondaryCritter;

            var (center, halfExtents) = SceneAnchors.ResolveArena(this);
            GlobalPosition = center;
            _director.SetArena(center.X, center.Y, halfExtents.X, halfExtents.Y);
            _director.Configure(_species, _profile.WildlifePopulation, Seed);

            // The scar layer is a sibling (Main.tscn draw order: ground, scars,
            // atmosphere, wildlife). Absent in standalone/dev scenes — then
            // critters simply wander without avoidance.
            var scarLayer = GetParent()?.GetNodeOrNull<ScarLayer>("ScarLayer");
            if (scarLayer != null)
                _director.ScarSampler = scarLayer.SampleScar;

            var bodyTexture = SoftDotTexture(16);
            var glowTexture = SoftDotTexture(28);
            for (int i = 0; i < MaxCritters; i++)
            {
                var body = new Sprite2D { Texture = bodyTexture, Visible = false };
                AddChild(body);
                _bodies[i] = body;

                var glow = new Sprite2D
                {
                    Texture = glowTexture,
                    Visible = false,
                    Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
                };
                AddChild(glow);
                _glows[i] = glow;
            }

            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
            EventBus.Instance.OnProjectileHit += OnProjectileHit;
            EventBus.Instance.OnWaveIncoming += OnWaveIncoming;
            EventBus.Instance.OnWaveStarted += OnWaveStarted;
            EventBus.Instance.OnWaveCompleted += OnWaveCompleted;
            EventBus.Instance.OnHeroDamaged += OnHeroDamaged;
            EventBus.Instance.OnThreatLevelChanged += OnThreatLevelChanged;

            RefreshPopulation();
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
                EventBus.Instance.OnProjectileHit -= OnProjectileHit;
                EventBus.Instance.OnWaveIncoming -= OnWaveIncoming;
                EventBus.Instance.OnWaveStarted -= OnWaveStarted;
                EventBus.Instance.OnWaveCompleted -= OnWaveCompleted;
                EventBus.Instance.OnHeroDamaged -= OnHeroDamaged;
                EventBus.Instance.OnThreatLevelChanged -= OnThreatLevelChanged;
            }
            base._ExitTree();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            _wind.Update(dt);

            if (_bossHold && _time - _bossHoldStartedAt > BossHoldMaxSeconds)
            {
                // Recovery valve (see BossHoldMaxSeconds): a wave that never
                // completes must not keep the arena silent forever.
                _inWave = false;
                ReleaseBossHold();
            }

            var hero = GameManager.Instance?.ActiveHero;
            bool heroKnown = hero != null && GodotObject.IsInstanceValid(hero);
            Vector2 heroPos = heroKnown ? hero!.GlobalPosition : GlobalPosition;
            if (heroKnown)
                _director.SetHero(heroPos.X, heroPos.Y);

            // The wind is sampled once, at the arena's centre of attention;
            // the director spreads it over every critter.
            var (wx, wy) = _wind.Sample(heroPos.X, heroPos.Y);
            wx *= _windScale;
            wy *= _windScale;

            float stillTarget = heroKnown && hero!.Velocity.LengthSquared() < 400f ? 1f : 0f;
            _stillness = Approach(_stillness, stillTarget, 2f, dt);

            float calmTarget = _inWave || _bossHold ? 0f : 1f;
            _calm = Approach(_calm, calmTarget, 0.55f, dt);

            // Curiosity needs calm *and* a hero who has stopped moving —
            // animals investigate a resting presence, not a running one.
            _curiosity = Approach(_curiosity, _calm * _stillness, 0.8f, dt);
            _director.SetCuriosity(_curiosity);

            _director.Update(dt, wx, wy);
            Render();
        }

        private void Render()
        {
            for (int i = 0; i < MaxCritters; i++)
            {
                var c = _director.GetCritter(i);
                var body = _bodies[i];
                var glow = _glows[i];

                if (!c.Active || c.Fade <= 0.004f)
                {
                    if (body.Visible)
                    {
                        body.Visible = false;
                        glow.Visible = false;
                    }
                    continue;
                }

                var spec = _species[c.Species == 1 ? 1 : 0];
                var accent = new Color(spec.AccentR, spec.AccentG, spec.AccentB);

                // Air species bob gently; ground skitters keep their back to the floor.
                float bob = spec.Height01 * MathF.Sin(_time * 2.6f + c.Phase) * 5f;
                var pos = new Vector2(c.X, c.Y + bob);
                bool grounded = spec.Height01 < 0.35f;

                // Silhouette: darker when the species carries a bright glow,
                // leaning toward the accent so it never reads as a grey blob.
                float bodyAlpha = c.Fade * (0.62f - 0.22f * spec.Glow01);
                var bodyTint = BodyColor.Lerp(accent, 0.15f + 0.3f * spec.Glow01);
                body.Modulate = new Color(bodyTint.R, bodyTint.G, bodyTint.B, bodyAlpha);
                body.Scale = grounded
                    ? new Vector2(spec.Size * 1.5f, spec.Size * 0.85f)
                    : new Vector2(spec.Size * 1.15f, spec.Size * 1.15f);
                body.Rotation = grounded && c.VX * c.VX + c.VY * c.VY > 4f
                    ? MathF.Atan2(c.VY, c.VX)
                    : 0f;
                body.GlobalPosition = pos;
                body.Visible = true;

                // Accent glow: the firefly pattern scaled by the species'
                // bioluminescence. Dim species still shimmer faintly, so
                // shadows on a black floor stay trackable. The phase is seeded
                // per critter at spawn, so a respawn in another slot gets a
                // fresh rhythm instead of inheriting that slot's.
                float flash = WildlifeDirectorState.FireflyFlash(_time, c.Phase * (1f / Mathf.Tau));
                float glowAlpha = c.Fade * (0.18f + 0.82f * spec.Glow01) * flash * 0.9f;
                bool glowVisible = glowAlpha > 0.01f;
                if (glowVisible)
                {
                    float glowScale = spec.Size * (grounded ? 2.6f : 3.8f) * (0.9f + 0.25f * flash);
                    glow.Modulate = new Color(accent.R, accent.G, accent.B, glowAlpha);
                    glow.Scale = Vector2.One * glowScale;
                    glow.GlobalPosition = pos;
                }
                if (glow.Visible != glowVisible)
                    glow.Visible = glowVisible;
            }
        }

        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
        {
            float strength = Mathf.Clamp(radius / 140f, 0.35f, 1f);
            _director.Panic(pos.X, pos.Y, radius * 2.2f + 60f, strength);
        }

        private void OnProjectileHit(Vector2 pos, DamageType type)
        {
            // Bullets land constantly; only blast-like impacts startle wildlife.
            // Acid is in because an Exploder's detonation emits it — a body-wide
            // bang, not a splash.
            float radius = type switch
            {
                DamageType.Explosive => 260f,
                DamageType.Acid => 220f,
                DamageType.Fire => 150f,
                _ => 0f,
            };
            if (radius <= 0f) return;
            float strength = type switch
            {
                DamageType.Explosive => 0.7f,
                DamageType.Acid => 0.6f,
                _ => 0.45f,
            };
            _director.Panic(pos.X, pos.Y, radius, strength);
        }

        private void OnWaveIncoming(int wave, bool isBoss)
        {
            _nextWaveIsBoss = isBoss;
            if (!isBoss) return;
            // The world holds its breath. Population 0 keeps fresh critters
            // from fading in mid-fight; the hide covers the ones already out.
            _bossHold = true;
            _bossHoldStartedAt = _time;
            _director.HideAll(BossHideSeconds);
            _director.SetTargetPopulation(0);
        }

        private void OnWaveStarted(int wave)
        {
            _inWave = true;
            if (_bossHold)
            {
                if (_nextWaveIsBoss)
                {
                    // Still the boss wave: keep holding the breath.
                    _director.HideAll(BossHideSeconds);
                    return;
                }
                // A non-boss wave is starting, so the boss wave ended without a
                // completion event — recover instead of staying silent all run.
                ReleaseBossHold();
            }
            // The arena wakes: everything near the hero bolts as the wave arrives.
            var hero = GameManager.Instance?.ActiveHero;
            Vector2 at = hero != null && GodotObject.IsInstanceValid(hero)
                ? hero.GlobalPosition
                : GlobalPosition;
            _director.Panic(at.X, at.Y, 1400f, 0.55f);
        }

        private void OnWaveCompleted(int wave)
        {
            _inWave = false;
            if (_bossHold)
                ReleaseBossHold();
        }

        /// <summary>Ends the boss hold in one place: the hide is over and the population returns.</summary>
        private void ReleaseBossHold()
        {
            _bossHold = false;
            _director.EndHide();
            RefreshPopulation();
        }

        private void OnHeroDamaged(float damage)
        {
            var hero = GameManager.Instance?.ActiveHero;
            if (hero == null || !GodotObject.IsInstanceValid(hero)) return;
            Vector2 pos = hero.GlobalPosition;
            _director.Panic(pos.X, pos.Y, 260f, 0.5f);
        }

        private void OnThreatLevelChanged(int level) => RefreshPopulation();

        private void RefreshPopulation()
        {
            if (_bossHold)
            {
                _director.SetTargetPopulation(0);
                return;
            }
            int threat = GameManager.Instance?.ThreatLevel ?? 0;
            float keep = 1f - 0.04f * Math.Clamp(threat, 0, 5);
            _director.SetTargetPopulation(Mathf.RoundToInt(_profile.WildlifePopulation * keep));
        }

        private static float Approach(float current, float target, float rate, float dt)
            => current + (target - current) * (1f - MathF.Exp(-rate * dt));

        /// <summary>Radial soft dot (critter bodies and glow halos).</summary>
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
    }
}
