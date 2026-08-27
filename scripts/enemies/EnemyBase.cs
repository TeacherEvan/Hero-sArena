using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    public enum EnemyAIState { Idle, Chase, Attack, Flee, Dead }

    /// <summary>
    /// Abstract base for all enemies. AI updates at 10 Hz via timer.
    /// Registers self with the SpatialHashGrid on spawn.
    /// </summary>
    public abstract partial class EnemyBase : CharacterBody2D
    {
        private static readonly Dictionary<int, EnemyBase> _byEntityId = new();

        public static bool TryGetById(int entityId, out EnemyBase? enemy)
            => _byEntityId.TryGetValue(entityId, out enemy);

        [Export] public float MaxHealth { get; set; } = 50f;
        [Export] public float MoveSpeed { get; set; } = 100f;
        [Export] public float Damage { get; set; } = 10f;
        [Export] public int ExpValue { get; set; } = 10;

        public float CurrentHealth { get; protected set; }
        public EnemyAIState State { get; protected set; } = EnemyAIState.Idle;
        public int ShieldCharges { get; private set; } = 0;

        protected HeroBase? _hero;
        protected int _entityId;

        private float _aiTimer = 0f;
        private const float AI_INTERVAL = 0.1f; // 10 Hz

        private Vector2 _lastGridPos;

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            _entityId = (int)(GetInstanceId() & 0x7FFFFFFF);

            var grid = GameManager.Instance.SpatialGrid;
            grid?.Insert(_entityId, GlobalPosition, 16f);

            _byEntityId[_entityId] = this;
            _lastGridPos = GlobalPosition;

            _hero = GameManager.Instance.ActiveHero;
            OnSpawn();
        }

        public override void _ExitTree()
        {
            _byEntityId.Remove(_entityId);
            base._ExitTree();
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
            if (GlobalPosition.DistanceSquaredTo(_lastGridPos) > 1.0f)
            {
                GameManager.Instance.SpatialGrid?.Update(_entityId, GlobalPosition, 16f);
                _lastGridPos = GlobalPosition;
            }
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
            if (ShieldCharges > 0)
            {
                ShieldCharges--;
                return;
            }
            ApplyHealth(CurrentHealth - amount);
        }

        public void AddShieldCharges(int charges) => ShieldCharges += charges;

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

        protected float DistanceSquaredToHero()
        {
            if (_hero == null) return float.MaxValue;
            return GlobalPosition.DistanceSquaredTo(_hero.GlobalPosition);
        }
    }
}
