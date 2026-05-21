using Godot;

namespace HeroArena
{
    public enum ApexPhase { IntroAnimation, Phase1Melee, Phase2BulletHell, Phase3Enrage }

    /// <summary>
    /// Apex Boss – spawns every 5 minutes.
    /// Phases: Intro → Phase1 Melee → Phase2 Bullet Hell → Phase3 Enrage.
    /// </summary>
    public partial class Apex : EnemyBase
    {
        private const float PHASE2_THRESHOLD = 0.6f;
        private const float PHASE3_THRESHOLD = 0.3f;
        private const float BULLET_COOLDOWN = 0.15f;
        private const int BULLET_COUNT = 12;

        private ApexPhase _phase = ApexPhase.IntroAnimation;
        private float _introTimer = 2f;
        private float _bulletCooldown = 0f;
        private float _meleeTimer = 0f;

        protected override void OnSpawn()
        {
            MaxHealth = 5000f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 80f;
            Damage = 50f;
            ExpValue = 500;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_bulletCooldown > 0f) _bulletCooldown -= dt;
            if (_meleeTimer > 0f) _meleeTimer -= dt;

            if (_phase == ApexPhase.IntroAnimation)
            {
                _introTimer -= dt;
                if (_introTimer <= 0f) _phase = ApexPhase.Phase1Melee;
            }

            float hpRatio = CurrentHealth / MaxHealth;
            if (hpRatio < PHASE3_THRESHOLD && _phase != ApexPhase.Phase3Enrage)
                _phase = ApexPhase.Phase3Enrage;
            else if (hpRatio < PHASE2_THRESHOLD && _phase == ApexPhase.Phase1Melee)
                _phase = ApexPhase.Phase2BulletHell;
        }

        protected override void UpdateAI()
        {
            switch (_phase)
            {
                case ApexPhase.Phase1Melee: DoMelee(); break;
                case ApexPhase.Phase2BulletHell: DoBulletHell(); break;
                case ApexPhase.Phase3Enrage: DoEnrage(); break;
            }
        }

        private void DoMelee()
        {
            if (_hero == null) return;
            if (DistanceToHero() < 80f && _meleeTimer <= 0f)
            {
                _hero.TakeDamage(Damage, DamageType.Kinetic);
                _meleeTimer = 1.5f;
            }
        }

        private void DoBulletHell()
        {
            if (_bulletCooldown > 0f) return;
            _bulletCooldown = BULLET_COOLDOWN;

            var pool = GameManager.Instance.PoolManager;
            if (pool == null) return;

            float angleStep = Mathf.Tau / BULLET_COUNT;
            for (int i = 0; i < BULLET_COUNT; i++)
            {
                float angle = angleStep * i;
                Vector2 dir = Vector2.Right.Rotated(angle);
                pool.GetProjectile(GlobalPosition, dir, 200f, Damage * 0.5f, DamageType.Energy);
            }
        }

        private void DoEnrage()
        {
            MoveSpeed = 160f; // Double speed
            DoMelee();
            DoBulletHell();
        }
    }
}
