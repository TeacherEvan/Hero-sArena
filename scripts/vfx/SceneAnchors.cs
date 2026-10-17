using Godot;

namespace HeroArena
{
    /// <summary>
    /// Shared Godot-side scene lookups for the ambient nodes
    /// (<c>BiomeAtmosphere</c>, <c>AmbientWildlife</c>, <c>LivingGround</c>):
    /// which biome the current scene represents, and where its arena lies in
    /// world space. One home for the resolution rules, so a fix lands in every
    /// consumer at once (the three nodes used to carry identical copies).
    ///
    /// Arena convention: <see cref="MapBase.ArenaSize"/> is the full extent of
    /// the play area, <b>centred on the map node's origin</b> — the hero spawns
    /// at world zero and the playable markers sit at ±800 (see
    /// <c>scenes/maps/RuinedMetropolis.tscn</c>).
    /// </summary>
    public static class SceneAnchors
    {
        /// <summary>
        /// Biome of the current scene: the map node's scene path wins (the map
        /// is the identity), then the scene's own path. Unknown paths resolve
        /// to the default biome.
        /// </summary>
        public static BiomeKind ResolveBiome(Node node)
        {
            var scene = node.GetTree().CurrentScene;
            var map = scene?.GetNodeOrNull<Node>("Map");
            string path = map?.SceneFilePath ?? scene?.SceneFilePath ?? string.Empty;
            return BiomeAtmosphereState.BiomeForMapPath(path);
        }

        /// <summary>
        /// World-space arena rect (centre, half extents). The map's
        /// <see cref="MapBase.ArenaSize"/> is centred on the map node's origin;
        /// without a map node, falls back to a 2048×2048 box at world zero so
        /// nodes still boot standalone (probes, dev scenes).
        /// </summary>
        public static (Vector2 Center, Vector2 HalfExtents) ResolveArena(Node node)
        {
            var scene = node.GetTree().CurrentScene;
            var mapNode = scene?.GetNodeOrNull<Node2D>("Map");
            if (mapNode is MapBase map)
                return (mapNode.GlobalPosition, map.ArenaSize * 0.5f);
            return (Vector2.Zero, new Vector2(1024f, 1024f));
        }
    }
}
