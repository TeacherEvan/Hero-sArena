using Godot;

namespace HeroArena
{
    /// <summary>
    /// Full-screen shading illusion layer: refraction rings that bend the
    /// rendered frame around explosions and heavy impacts, plus a karma
    /// vignette that tightens the screen edges as the Collateral Karma
    /// threat level rises (with a red pulse whenever the hero is hit).
    ///
    /// The overlay is a CanvasLayer + full-rect ColorRect running
    /// res://shaders/screen_distortion.gdshader. Main.tscn pins explicit
    /// CanvasLayer values (this layer 1 < HUD 2 < UI 3 < HitFlash 4), so the
    /// playfield warps while the UI stays crisp regardless of sibling order.
    /// All ripple and
    /// vignette math lives in the Godot-free ScreenRippleState (covered by
    /// ScreenRippleStateTests); this node only does world→UV mapping and
    /// uniform plumbing, and it stops processing once everything settles
    /// (hiding the overlay entirely once the vignette has faded out).
    /// </summary>
    public partial class ScreenDistortionFX : CanvasLayer
    {
        private const float ImpactStrength = 0.30f;
        private const float ExplosionStrength = 0.65f;
        private const float ImpactLifetime = 0.45f;
        private const float ExplosionLifetime = 0.80f;
        private const float ImpactRadius01 = 0.16f;
        private const float ExplosionRadius01 = 0.46f;
        private const float OffscreenUvMargin = 0.15f;

        // Hoisted uniform names: PushUniforms runs per frame while anything
        // animates, so it must not allocate interpolated strings.
        private static readonly string[] RippleParamNames =
        {
            "u_ripple0", "u_ripple1", "u_ripple2", "u_ripple3",
            "u_ripple4", "u_ripple5", "u_ripple6", "u_ripple7",
        };
        private const string VignetteParamName = "u_vignette";
        private const string AspectParamName = "u_aspect";

        private ColorRect _overlay = null!;
        private ShaderMaterial? _material;
        private readonly ScreenRippleState _state = new();
        private float _timeSinceAdd = 1f;

        public override void _Ready()
        {
            Layer = 1;
            _overlay = new ColorRect
            {
                Color = Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorRight = 1f,
                AnchorBottom = 1f,
                Visible = false,
            };
            AddChild(_overlay);

            var shader = GD.Load<Shader>("res://shaders/screen_distortion.gdshader");
            if (shader == null)
            {
                GD.PrintErr("ScreenDistortionFX: shader missing at res://shaders/screen_distortion.gdshader; distortion layer disabled.");
                return;
            }
            _material = new ShaderMaterial { Shader = shader };
            _overlay.Material = _material;
            PushUniforms();

            EventBus.Instance.OnProjectileHit += OnProjectileHit;
            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
            EventBus.Instance.OnHeroDamaged += OnHeroDamaged;
            EventBus.Instance.OnThreatLevelChanged += OnThreatLevelChanged;

            var gm = GameManager.Instance;
            if (gm != null)
                _state.SetVignetteTarget(ScreenRippleState.ThreatVignette(gm.ThreatLevel));

            if (_state.ShouldRender) Wake();
            else SetProcess(false);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnProjectileHit -= OnProjectileHit;
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
                EventBus.Instance.OnHeroDamaged -= OnHeroDamaged;
                EventBus.Instance.OnThreatLevelChanged -= OnThreatLevelChanged;
            }
            base._ExitTree();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _timeSinceAdd += dt;
            _state.Update(dt);

            if (!_state.ShouldRender)
            {
                _overlay.Visible = false;
                SetProcess(false);
                return;
            }
            if (!_state.AnyWork)
            {
                // The settled vignette stays on screen; the shader's TIME
                // shimmer keeps it alive, so there is nothing to push.
                SetProcess(false);
                return;
            }
            PushUniforms();
        }

        private void OnProjectileHit(Vector2 pos, DamageType type)
        {
            if (type == DamageType.Explosive) AddRipple(pos, ExplosionStrength, ExplosionRadius01, ExplosionLifetime);
            else AddRipple(pos, ImpactStrength, ImpactRadius01, ImpactLifetime);
        }

        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
            => AddRipple(pos, ExplosionStrength, ExplosionRadius01, ExplosionLifetime);

        private void OnHeroDamaged(float damage)
        {
            _state.PulseVignette(0.35f);
            Wake();
        }

        private void OnThreatLevelChanged(int level)
        {
            _state.SetVignetteTarget(ScreenRippleState.ThreatVignette(level));
            Wake();
        }

        private void AddRipple(Vector2 world, float strength, float radius01, float life)
        {
            Vector2 uv = WorldToUv(world);
            if (uv.X < -OffscreenUvMargin || uv.X > 1f + OffscreenUvMargin
                || uv.Y < -OffscreenUvMargin || uv.Y > 1f + OffscreenUvMargin)
                return;

            if (_state.TryAdd(uv.X, uv.Y, strength, radius01, life, _timeSinceAdd))
            {
                _timeSinceAdd = 0f;
                Wake();
            }
        }

        /// <summary>
        /// Makes sure the overlay is visible and processing while something
        /// needs animating (ripples, the damage pulse, or a vignette change).
        /// </summary>
        private void Wake()
        {
            if (_material == null) return;
            _overlay.Visible = true;
            SetProcess(true);
            PushUniforms();
        }

        private Vector2 WorldToUv(Vector2 world)
        {
            var vp = GetViewport();
            Vector2 screen = vp.CanvasTransform * world;
            Vector2 size = vp.GetVisibleRect().Size;
            return size.X <= 0f || size.Y <= 0f ? Vector2.Zero : screen / size;
        }

        private void PushUniforms()
        {
            if (_material == null) return;
            for (int i = 0; i < ScreenRippleState.MaxRipples; i++)
            {
                var r = _state.GetRipple(i);
                Vector4 packed = r.Active
                    ? new Vector4(r.U, r.V, r.Amplitude, r.RingRadius01)
                    : Vector4.Zero;
                _material.SetShaderParameter(RippleParamNames[i], packed);
            }
            _material.SetShaderParameter(VignetteParamName, _state.Vignette);

            Vector2 size = GetViewport().GetVisibleRect().Size;
            float aspect = size.Y > 0f ? size.X / size.Y : 1.7778f;
            _material.SetShaderParameter(AspectParamName, aspect);
        }
    }
}
