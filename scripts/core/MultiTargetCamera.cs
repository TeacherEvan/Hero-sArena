using Godot;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Shared single-screen camera for couch co-op. Follows the average
    /// position of alive players and zooms out dynamically based on player
    /// spread, clamped to level bounds. No split-screen — matches Crimsonland's
    /// shared-screen co-op.
    ///
    /// Thin engine wrapper; the averaging/zoom math is pure and could be
    /// extracted if headless coverage is ever needed.
    /// </summary>
    public partial class MultiTargetCamera : Node2D
    {
        public static MultiTargetCamera Instance { get; private set; } = null!;

        [Export] public Camera2D? TargetCamera;
        [Export] public float SmoothSpeed = 8f;
        [Export] public float MinZoom = 0.7f;
        [Export] public float MaxZoom = 1.4f;
        [Export] public float ZoomSpreadFactor = 0.004f;

        // Level bounds in pixels; clamped automatically to viewport if unset.
        public Rect2I LevelBounds;

        public readonly List<HeroBase> Targets = new();

        private bool _boundsSet;

        public override void _Ready()
        {
            Instance = this;
            TargetCamera ??= GetNodeOrNull<Camera2D>("Camera")
                             ?? GetTree().CurrentScene?.GetNodeOrNull<Camera2D>("Camera");
            if (!_boundsSet)
            {
                var vp = GetViewportRect();
                LevelBounds = new Rect2I((int)vp.Size.X / 2, (int)vp.Size.Y / 2,
                                         (int)vp.Size.X, (int)vp.Size.Y);
                _boundsSet = true;
            }
        }

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null!;
            base._ExitTree();
        }

        /// <summary>Set explicit level bounds (in pixels).</summary>
        public void SetLevelBounds(Rect2I bounds)
        {
            LevelBounds = bounds;
            _boundsSet = true;
        }

        public override void _Process(double delta)
        {
            if (Targets.Count == 0 || TargetCamera == null) return;

            float dt = (float)delta;

            // Average position of alive targets.
            Vector2 avg = Vector2.Zero;
            int alive = 0;
            foreach (var t in Targets)
            {
                if (t.IsInsideTree() && t.CurrentHealth > 0f)
                {
                    avg += t.GlobalPosition;
                    alive++;
                }
            }
            if (alive > 0) avg /= alive;
            else if (Targets.Count > 0) avg = Targets[0].GlobalPosition;

            // Dynamic zoom from player spread.
            float minX = avg.X, minY = avg.Y, maxX = avg.X, maxY = avg.Y;
            foreach (var t in Targets)
            {
                if (!t.IsInsideTree()) continue;
                var p = t.GlobalPosition;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            var min = new Vector2(minX, minY);
            var max = new Vector2(maxX, maxY);
            float spread = (max - min).Length();

            float targetZoom = Mathf.Lerp(MaxZoom, MinZoom,
                Mathf.Clamp(spread * ZoomSpreadFactor, 0f, 1f));
            targetZoom = Mathf.Clamp(targetZoom, MinZoom, MaxZoom);

            float curZ = TargetCamera.Zoom.X;
            TargetCamera.Zoom = new Vector2(
                Mathf.Lerp(curZ, targetZoom, dt * SmoothSpeed),
                Mathf.Lerp(curZ, targetZoom, dt * SmoothSpeed));

            // Smooth position + manual clamp (Camera2D Limit clamps display
            // edges, not center — see godotengine/godot#62441).
            Vector2 curPos = TargetCamera.GlobalPosition;
            Vector2 newPos = curPos.Lerp(avg, dt * SmoothSpeed);

            var vpSize = GetViewportRect().Size / TargetCamera.Zoom.X;
            float halfW = vpSize.X / 2f;
            float halfH = vpSize.Y / 2f;

            float left = LevelBounds.Position.X + halfW;
            float right = LevelBounds.Position.X + LevelBounds.Size.X - halfW;
            float top = LevelBounds.Position.Y + halfH;
            float bottom = LevelBounds.Position.Y + LevelBounds.Size.Y - halfH;

            newPos.X = Mathf.Clamp(newPos.X, left, right);
            newPos.Y = Mathf.Clamp(newPos.Y, top, bottom);

            TargetCamera.GlobalPosition = newPos;
        }

        public void RegisterTarget(HeroBase hero)
        {
            if (!Targets.Contains(hero)) Targets.Add(hero);
        }

        public void UnregisterTarget(HeroBase hero) => Targets.Remove(hero);
    }
}
