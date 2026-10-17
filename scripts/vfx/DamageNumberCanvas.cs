using Godot;

namespace HeroArena
{
    /// <summary>
    /// Pooled floating combat text canvas layer.
    /// Renders animated damage numbers in world space with color-coded damage types.
    /// </summary>
    public partial class DamageNumberCanvas : CanvasLayer
    {
        private const int POOL_SIZE = 32;
        private const float LIFETIME = 0.9f;
        private const float FLOAT_SPEED = 65f;

        private struct ActiveNumber
        {
            public Label Label;
            public Vector2 WorldPos;
            public Vector2 Velocity;
            public float Elapsed;
            public bool IsActive;
        }

        private readonly ActiveNumber[] _pool = new ActiveNumber[POOL_SIZE];
        private System.Action<Vector2, float, DamageType, bool>? _handler;

        public override void _Ready()
        {
            Layer = 5; // Above world, below modals
            for (int i = 0; i < POOL_SIZE; i++)
            {
                var lbl = new Label
                {
                    Visible = false,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    PivotOffset = new Vector2(30f, 15f)
                };
                lbl.AddThemeFontSizeOverride("font_size", 18);
                AddChild(lbl);
                _pool[i] = new ActiveNumber { Label = lbl, IsActive = false };
            }

            _handler = (pos, amount, type, crit) => Spawn(pos, amount, type, crit);
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnDamageNumber += _handler;
            }
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null && _handler != null)
            {
                EventBus.Instance.OnDamageNumber -= _handler;
            }
            base._ExitTree();
        }

        public void Spawn(Vector2 worldPos, float amount, DamageType type, bool isCritical = false)
        {
            if (amount <= 0f) return;

            int targetIndex = -1;
            float oldestElapsed = -1f;

            for (int i = 0; i < POOL_SIZE; i++)
            {
                if (!_pool[i].IsActive)
                {
                    targetIndex = i;
                    break;
                }
                if (_pool[i].Elapsed > oldestElapsed)
                {
                    oldestElapsed = _pool[i].Elapsed;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0) return;

            Color color = isCritical ? VisualTheme.Gold : GetDamageColor(type);
            var lbl = _pool[targetIndex].Label;
            lbl.Text = isCritical ? $"!{(int)Mathf.Round(amount)}" : $"{(int)Mathf.Round(amount)}";
            lbl.Modulate = color;
            lbl.Scale = isCritical ? new Vector2(1.35f, 1.35f) : new Vector2(1.0f, 1.0f);
            lbl.Visible = true;

            float angleJitter = (float)GD.RandRange(-0.4, 0.4);
            Vector2 initialVel = new Vector2(angleJitter * 25f, -FLOAT_SPEED);

            _pool[targetIndex].WorldPos = worldPos + new Vector2(angleJitter * 10f, -10f);
            _pool[targetIndex].Velocity = initialVel;
            _pool[targetIndex].Elapsed = 0f;
            _pool[targetIndex].IsActive = true;

            UpdateLabelTransform(ref _pool[targetIndex]);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            var viewport = GetViewport();
            if (viewport == null) return;

            for (int i = 0; i < POOL_SIZE; i++)
            {
                if (!_pool[i].IsActive) continue;

                _pool[i].Elapsed += dt;
                if (_pool[i].Elapsed >= LIFETIME)
                {
                    _pool[i].IsActive = false;
                    _pool[i].Label.Visible = false;
                    continue;
                }

                _pool[i].WorldPos += _pool[i].Velocity * dt;
                float progress = _pool[i].Elapsed / LIFETIME;

                // Scale pop on spawn, then settle
                if (progress < 0.15f)
                {
                    float pop = 1.0f + 0.3f * (1.0f - progress / 0.15f);
                    _pool[i].Label.Scale = new Vector2(pop, pop);
                }

                // Alpha fade
                Color c = _pool[i].Label.Modulate;
                c.A = Mathf.Clamp(1.0f - (progress * progress), 0f, 1f);
                _pool[i].Label.Modulate = c;

                UpdateLabelTransform(ref _pool[i]);
            }
        }

        private void UpdateLabelTransform(ref ActiveNumber item)
        {
            var canvasTransform = GetViewport().GetCanvasTransform();
            Vector2 screenPos = canvasTransform * item.WorldPos;
            item.Label.Position = screenPos - (item.Label.Size * 0.5f);
        }

        public static Color GetDamageColor(DamageType type) => type switch
        {
            DamageType.Kinetic => new Color(0.92f, 0.94f, 0.96f),
            DamageType.Energy => VisualTheme.Cyan,
            DamageType.Fire => new Color(1.0f, 0.45f, 0.1f),
            DamageType.Acid => new Color(0.2f, 0.85f, 0.3f),
            DamageType.Lightning => new Color(0.75f, 0.5f, 1.0f),
            DamageType.Explosive => new Color(1.0f, 0.25f, 0.25f),
            _ => VisualTheme.Text
        };
    }
}
