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

        [Export] public PauseMenu? PauseMenu { get; set; }
        [Export] public GameOver? GameOver { get; set; }

        private double _runStartTime;
        private Camera2D? _camera;

        public override void _Ready()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                GD.PrintErr("MainBootstrap: GameManager autoload missing; scene wiring skipped.");
                return;
            }

            _runStartTime = Time.GetUnixTimeFromSystem();
            EventBus.Instance.OnGameOver += HandleGameOver;
            EnsureInputMap();
            EnsureSystems(gm);
            WireWaveManager(gm);
            EnsureEnvironment(gm);
            SpawnHero(gm);
            _camera = GetParent().GetNodeOrNull<Camera2D>("Camera");
            // Start the run exactly once after the scene is wired.
            // - Menu-driven boot: MainMenu sets PendingStartAfterSceneChange and
            //   changes scene; MainBootstrap owns starting the run here (a
            //   CallDeferred on the freed MainMenu node would be dropped).
            // - Direct boot (run/main_scene): PendingStartAfterSceneChange is false
            //   and state is MainMenu, so we auto-start here instead.
            // Exactly one path fires: StartGame transitions MainMenu→Playing and
            // kicks off WaveManager, so it must not be called twice.
            if (gm.PendingStartAfterSceneChange)
            {
                gm.PendingStartAfterSceneChange = false;
                if (!gm.PendingTutorial)
                    gm.StartGame();
                gm.PendingTutorial = false;
            }
            else if (gm.CurrentState == GameState.MainMenu)
            {
                gm.StartGame();
            }

            // Tutorial handoff (R1): MainMenu set PendingTutorial + called
            // GameManager.StartTutorial() before the scene change. Spawn the
            // Tutorial onboarding node so it can spawn drones and show prompts.
            if (gm.PendingTutorial || gm.CurrentState == GameState.Tutorial)
            {
                var tutorialScene = GD.Load<PackedScene>("res://scenes/ui/Tutorial.tscn");
                if (tutorialScene != null)
                {
                    var tutorial = tutorialScene.Instantiate<Tutorial>();
                    GetParent().AddChild(tutorial);
                    gm.PendingTutorial = false;
                }
                else
                {
                    GD.PrintErr("MainBootstrap: Tutorial.tscn missing; tutorial cannot run.");
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            {
                if (PauseMenu == null) return;
                PauseMenu.Toggle();
            }
        }

        private void HandleGameOver()
        {
            if (GameOver == null) return;
            var gm = GameManager.Instance;
            if (gm == null) return;

            int runSeconds = (int)(Time.GetUnixTimeFromSystem() - _runStartTime);
            GameOver.Show(gm.KillCount, gm.Score, gm.CurrentWave, runSeconds, gm.CurrentDifficulty);
        }

        public override void _Process(double delta)
        {
            // When MultiTargetCamera (co-op) is active it owns camera follow;
            // the single-hero follow below only runs for single-player.
            if (MultiTargetCamera.Instance != null) return;

            var hero = GameManager.Instance?.ActiveHero;
            if (hero == null || !IsInstanceValid(hero) || _camera == null || !IsInstanceValid(_camera)) return;
            // Smooth camera follow so the hero never walks out of view.
            // Frame-rate independent: exponent scaled by delta * 60 (60 Hz reference).
            float t = 1f - MathF.Pow(0.001f, (float)delta * 60f);
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(hero.GlobalPosition, t);
        }

        private static void EnsureInputMap()
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

        private void EnsureEnvironment(GameManager gm)
        {
            // Dark godlike atmosphere over the playfield: void background with
            // a celestial glow. Environment is a Resource (not a Node), so it is
            // wrapped in a WorldEnvironment node that can sit in the scene tree.
            // BGMode.Canvas is the documented 2D glow path ("Using glow in 2D"):
            // the gameplay canvas is the glow source, and the viewport clear
            // color (kept dark in project.godot) fills wherever it is empty.
            // GlowHdrThreshold must sit below 1.0 because 2D rendering is SDR;
            // Bloom adds the soft lift on top. BackgroundCanvasMaxLayer = 0
            // keeps CanvasLayer UI (HUD/UI/HitFlash/distortion) out of the glow
            // source, so only the world blooms.
            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Canvas,
                BackgroundColor = VisualTheme.Background,
                GlowEnabled = true,
                GlowStrength = 0.5f,
                GlowBloom = 0.08f,
                GlowHdrThreshold = 0.9f,
                BackgroundCanvasMaxLayer = 0,
            };
            AddChild(new WorldEnvironment { Environment = env });
        }

        private void SpawnHero(GameManager gm)
        {
            string path = gm.PendingHeroClass switch
            {
                HeroClass.Atlas => "res://scenes/heroes/Atlas.tscn",
                HeroClass.Zephyr => "res://scenes/heroes/Zephyr.tscn",
                HeroClass.Synapse => "res://scenes/heroes/Synapse.tscn",
                HeroClass.Volt => "res://scenes/heroes/Volt.tscn",
                HeroClass.Ignis => "res://scenes/heroes/Ignis.tscn",
                HeroClass.Toxis => "res://scenes/heroes/Toxis.tscn",
                HeroClass.Havoc => "res://scenes/heroes/Havoc.tscn",
                _ => "res://scenes/heroes/Atlas.tscn",
            };
            var scene = GD.Load<PackedScene>(path);
            if (scene == null)
            {
                GD.PrintErr($"MainBootstrap: hero scene missing at {path}.");
                return;
            }
            var hero = scene.Instantiate<HeroBase>();
            gm.MetaProgression.ApplyToHeroStats(hero.Stats);
            hero.SetCurrentHealth(hero.Stats.MaxHealth);
            var parent = GetParent().GetNodeOrNull<Node>("EntitiesLayer") ?? GetParent();
            parent.AddChild(hero);
            hero.GlobalPosition = Vector2.Zero;
            gm.ActiveHero = hero;
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnGameOver -= HandleGameOver;
        }
    }
}
