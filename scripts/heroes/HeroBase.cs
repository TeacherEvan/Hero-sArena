using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// Abstract base for all hero characters.
    /// Handles twin-stick movement, dodge with coyote time, leveling, and damage.
    /// </summary>
    public abstract partial class HeroBase : CharacterBody2D
    {
        // ── Stats ─────────────────────────────────────────────────────────────
        /// <summary>All modifiable hero stats, shared with <see cref="PerkSystem"/>.</summary>
        public HeroStats Stats { get; } = new HeroStats();

        [Export] public float MaxHealth { get => Stats.MaxHealth; set => Stats.MaxHealth = value; }
        [Export] public float MoveSpeed { get => Stats.MoveSpeed; set => Stats.MoveSpeed = value; }

        public float CurrentHealth { get; protected set; }
        public int Level { get; protected set; } = 1;
        public int Experience { get; protected set; } = 0;

        // ── Coyote / dodge ────────────────────────────────────────────────────
        protected const float COYOTE_TIME = 0.15f;
        protected float _dodgeCooldown = 0f;
        protected float _dodgeTimer = 0f;
        protected bool _isDodging = false;

        // ── Subsystems ────────────────────────────────────────────────────────
        protected InputBuffer _inputBuffer = null!;

        /// <summary>When true, <see cref="TakeDamage"/> ignores all incoming damage (used by god mode / i-frame effects).</summary>
        public bool Invulnerable { get; set; }

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            _inputBuffer = new InputBuffer();
            AddChild(_inputBuffer);
            OnReady();
        }

        protected virtual void OnReady() { }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            HandleInput(dt);
            HandleMovement(dt);

            if (_dodgeCooldown > 0f) _dodgeCooldown -= dt;
            if (_dodgeTimer > 0f) _dodgeTimer -= dt;
            if (_dodgeTimer <= 0f) _isDodging = false;
        }

        // ── Input ──────────────────────────────────────────────────────────────
        private void HandleInput(float dt)
        {
            // Buffer attack and dodge inputs
            if (Input.IsActionJustPressed("attack"))
                _inputBuffer.BufferAction("attack");
            if (Input.IsActionJustPressed("dodge"))
                _inputBuffer.BufferAction("dodge");
            if (Input.IsActionJustPressed("ability"))
                _inputBuffer.BufferAction("ability");

            if (_inputBuffer.ConsumeAction("attack")) Attack();
            if (_inputBuffer.ConsumeAction("dodge") && _dodgeCooldown <= 0f) Dodge();
            if (_inputBuffer.ConsumeAction("ability")) UseAbility();
        }

        private void HandleMovement(float dt)
        {
            Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            float speed = _isDodging ? MoveSpeed * 2.5f : MoveSpeed;
            Velocity = inputDir * speed;
            MoveAndSlide();
        }

        // ── Abstract hero actions ─────────────────────────────────────────────
        public abstract void Attack();
        public abstract void Dodge();
        public abstract void UseAbility();

        // ── Damage / death ─────────────────────────────────────────────────────
        public virtual void TakeDamage(float amount, DamageType type)
        {
            if (Invulnerable) return; // god mode / invulnerability
            if (_isDodging) return; // i-frames during dodge
            CurrentHealth -= amount;
            EventBus.Instance.EmitHeroDamaged(amount);
            if (CurrentHealth <= 0f) Die();
        }

        /// <summary>Directly sets current health, clamped between 0 and MaxHealth.</summary>
        public void SetCurrentHealth(float value) => CurrentHealth = Mathf.Clamp(value, 0f, MaxHealth);

        protected virtual void Die()
        {
            GameManager.Instance.TriggerGameOver();
            GameManager.Instance.ActiveHero = null;
            QueueFree();
        }

        // ── Leveling ──────────────────────────────────────────────────────────
        public void AddExperience(int xp)
        {
            Experience += xp;
            int required = LevelProgression.XpRequiredForLevel(Level + 1);
            while (Experience >= required)
            {
                Experience -= required;
                LevelUp();
                required = LevelProgression.XpRequiredForLevel(Level + 1);
            }
        }

        protected virtual void LevelUp()
        {
            Level++;
            EventBus.Instance.EmitLevelUp(Level);
        }

        // ── Utility ───────────────────────────────────────────────────────────
        protected Vector2 GetAimDirection()
        {
            var mouse = GetGlobalMousePosition();
            return (mouse - GlobalPosition).Normalized();
        }

        protected void StartDodge(float duration = 0.25f, float cooldown = 0.8f)
        {
            _isDodging = true;
            _dodgeTimer = duration;
            _dodgeCooldown = cooldown;
        }
    }
}
