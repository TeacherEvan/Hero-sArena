using Godot;

namespace HeroArena
{
    /// <summary>
    /// Subscribes to <see cref="EventBus.OnProjectileHit"/> and briefly tints
    /// the screen with a ColorRect overlay. Resolves the F-31 dead-signal
    /// finding: OnProjectileHit was emitted by 5+ call sites but had zero
    /// in-tree consumers, so the event did nothing. The HitFlash is the
    /// minimum useful consumer — a damage-type-keyed flash on every projectile impact.
    ///
    /// DamageType-keyed color: kinetic=red, energy=cyan, lightning=yellow,
    /// acid=green, fire=orange, explosive=white. Other types fall back to red.
    /// </summary>
    public partial class HitFlash : CanvasLayer
    {
        private ColorRect _overlay = null!;
        private float _remaining;

        // Scratch color reused per projectile hit to avoid per-event Color allocation.
        private Color _flashColor;

        public override void _Ready()
        {
            _overlay = new ColorRect
            {
                Color = new Color(1f, 0f, 0f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorRight = 1f,
                AnchorBottom = 1f,
            };
            AddChild(_overlay);
            if (EventBus.Instance != null)
                EventBus.Instance.OnProjectileHit += OnProjectileHit;
            SetProcess(false);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnProjectileHit -= OnProjectileHit;
            base._ExitTree();
        }

        private void OnProjectileHit(Vector2 pos, DamageType type)
        {
            var (r, g, b) = HitFlashPalette.ColorFor(type);
            _flashColor.R = r;
            _flashColor.G = g;
            _flashColor.B = b;
            _overlay.Color = _flashColor;
            _remaining = HitFlashPalette.Duration;
            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            _remaining -= (float)delta;
            float alpha = HitFlashPalette.AlphaFor(_remaining);
            _overlay.Color = _overlay.Color with { A = alpha };
            if (alpha <= 0f)
            {
                SetProcess(false);
            }
        }
    }
}
