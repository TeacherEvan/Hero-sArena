using Godot;

namespace HeroArena
{
    /// <summary>
    /// Pooled projectile node. Activated/deactivated by ObjectPoolManager.
    /// Pre-allocated – no runtime instantiation.
    /// </summary>
    public partial class ProjectileBase : Area2D
    {
        public const float MAX_LIFETIME = 5f;

        public bool IsActive { get; private set; } = false;
        public Vector2 Direction { get; private set; }
        public float Speed { get; private set; }
        public float Damage { get; private set; }
        public DamageType DamageType { get; private set; }
        public int PiercingCount { get; set; } = 0;
        public int PoolIndex { get; private set; }

        private float _lifetime = 0f;

        public override void _Ready()
        {
            BodyEntered += OnBodyEntered;
            Visible = false;
            SetProcess(false);
            SetPhysicsProcess(false);
        }

        public void Activate(Vector2 pos, Vector2 dir, float speed, float damage, DamageType type, int poolIndex)
        {
            GlobalPosition = pos;
            Direction = dir.Normalized();
            Speed = speed;
            Damage = damage;
            DamageType = type;
            PoolIndex = poolIndex;
            _lifetime = 0f;
            IsActive = true;
            Visible = true;
            SetPhysicsProcess(true);
            // Re-enable collision monitoring
            Monitoring = true;
        }

        public void Deactivate()
        {
            IsActive = false;
            Visible = false;
            SetPhysicsProcess(false);
            Monitoring = false;
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            _lifetime += dt;
            if (_lifetime >= MAX_LIFETIME) { Expire(); return; }

            GlobalPosition += Direction * Speed * dt;
        }

        private void OnBodyEntered(Node body)
        {
            if (!IsActive) return;

            if (body is EnemyBase enemy)
            {
                enemy.TakeDamage(Damage, DamageType);
                EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType);

                if (PiercingCount > 0)
                {
                    PiercingCount--;
                    return; // Keep going
                }
                OnHit(body);
            }
            else if (body is HeroBase hero)
            {
                hero.TakeDamage(Damage, DamageType);
                OnHit(body);
            }
        }

        private void OnHit(Node? _)
        {
            GameManager.Instance.PoolManager?.ReturnProjectile(this);
        }

        /// <summary>Expires a projectile that reached max lifetime without hitting anything.</summary>
        private void Expire()
        {
            GameManager.Instance.PoolManager?.ReturnProjectile(this);
        }
    }
}
