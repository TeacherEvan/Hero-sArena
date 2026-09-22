using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HeroArena
{
    /// <summary>
    /// Scene bootstrap for Main.tscn. Runs once per game-scene load and wires
    /// the combat runtime that MainMenu.StartGame expects to find:
    /// SpatialGrid, FlowField, WaveManager reference, map spawn points, and
    /// the selected hero (GameManager.PendingHeroClass, default Atlas).
    /// All steps are null-tolerant so a partially-wired scene degrades
    /// instead of crashing.
    /// </summary>
    public partial class MainBootstrap : Node
    {
        public override void _Ready()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                GD.PrintErr("MainBootstrap: GameManager autoload missing; scene wiring skipped.");
                return;
            }

            EnsureSystems(gm);
            WireWaveManager(gm);
            SpawnHero(gm);
        }

        private void EnsureSystems(GameManager gm)
        {
            if (gm.SpatialGrid == null)
                gm.SpatialGrid = new SpatialHashGrid(64, 64);

            if (gm.FlowField == null || !IsInstanceValid(gm.FlowField))
            {
                var flow = new FlowFieldPathfinder();
                AddChild(flow);
                gm.FlowField = flow;
            }
        }

        private void WireWaveManager(GameManager gm)
        {
            var wm = GetParent().GetNodeOrNull<WaveManager>("WaveManager");
            if (wm == null)
            {
                GD.PrintErr("MainBootstrap: no WaveManager node in Main scene; waves will not spawn.");
                return;
            }
            gm.WaveManager = wm;
            if (wm.SpawnPoints == null || wm.SpawnPoints.Length == 0)
                wm.SpawnPoints = CollectMapSpawnPoints();
        }

        private Vector2[] CollectMapSpawnPoints()
        {
            var scene = GetTree().CurrentScene;
            if (scene == null) return System.Array.Empty<Vector2>();
            var points = scene.FindChildren("*", "Marker2D", true, false)
                .Where(m => m.Name.ToString().StartsWith("SpawnPoint"))
                .OrderBy(m => m.Name.ToString())
                .Select(m => ((Node2D)m).GlobalPosition)
                .ToArray();
            if (points.Length == 0)
                GD.PrintErr("MainBootstrap: map has no SpawnPoint* markers; WaveManager.SpawnPoints left empty.");
            return points;
        }

        private void SpawnHero(GameManager gm)
        {
            string path = gm.PendingHeroClass switch
            {
                HeroClass.Atlas => "res://scenes/heroes/Atlas.tscn",
                HeroClass.Zephyr => "res://scenes/heroes/Zephyr.tscn",
                HeroClass.Synapse => "res://scenes/heroes/Synapse.tscn",
                HeroClass.Volt => "res://scenes/heroes/Volt.tscn",
                _ => "res://scenes/heroes/Atlas.tscn",
            };
            var scene = GD.Load<PackedScene>(path);
            if (scene == null)
            {
                GD.PrintErr($"MainBootstrap: hero scene missing at {path}.");
                return;
            }
            var hero = scene.Instantiate<HeroBase>();
            var parent = GetParent().GetNodeOrNull<Node>("EntitiesLayer") ?? GetParent();
            parent.AddChild(hero);
            hero.GlobalPosition = Vector2.Zero;
            gm.ActiveHero = hero;
        }
    }
}
