using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Co-op respawn manager: when a hero dies, schedules a respawn after a
    /// delay instead of ending the run. Dead players wait out the wave (or
    /// respawn mid-wave after the delay) while others continue — game over
    /// only when <see cref="LivesManager"/> reports everyone out.
    ///
    /// Thin engine wrapper over <see cref="LivesManager"/>; respawn timing and
    /// SceneTree spawning live here because they require the engine.
    /// </summary>
    public partial class RespawnManager : Node
    {
        public static RespawnManager Instance { get; private set; } = null!;

        [Export] public PackedScene? HeroScene;
        [Export] public float RespawnDelaySeconds = 2.5f;
        [Export] public float InvulnerableSeconds = 3f;
        [Export] public Vector2[] SpawnPoints = Array.Empty<Vector2>();

        private readonly Dictionary<int, (Vector2 Pos, float Timer)> _pending = new();
        private readonly Dictionary<int, HeroBase> _activeHeroes = new();
        private LivesManager? _lives;

        public override void _Ready()
        {
            Instance = this;
            _lives = GameManager.Instance?.Lives;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            var toRemove = new List<int>();
            foreach (var kvp in _pending)
            {
                var (pos, timer) = kvp.Value;
                timer -= dt;
                if (timer <= 0f)
                {
                    SpawnHero(kvp.Key, pos);
                    toRemove.Add(kvp.Key);
                }
                else
                {
                    _pending[kvp.Key] = (pos, timer);
                }
            }
            foreach (var i in toRemove) _pending.Remove(i);
        }

        /// <summary>Called by HeroBase when it dies. Spends one life and
        /// schedules a respawn if the player can still respawn; otherwise the
        /// player is out (and <see cref="LivesManager.OnAllOut"/> fires when
        /// everyone is out). Returns true when a respawn was scheduled.</summary>
        public bool RequestRespawn(int playerIndex, Vector2 deathPosition)
        {
            var lives = _lives ?? GameManager.Instance?.Lives;
            if (lives == null || !lives.HasLives(playerIndex))
                return false;

            lives.SpendLife(playerIndex);
            if (!lives.HasLives(playerIndex))
            {
                // Spent the last life — player is out, no respawn.
                EventBus.Instance?.EmitPlayerDied(playerIndex);
                return false;
            }

            if (_activeHeroes.TryGetValue(playerIndex, out var old) && old.IsInsideTree())
                old.QueueFree();

            _pending[playerIndex] = (deathPosition, RespawnDelaySeconds);
            GD.Print($"Respawn: player {playerIndex + 1} in {RespawnDelaySeconds}s");
            EventBus.Instance?.EmitPlayerDied(playerIndex);
            return true;
        }

        private void SpawnHero(int playerIndex, Vector2 spawnPos)
        {
            if (HeroScene == null)
            {
                GD.PushError("RespawnManager: HeroScene not set.");
                return;
            }

            Vector2 pos = spawnPos;
            if (SpawnPoints.Length > 0)
            {
                var rng = new RandomNumberGenerator();
                pos = SpawnPoints[rng.RandiRange(0, SpawnPoints.Length - 1)];
            }

            var hero = HeroScene.Instantiate<HeroBase>();
            hero.GlobalPosition = pos;
            hero.PlayerInput = playerIndex == 0
                ? PlayerInput.P1
                : LocalCoopInputManager.Instance?.GetPlayer(playerIndex) ?? PlayerInput.P1;

            var parent = GetTree().CurrentScene;
            parent?.AddChild(hero);
            _activeHeroes[playerIndex] = hero;

            // Keep GameManager.ActiveHero pointing at a live hero (single-player
            // compat; co-op tracks the most recently spawned player).
            var gm = GameManager.Instance;
            if (gm != null && gm.ActiveHero == null)
                gm.ActiveHero = hero;

            // Temporary invulnerability on respawn (forcefield / i-frames).
            hero.GrantForcefield(InvulnerableSeconds);

            var cam = GetNodeOrNull<MultiTargetCamera>("/root/MultiTargetCamera");
            cam?.RegisterTarget(hero);

            EventBus.Instance?.EmitPlayerRespawned(playerIndex);
            GD.Print($"Respawn: player {playerIndex + 1} spawned at {pos}");
        }

        public void RegisterActiveHero(int playerIndex, HeroBase hero)
            => _activeHeroes[playerIndex] = hero;

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null!;
            base._ExitTree();
        }
    }
}
