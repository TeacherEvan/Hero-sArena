using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// The ground's memory (R-LA6): renders <see cref="DisturbanceMemoryState"/>
    /// stamps — hero footprints, blast cracks and scorch patches — as a single
    /// <c>_Draw</c> pass of soft code-generated brushes, stamps blast events
    /// into the memory, tracks the hero's footprints, and exposes
    /// <see cref="SampleScar"/> so the wildlife director keeps critters off
    /// scarred ground.
    ///
    /// Zero new art assets: the brush is a code-generated radial gradient.
    /// Covered by DisturbanceMemoryStateTests (pure class) plus the
    /// LivingArenaProbe engine check (blast event → sampler reports scar).
    /// </summary>
    public partial class ScarLayer : Node2D
    {
        public const float PrintStepPx = 26f;
        public const float PrintRadiusPx = 9f;
        public const float PrintStrength = 0.25f;

        private readonly DisturbanceMemoryState _memory = new(DisturbanceMemoryState.DefaultCapacity);
        private Texture2D _brush = null!;
        private Vector2 _lastPrintAt;
        private bool _printSeeded;
        private float _redrawTimer;

        public override void _Ready()
        {
            _brush = MakeBrush();
            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
            EventBus.Instance.OnProjectileHit += OnProjectileHit;
            QueueRedraw();
        }

        public override void _ExitTree()
        {
            EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
            EventBus.Instance.OnProjectileHit -= OnProjectileHit;
        }

        public override void _Process(double delta)
        {
            var dt = (float)delta;
            _memory.Update(dt);

            HeroPrints(dt);

            _redrawTimer -= dt;
            if (_redrawTimer <= 0f && _memory.ActiveCount > 0)
            {
                _redrawTimer = 1f / 30f;
                QueueRedraw();
            }
        }

        /// <summary>Scar strength at a world point (0 clean .. 1 scarred) — for the wildlife director's avoidance.</summary>
        public float SampleScar(float x, float y) => _memory.SampleScar(x, y);

        private void HeroPrints(float dt)
        {
            var hero = GameManager.Instance?.ActiveHero;
            if (hero == null || !GodotObject.IsInstanceValid(hero)) return;
            var pos = hero.GlobalPosition;
            if (!_printSeeded)
            {
                _lastPrintAt = pos;
                _printSeeded = true;
                return;
            }
            if ((pos - _lastPrintAt).LengthSquared() < PrintStepPx * PrintStepPx) return;
            float heading = MathF.Atan2(pos.Y - _lastPrintAt.Y, pos.X - _lastPrintAt.X);
            _memory.Stamp(DisturbanceMemoryState.ScarKind.Print, pos.X, pos.Y, PrintRadiusPx, PrintStrength, heading);
            _lastPrintAt = pos;
            QueueRedraw();
        }

        private void OnEnvironmentDestroyed(Vector2 pos, float radius)
        {
            if (radius <= 0f) return;
            _memory.Stamp(DisturbanceMemoryState.ScarKind.Scorch, pos.X, pos.Y, Math.Clamp(radius * 0.9f, 24f, 90f), 0.9f);
            _memory.Stamp(DisturbanceMemoryState.ScarKind.Crack, pos.X, pos.Y, radius * 0.5f, 0.6f, 0f);
            QueueRedraw();
        }

        private void OnProjectileHit(Vector2 pos, DamageType type)
        {
            // An Exploder's detonation emits Acid: a body-wide bang that scars the floor.
            if (type != DamageType.Acid) return;
            _memory.Stamp(DisturbanceMemoryState.ScarKind.Scorch, pos.X, pos.Y, 24f, 0.5f);
            QueueRedraw();
        }

        private static GradientTexture2D MakeBrush()
        {
            var g = new Gradient();
            g.SetColor(0, new Color(1f, 1f, 1f, 1f));
            g.SetOffset(0, 0f);
            g.AddPoint(0.55f, new Color(1f, 1f, 1f, 0.45f));
            g.SetColor(1, new Color(1f, 1f, 1f, 0f));
            g.SetOffset(1, 1f);
            return new GradientTexture2D
            {
                Gradient = g,
                Width = 64,
                Height = 64,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
            };
        }

        public override void _Draw()
        {
            for (int i = 0; i < DisturbanceMemoryState.DefaultCapacity; i++)
            {
                var s = _memory.GetScar(i);
                if (!s.Active) continue;
                float env = DisturbanceMemoryState.EnvelopeFor(s.Age, s.Lifetime);
                if (env <= 0.01f) continue;
                float alpha = env * s.Strength01;

                switch (s.Kind)
                {
                    case DisturbanceMemoryState.ScarKind.Scorch:
                        // Soft dark halo, larger than the stamp radius.
                        DrawTextureRect(_brush,
                            new Rect2(s.X - s.Radius * 1.6f, s.Y - s.Radius * 1.6f, s.Radius * 3.2f, s.Radius * 3.2f),
                            false, new Color(0.12f, 0.08f, 0.05f, alpha * 0.8f));
                        break;

                    case DisturbanceMemoryState.ScarKind.Crack:
                        // Deterministic line fan from the stamp centre.
                        for (int k = 0; k < 4; k++)
                        {
                            float angle = s.Heading + (GroundDetailPlan.Hash01(i, k, 0x9E3779Bu) * 2f - 1f) * 0.6f
                                + k * (Mathf.Tau * 0.25f);
                            var from = new Vector2(s.X, s.Y);
                            var to = new Vector2(s.X + MathF.Cos(angle) * s.Radius,
                                s.Y + MathF.Sin(angle) * s.Radius);
                            DrawLine(from, to, new Color(0.05f, 0.05f, 0.07f, alpha * 0.6f), 2f);
                        }
                        break;

                    case DisturbanceMemoryState.ScarKind.Print:
                        // Two soft pads offset across the heading.
                        var perp = new Vector2(-MathF.Sin(s.Heading), MathF.Cos(s.Heading)) * 4f;
                        foreach (var offset in new Vector2[] { perp, -perp })
                        {
                            DrawTextureRect(_brush,
                                new Rect2(s.X + offset.X - PrintRadiusPx, s.Y + offset.Y - PrintRadiusPx,
                                    PrintRadiusPx * 2f, PrintRadiusPx * 2f),
                                false, new Color(0.10f, 0.12f, 0.09f, alpha * 0.35f));
                        }
                        break;
                }
            }
        }
    }
}
