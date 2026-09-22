using Godot;
using System;
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
        // Input actions are set up in code (not project.godot) so the map
        // survives engine rewrites of the config file. One-shot per run so
        // scene reloads never stack duplicate events.
        private static bool _inputReady;

        private Camera2D? _camera;

        public override void _Ready()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                GD.PrintErr("MainBootstrap: GameManager autoload missing; scene wiring skipped.");
                return;
            }

            EnsureInputMap();
            EnsureSystems(gm);
            WireWaveManager(gm);
            SpawnHero(gm);
            _camera = GetParent().GetNodeOrNull<Camera2D>("Camera");
            // Direct boot (run/main_scene) has no menu to call StartGame; the
            // menu sets PendingStartAfterSceneChange and starts it later, so
            // only auto-start when nobody else will. Exactly one path fires.
            if (!gm.PendingStartAfterSceneChange && gm.CurrentState == GameState.MainMenu)
                gm.StartGame();
        }

        public override void _Process(double delta)
        {
            var hero = GameManager.Instance?.ActiveHero;
            if (hero == null || _camera == null || !IsInstanceValid(_camera)) return;
            // Smooth camera follow so the hero never walks out of view.
            float t = 1f - MathF.Pow(0.001f, (float)delta);
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(hero.GlobalPosition, t);
        }

        private void EnsureInputMap()
        {
            if (_inputReady) return;
            _inputReady = true;
            AddKeyAction("move_left", Key.A, Key.Left);
            AddKeyAction("move_right", Key.D, Key.Right);
            AddKeyAction("move_up", Key.W, Key.Up);
            AddKeyAction("move_down", Key.S, Key.Down);
            AddMouseAction("attack", MouseButton.Left);
            AddKeyAction("ability", Key.E);
            AddMouseAction("ability", MouseButton.Right);
            AddKeyAction("dodge", Key.Space, Key.Shift);
        }

        private static void AddKeyAction(string action, params Key[] keys)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            foreach (var k in keys)
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
        }

        private static void AddMouseAction(string action, params MouseButton[] buttons)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            foreach (var b in buttons)
                InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = b });
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
