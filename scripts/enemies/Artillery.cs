using Godot;

namespace HeroArena
{
    /// <summary>Artillery – stationary, fires explosive mortar arcs.</summary>
    public partial class Artillery : EnemyBase
    {
        private const float FIRE_RANGE = 600f;
        private const float FIRE_COOLDOWN = 3f;
        private float _fireCooldown = 0f;

        protected override void OnSpawn()
        {
            MaxHealth = 80f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 0f; // stationary
            Damage = 60f;
            ExpValue = 30;
            State = EnemyAIState.Attack;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            if (_fireCooldown > 0f) _fireCooldown -= (float)delta;
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            if (DistanceToHero() <= FIRE_RANGE && _fireCooldown <= 0f)
            {
                FireMortar();
                _fireCooldown = FIRE_COOLDOWN;
            }
        }

        protected override void Move(float dt) { } // Does not move

        private void FireMortar()
        {
            if (_hero == null) return;
            var pool = GameManager.Instance.PoolManager;
            if (pool == null) return;
            Vector2 dir = (_hero.GlobalPosition - GlobalPosition).Normalized();
            pool.GetProjectile(GlobalPosition, dir, 250f, Damage, DamageType.Explosive);
        }
    }
}
