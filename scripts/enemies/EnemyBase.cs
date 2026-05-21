using Godot;
using System;

namespace HeroArena
{
    public enum EnemyAIState { Idle, Chase, Attack, Flee, Dead }

    /// <summary>
    /// Abstract base for all enemies. AI updates at 10 Hz via timer.
    /// Registers self with the SpatialHashGrid on spawn.
    /// </summary>
    public abstract partial class EnemyBase : CharacterBody2D
    {
        [Export] public float MaxHealth { get; set; } = 50f;
        [Export] public float MoveSpeed { get; set; } = 100f;
        [Export] public float Damage { get; set; } = 10f;
        [Export] public int ExpValue { get; set; } = 10;

        public float CurrentHealth { get; protected set; }
        public EnemyAIState State { get; protected set; } = EnemyAIState.Idle;

        protected HeroBase? _hero;
        protected int _entityId;

        private float _aiTimer = 0f;
        private const float AI_INTERVAL = 0.1f; // 10 Hz

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            _entityId = (int)(GetInstanceId() & 0x7FFFFFFF);

            var grid = GameManager.Instance.SpatialGrid;
            grid?.Insert(_entityId, GlobalPosition, 16f);

            _hero = GameManager.Instance.ActiveHero;
            OnSpawn();
        }

        protected virtual void OnSpawn() { }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            _aiTimer += dt;
            if (_aiTimer >= AI_INTERVAL)
            {
                _aiTimer -= AI_INTERVAL;
                UpdateAI();
            }

            if (State != EnemyAIState.Dead)
                Move(dt);

            // Update spatial grid
            GameManager.Instance.SpatialGrid?.Update(_entityId, GlobalPosition, 16f);
        }

        protected abstract void UpdateAI();

        protected virtual void Move(float dt)
        {
            if (_hero == null || State == EnemyAIState.Dead) return;
            var flow = GameManager.Instance.FlowField;
            Vector2 dir = flow != null
                ? flow.GetFlowDirection(GlobalPosition)
                : (_hero.GlobalPosition - GlobalPosition).Normalized();

            Velocity = dir * MoveSpeed;
            MoveAndSlide();
        }

        public virtual void TakeDamage(float amount, DamageType type = DamageType.Kinetic)
        {
            ApplyHealth(CurrentHealth - amount);
        }

        public void SetCurrentHealth(float value)
        {
            ApplyHealth(value);
        }

        private void ApplyHealth(float newValue)
        {
            CurrentHealth = Mathf.Clamp(newValue, 0f, MaxHealth);
            if (CurrentHealth <= 0f) Die();
        }

        protected virtual void Die()
        {
            if (State == EnemyAIState.Dead) return;
            State = EnemyAIState.Dead;
            GameManager.Instance.SpatialGrid?.Remove(_entityId);
            EventBus.Instance.EmitEnemyKilled(this);
            QueueFree();
        }

        protected float DistanceToHero()
        {
            if (_hero == null) return float.MaxValue;
            return GlobalPosition.DistanceTo(_hero.GlobalPosition);
        }
    }
}
