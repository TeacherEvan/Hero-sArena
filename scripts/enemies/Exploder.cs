using Godot;

namespace HeroArena
{
    /// <summary>Exploder – detonates on death or proximity. Leaves acid pool decal.</summary>
    public partial class Exploder : EnemyBase
    {
        private const float DETONATE_RADIUS = 80f;
        private const float DETONATE_DAMAGE = 80f;
        private const float PROX_TRIGGER = 50f;

        protected override void OnSpawn()
        {
            MaxHealth = 30f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 160f;
            Damage = DETONATE_DAMAGE;
            ExpValue = 20;
            State = EnemyAIState.Chase;
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            if (DistanceToHero() < PROX_TRIGGER) Detonate();
        }

        protected override void Die()
        {
            Detonate();
        }

        private void Detonate()
        {
            // AoE damage to hero if in radius
            if (_hero != null && GlobalPosition.DistanceTo(_hero.GlobalPosition) <= DETONATE_RADIUS)
                _hero.TakeDamage(DETONATE_DAMAGE, DamageType.Explosive);

            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.AcidPool, DETONATE_RADIUS);
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Acid);

            State = EnemyAIState.Dead;
            GameManager.Instance.SpatialGrid?.Remove(_entityId);
            EventBus.Instance.EmitEnemyKilled(this);
            QueueFree();
        }
    }
}
