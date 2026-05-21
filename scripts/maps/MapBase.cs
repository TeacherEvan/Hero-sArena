using Godot;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>Abstract base for all arena maps.</summary>
    public abstract partial class MapBase : Node2D
    {
        [Export] public TileMapLayer? TileMap { get; set; }
        [Export] public Vector2[] SpawnPoints { get; set; } = System.Array.Empty<Vector2>();
        [Export] public Vector2 ArenaSize { get; set; } = new Vector2(2048f, 2048f);

        public List<DestructibleObject> Destructibles { get; } = new();

        public override void _Ready()
        {
            // Collect all destructible objects in scene tree
            foreach (var child in GetChildren())
            {
                if (child is DestructibleObject d) Destructibles.Add(d);
            }
            OnMapReady();
        }

        protected virtual void OnMapReady() { }
    }
}
