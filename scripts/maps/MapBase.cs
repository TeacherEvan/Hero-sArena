using Godot;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>Base for all arena maps. Concrete (not abstract) so map scenes
    /// can attach it directly; override <see cref="OnMapReady"/> per map when
    /// map-specific setup is needed.</summary>
    public partial class MapBase : Node2D
    {
        [Export] public TileMapLayer? TileMap { get; set; }
        [Export] public Vector2[] SpawnPoints { get; set; } = System.Array.Empty<Vector2>();
        [Export] public Vector2 ArenaSize { get; set; } = new Vector2(2048f, 2048f);

        public List<DestructibleObject> Destructibles { get; } = new();

        public override void _Ready()
        {
            EnsureFloor();
            EnsureBoundary();
            EnsureBoundaryVisual();

            // Collect all destructible objects in scene tree
            foreach (var child in GetChildren())
            {
                if (child is DestructibleObject d) Destructibles.Add(d);
            }
            OnMapReady();
        }

        protected virtual void OnMapReady() { }

        private void EnsureFloor()
        {
            if (GetNodeOrNull<Node>("Floor") != null) return;

            var floorTex = GD.Load<Texture2D>("res://res/environment/arena_floor.png");
            if (floorTex == null) return;

            var floor = new Sprite2D
            {
                Name = "Floor",
                Texture = floorTex,
                TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
                RegionEnabled = true,
                RegionRect = new Rect2(0, 0, ArenaSize.X, ArenaSize.Y),
                Position = Vector2.Zero,
                ZIndex = -100
            };
            AddChild(floor);
            MoveChild(floor, 0);
        }

        private void EnsureBoundary()
        {
            if (GetNodeOrNull<Node>("ArenaBounds") != null) return;

            var bounds = new StaticBody2D { Name = "ArenaBounds" };
            float hx = ArenaSize.X * 0.5f;
            float hy = ArenaSize.Y * 0.5f;
            float thickness = 64f;

            AddWall(bounds, new Vector2(0, -hy - thickness * 0.5f), new Vector2(ArenaSize.X + thickness * 2f, thickness));
            AddWall(bounds, new Vector2(0, hy + thickness * 0.5f), new Vector2(ArenaSize.X + thickness * 2f, thickness));
            AddWall(bounds, new Vector2(-hx - thickness * 0.5f, 0), new Vector2(thickness, ArenaSize.Y + thickness * 2f));
            AddWall(bounds, new Vector2(hx + thickness * 0.5f, 0), new Vector2(thickness, ArenaSize.Y + thickness * 2f));

            AddChild(bounds);
        }

        private static void AddWall(StaticBody2D body, Vector2 pos, Vector2 size)
        {
            var col = new CollisionShape2D
            {
                Position = pos,
                Shape = new RectangleShape2D { Size = size }
            };
            body.AddChild(col);
        }

        private void EnsureBoundaryVisual()
        {
            if (GetNodeOrNull<Node>("BoundaryBorder") != null) return;

            float hx = ArenaSize.X * 0.5f;
            float hy = ArenaSize.Y * 0.5f;
            var border = new Line2D
            {
                Name = "BoundaryBorder",
                Width = 8f,
                DefaultColor = new Color(0.25f, 0.55f, 0.85f, 0.7f),
                Points = new[]
                {
                    new Vector2(-hx, -hy),
                    new Vector2(hx, -hy),
                    new Vector2(hx, hy),
                    new Vector2(-hx, hy),
                    new Vector2(-hx, -hy)
                },
                ZIndex = -50
            };
            AddChild(border);
        }
    }
}
