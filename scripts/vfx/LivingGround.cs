using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// The living ground: the arena floor is scattered with deterministic,
    /// position-hashed detail — grass tufts, pebbles, cracks — from
    /// <see cref="GroundDetailPlan"/>, drawn by a single <c>_Draw</c> node
    /// (the documented approach for "a large number of simple objects").
    /// Tuft tips sway on the shared <see cref="AmbientWindState"/> wind with
    /// per-position flutter, and explosions punch local impulses that shove
    /// nearby tufts before settling.
    ///
    /// The killer detail: custom-drawn CanvasItems are lit by default, so the
    /// floor detail catches the combat light the existing light rig throws
    /// around — light finally has a surface to carve out of the void.
    ///
    /// Perf: the item list is only regenerated when the (32 px-snapped) view
    /// rect changes; a 30 Hz redraw animates sway. All textures/geometry are
    /// code-generated; zero new art assets.
    /// </summary>
    public partial class LivingGround : Node2D
    {
        /// <summary>Redraw rate: sway is slow, 30 Hz reads perfectly smooth.</summary>
        private const float RedrawInterval = 1f / 30f;

        /// <summary>Fixed content seed: the same floor always grows the same tufts.</summary>
        private const uint DetailSeed = 0x4845524Fu; // "HERO"

        /// <summary>World-space margin drawn beyond the viewport so edges never pop.</summary>
        private const float ViewMargin = 160f;

        /// <summary>View-rect snap step (px); absorbs sub-pixel camera jitter.</summary>
        private const float SnapStep = 32f;

        private static readonly Color TuftBase = new(0.30f, 0.38f, 0.26f);
        private static readonly Color PebbleBase = new(0.42f, 0.43f, 0.47f);
        private static readonly Color CrackBase = new(0.07f, 0.06f, 0.09f);

        private readonly GroundDetailPlan.Item[] _items =
            new GroundDetailPlan.Item[GroundDetailPlan.MaxItemsPerCall];
        private readonly AmbientWindState _wind = new();

        private Color _tuftColor = TuftBase;
        private Color _pebbleColor = PebbleBase;
        private float _windScale = 1f;
        private Rect2 _region;
        private float _redrawTimer;
        private float _time;
        private int _count;

        public override void _Ready()
        {
            var profile = BiomeAtmosphereState.ProfileFor(SceneAnchors.ResolveBiome(this));
            _windScale = profile.WindScale;

            // The biome tint flavours the floor's flora and stone.
            var tint = new Color(profile.TintR, profile.TintG, profile.TintB);
            _tuftColor = new Color(TuftBase.R * tint.R, TuftBase.G * tint.G, TuftBase.B * tint.B);
            _pebbleColor = new Color(PebbleBase.R * tint.R, PebbleBase.G * tint.G, PebbleBase.B * tint.B);

            EventBus.Instance.OnEnvironmentDestroyed += OnEnvironmentDestroyed;
            EventBus.Instance.OnProjectileHit += OnProjectileHit;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnvironmentDestroyed -= OnEnvironmentDestroyed;
                EventBus.Instance.OnProjectileHit -= OnProjectileHit;
            }
            base._ExitTree();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            _wind.Update(dt);

            _redrawTimer -= dt;
            if (_redrawTimer > 0f) return;

            _redrawTimer = RedrawInterval;
            Rect2 wanted = CurrentViewRect();
            if (wanted != _region)
            {
                _region = wanted;
                _count = GroundDetailPlan.GenerateRegion(
                    wanted.Position.X, wanted.Position.Y,
                    wanted.End.X, wanted.End.Y,
                    DetailSeed, _items, _items.Length);
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_count == 0) return;

            var (bx, by) = _wind.Wind();
            bx *= _windScale;
            by *= _windScale;

            DrawCracks();
            DrawPebbles();
            DrawTufts(bx, by);
        }

        private void DrawCracks()
        {
            var color = new Color(CrackBase.R, CrackBase.G, CrackBase.B, 0.55f);
            for (int i = 0; i < _count; i++)
            {
                var it = _items[i];
                if (it.Kind != GroundDetailPlan.ItemKind.Crack) continue;

                var dir = new Vector2(MathF.Cos(it.Rotation), MathF.Sin(it.Rotation));
                var mid = new Vector2(it.X, it.Y);
                var a = mid - dir * (it.Size * 0.5f);
                var b = mid + dir * (it.Size * 0.5f);
                DrawLine(a, b, color, 1f);

                if (it.Size > 18f)
                {
                    // A short side-branch makes long cracks read as cracked, not ruled.
                    float side = it.Shade01 > 0.5f ? 0.8f : -0.8f;
                    var bdir = dir.Rotated(side);
                    DrawLine(mid, mid + bdir * (it.Size * 0.28f), color, 1f);
                }
            }
        }

        private void DrawPebbles()
        {
            for (int i = 0; i < _count; i++)
            {
                var it = _items[i];
                if (it.Kind != GroundDetailPlan.ItemKind.Pebble) continue;

                var color = new Color(
                    _pebbleColor.R * it.Shade01,
                    _pebbleColor.G * it.Shade01,
                    _pebbleColor.B * it.Shade01,
                    0.8f);
                DrawCircle(new Vector2(it.X, it.Y), it.Size, color);
            }
        }

        private void DrawTufts(float baseWindX, float baseWindY)
        {
            for (int i = 0; i < _count; i++)
            {
                var it = _items[i];
                if (it.Kind != GroundDetailPlan.ItemKind.Tuft) continue;

                // Localised explosion impulses shove tufts near a blast; the
                // prevailing wind does the rest. Scaled down from the pure
                // clamp so blades bend without tearing off the floor.
                var (ix, iy) = _wind.ImpulseAt(it.X, it.Y);
                float wx = baseWindX + ix * _windScale;
                float wy = baseWindY + iy * _windScale;
                GroundDetailPlan.Sway(_time, wx, wy, it.X, it.Y, out float sx, out float sy);

                var root = new Vector2(it.X, it.Y);
                var tip = new Vector2(it.X + sx * 0.5f, it.Y - it.Size + sy * 0.25f);
                var color = new Color(
                    _tuftColor.R * it.Shade01,
                    _tuftColor.G * it.Shade01,
                    _tuftColor.B * it.Shade01,
                    0.85f);
                DrawLine(root, tip, color, 1f + 0.8f * it.Shade01);
            }
        }

        /// <summary>Viewport rect in world space, snapped and margined.</summary>
        private Rect2 CurrentViewRect()
        {
            var camera = GetViewport().GetCamera2D();
            if (camera == null || !GodotObject.IsInstanceValid(camera))
                return new Rect2(-700f, -700f, 1400f, 1400f);

            Vector2 center = camera.GetScreenCenterPosition();
            Vector2 zoom = camera.Zoom;
            Vector2 viewport = GetViewportRect().Size;
            var world = new Vector2(
                viewport.X / MathF.Max(zoom.X, 0.001f) + 2f * ViewMargin,
                viewport.Y / MathF.Max(zoom.Y, 0.001f) + 2f * ViewMargin);

            Vector2 pos = center - world * 0.5f;
            pos = new Vector2(
                MathF.Round(pos.X / SnapStep) * SnapStep,
                MathF.Round(pos.Y / SnapStep) * SnapStep);
            return new Rect2(pos, world);
        }

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

    }
}
