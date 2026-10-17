using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Guided "First Wave" onboarding (R1). Teaches the three core verbs
    /// (move/dodge, attack, collect XP) with slow, telegraphed enemies and
    /// on-screen prompts, then hands off to the normal perk flow.
    ///
    /// Tutorial runs inside GameState.Tutorial. WaveManager's Weibull
    /// spawning is suppressed; Tutorial.cs spawns 1–3 slow Drones one at a
    /// time instead. After ~4 kills the hero levels up (bonus XP in tutorial
    /// mode), UpgradeMenu opens with 3 beginner perks, and completing it
    /// returns to MainMenu with Normal mode unlocked.
    /// </summary>
    public partial class Tutorial : Node
    {
        public enum Step { Move, Attack, Dodge, CollectXP, LevelUp, Done }

        [Export] public Label PromptLabel { get; set; } = null!;
        [Export] public Label ProgressLabel { get; set; } = null!;

        // Tutorial balance — soft, slow, one at a time
        private const int MaxDrones = 3;
        private const float DroneSpawnDelay = 2.5f; // seconds between drones
        private const int KillsToLevelUp = 4;
        private const int TutorialXpPerKill = 25; // bonus XP so level-up is fast

        private PackedScene? _droneScene;
        private Marker2D? _spawnPoint;
        private Node2D? _entitiesLayer;

        private Step _currentStep = Step.Move;
        private int _enemiesKilled;
        private int _dronesSpawned;
        private int _dronesAlive;
        private float _spawnTimer;
        private bool _tutorialComplete;

        public override void _Ready()
        {
            // Resolve references from the scene tree (Main.tscn layout)
            _droneScene = GD.Load<PackedScene>("res://scenes/enemies/Drone.tscn");
            var main = GetParent();
            _spawnPoint = main.GetNodeOrNull<Marker2D>("SpawnPoint");
            _entitiesLayer = main.GetNodeOrNull<Node2D>("EntitiesLayer");

            EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
            EventBus.Instance.OnLevelUp += OnLevelUp;
            EventBus.Instance.OnPerkChosen += OnPerkChosen;
            ShowPrompt(_currentStep);
            UpdateProgress();
            // Spawn the first drone after a short delay so the player reads the prompt
            _spawnTimer = 1.5f;
        }

        public override void _Process(double delta)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Tutorial)
                return;
            if (_tutorialComplete) return;

            _spawnTimer -= (float)delta;
            if (_spawnTimer <= 0f && _dronesSpawned < MaxDrones && _dronesAlive == 0)
            {
                _spawnTimer = DroneSpawnDelay;
                SpawnNextDrone();
            }
        }

        private void ShowPrompt(Step step)
        {
            PromptLabel.Text = step switch
            {
                Step.Move => "MOVE → WASD / left stick",
                Step.Attack => "ATTACK → Left Click (weapon fires automatically)",
                Step.Dodge => "DODGE → Space / Shift",
                Step.CollectXP => "COLLECT XP → Walk over glowing gems from kills",
                Step.LevelUp => "LEVEL UP → Pick a perk (game pauses)",
                Step.Done => "Tutorial complete! You're ready for Normal mode.",
                _ => ""
            };
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            ProgressLabel.Text = $"Step {(int)_currentStep + 1} of 6  •  Kills: {_enemiesKilled}/{KillsToLevelUp}";
        }

        private void SpawnNextDrone()
        {
            if (_droneScene == null) return;
            var drone = _droneScene.Instantiate<EnemyBase>();
            drone.GlobalPosition = _spawnPoint?.GlobalPosition ?? Vector2.Zero;
            (_entitiesLayer ?? GetTree().CurrentScene).AddChild(drone);
            GameManager.Instance.IncrementEnemyCount();
            _dronesSpawned++;
            _dronesAlive++;
        }

        private void OnEnemyKilled(EnemyBase enemy)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Tutorial)
                return;
            _dronesAlive--;
            _enemiesKilled++;

            // Award bonus XP so the player levels up quickly in tutorial
            var hero = GameManager.Instance.ActiveHero;
            hero?.AddExperience(TutorialXpPerKill);

            UpdateProgress();

            if (_enemiesKilled >= KillsToLevelUp && !_tutorialComplete)
            {
                _currentStep = Step.LevelUp;
                ShowPrompt(Step.LevelUp);
                // AddExperience above triggers OnLevelUp → UpgradeMenu opens
            }
            else if (_enemiesKilled == 1 && _currentStep == Step.Move)
            {
                _currentStep = Step.Attack;
                ShowPrompt(Step.Attack);
            }
            else if (_enemiesKilled == 2 && _currentStep == Step.Attack)
            {
                _currentStep = Step.Dodge;
                ShowPrompt(Step.Dodge);
            }
            else if (_enemiesKilled == 3 && _currentStep == Step.Dodge)
            {
                _currentStep = Step.CollectXP;
                ShowPrompt(Step.CollectXP);
            }
        }

        private void OnLevelUp(int level)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Tutorial)
                return;
            _currentStep = Step.LevelUp;
            ShowPrompt(Step.LevelUp);
        }

        private void OnPerkChosen(PerkType perk)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Tutorial)
                return;
            _tutorialComplete = true;
            _currentStep = Step.Done;
            ShowPrompt(Step.Done);
            GameManager.Instance.CompleteTutorial();
            // Return to main menu after a brief moment so the player reads the done prompt
            CallDeferred(MethodName.ReturnToMainMenu);
        }

        private void ReturnToMainMenu()
        {
            var mainMenu = GD.Load<PackedScene>("res://scenes/ui/MainMenu.tscn");
            if (mainMenu != null)
                GetTree().ChangeSceneToPacked(mainMenu);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
                EventBus.Instance.OnLevelUp -= OnLevelUp;
                EventBus.Instance.OnPerkChosen -= OnPerkChosen;
            }
        }
    }
}
