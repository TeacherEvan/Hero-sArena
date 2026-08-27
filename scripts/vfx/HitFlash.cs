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
        private const float FlashDuration = 0.08f;
        private const float MaxAlpha = 0.25f;
        private float _remaining;

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
            _overlay.Color = TypeToColor(type);
            _remaining = FlashDuration;
            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            _remaining -= (float)delta;
            if (_remaining <= 0f)
            {
                _overlay.Color = _overlay.Color with { A = 0f };
                SetProcess(false);
                return;
            }
            float t = _remaining / FlashDuration; // 1 → 0
            _overlay.Color = _overlay.Color with { A = MaxAlpha * t };
        }

        private static Color TypeToColor(DamageType type) => type switch
        {
            DamageType.Kinetic => new Color(1f, 0.2f, 0.2f),
            DamageType.Energy => new Color(0.3f, 0.8f, 1f),
            DamageType.Lightning => new Color(1f, 1f, 0.3f),
            DamageType.Acid => new Color(0.4f, 1f, 0.4f),
            DamageType.Fire => new Color(1f, 0.5f, 0.1f),
            DamageType.Explosive => new Color(1f, 1f, 1f),
            _ => new Color(1f, 0f, 0f),
        };
    }
}
