using Godot;

namespace HeroArena
{
    /// <summary>
    /// Havoc – Demolitionist class. Wields Heavy Ordnance, Cluster Munitions, and Blast Recoil.
    /// Firing heavy rockets exerts physical recoil momentum for rocket-jump kiting.
    /// Rocket Rebound leaves high-explosive blast shockwaves; Orbital Barrage calls down 3 sequential carpet-bombing artillery strikes.
    /// </summary>
    public partial class Havoc : HeroBase
    {
        private const float BASE_ROCKET_DAMAGE = 65f;
        private const float BASE_ROCKET_SPEED = 400f;
        private const float BASE_ROCKET_COOLDOWN = 0.50f;
        private const float BASE_BARRAGE_DAMAGE = 175f;
        private const float BASE_BARRAGE_RADIUS = 280f;
        private const float BARRAGE_COOLDOWN = 28f;

        private float _rocketCooldown = 0f;
        private float _barrageCooldown = 0f;

        public override float AbilityCooldownRemaining => Mathf.Max(0f, _barrageCooldown);
        public override float AbilityCooldownTotal => BARRAGE_COOLDOWN;

        protected override void OnReady()
        {
            MaxHealth = 135f;
            MoveSpeed = 185f;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;

            if (_rocketCooldown > 0f) _rocketCooldown -= dt;
            if (_barrageCooldown > 0f) _barrageCooldown -= dt;
        }

        public override void Attack()
        {
            if (_rocketCooldown > 0f) return;
            FireClusterRocket();
            _rocketCooldown = BASE_ROCKET_COOLDOWN * Stats.AttackCooldownMult;
        }

        public override void Dodge()
        {
            StartDodge(0.30f, 0.85f * Stats.DodgeCooldownMult);

            var grid = GameManager.Instance?.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(GlobalPosition, 110f, out int count, out bool _);
                for (int i = 0; i < count; i++)
                {
                    if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                        && enemy.State != EnemyAIState.Dead)
                    {
                        enemy.TakeDamage(40f * Stats.DamageMult, DamageType.Explosive);
                        Vector2 blastPush = (enemy.GlobalPosition - GlobalPosition).Normalized();
                        enemy.Velocity += blastPush * 250f;
                    }
                }
            }

            EventBus.Instance?.EmitDecalRequested(GlobalPosition, DecalType.ExplosionScorch, 80f);
            EventBus.Instance?.EmitProjectileHit(GlobalPosition, DamageType.Explosive);
            AudioManager.Instance?.PlaySound(SoundCue.Explosion, -6f);
        }

        public override void UseAbility()
        {
            if (_barrageCooldown > 0f) return;
            ActivateOrbitalBarrage();
        }

        private void FireClusterRocket()
        {
            var pool = GameManager.Instance?.PoolManager;
            if (pool == null) return;

            Vector2 baseDir = GetAimDirection();
            float dmg = LevelProgression.CalcKineticDamage(BASE_ROCKET_DAMAGE, Level) * Stats.DamageMult;

            Velocity += HavocArtilleryState.CalcRecoilImpulse(baseDir, 280f);

            var data = new ProjectileData(GlobalPosition, baseDir, BASE_ROCKET_SPEED, dmg, DamageType.Explosive);
            var p = pool.GetProjectile(data);
            if (p != null)
            {
                p.PiercingCount = Stats.ProjectilePiercing ? 3 : 1;
            }

            int clusterCount = 2 + Stats.ProjectileCountBonus;
            var clusterDirs = HavocArtilleryState.CalcClusterSpreadDirections(baseDir, clusterCount, 0.45f);

            for (int i = 0; i < clusterDirs.Length; i++)
            {
                var subData = new ProjectileData(
                    GlobalPosition,
                    clusterDirs[i],
                    BASE_ROCKET_SPEED * 0.85f,
                    dmg * 0.45f,
                    DamageType.Explosive);

                var sub = pool.GetProjectile(subData);
                if (sub != null)
                {
                    sub.PiercingCount = 0;
                }
            }

            AudioManager.Instance?.PlaySound(SoundCue.Explosion, -6f);
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Explosive);
        }

        private void ActivateOrbitalBarrage()
        {
            _barrageCooldown = BARRAGE_COOLDOWN;
            Vector2 targetPos = GlobalPosition + GetAimDirection() * 500f;

            AudioManager.Instance?.PlaySound(SoundCue.Ability, -1f);
            EventBus.Instance.EmitPowerupCollected("OrbitalBarrage");

            var strikes = HavocArtilleryState.GetSequentialStrikes();
            for (int i = 0; i < strikes.Count; i++)
            {
                var strike = strikes[i];
                if (strike.DelaySeconds <= 0f)
                {
                    ExecuteStrike(targetPos, strike);
                }
                else
                {
                    GetTree().CreateTimer(strike.DelaySeconds).Timeout += () =>
                    {
                        if (IsInstanceValid(this))
                        {
                            ExecuteStrike(targetPos, strike);
                        }
                    };
                }
            }
        }

        private void ExecuteStrike(Vector2 baseTarget, ArtilleryStrike strike)
        {
            Vector2 impactPos = baseTarget + strike.TargetOffset;
            float damage = LevelProgression.CalcKineticDamage(BASE_BARRAGE_DAMAGE, Level)
                * Stats.DamageMult * Stats.AoeRadiusMult * strike.DamageMultiplier;
            float radius = BASE_BARRAGE_RADIUS * Stats.AoeRadiusMult * strike.RadiusMultiplier;

            var grid = GameManager.Instance?.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(impactPos, radius, out int count, out bool _);
                for (int i = 0; i < count; i++)
                {
                    if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                        && enemy.State != EnemyAIState.Dead)
                    {
                        enemy.TakeDamage(damage, DamageType.Explosive);
                    }
                }
            }

            AudioManager.Instance?.PlaySound(SoundCue.Explosion, -2f);
            EventBus.Instance.EmitProjectileHit(impactPos, DamageType.Explosive);
            EventBus.Instance.EmitDecalRequested(impactPos, DecalType.CraterMark, radius);
            EventBus.Instance.EmitDecalRequested(impactPos, DecalType.ExplosionScorch, radius * 0.8f);
        }
    }
}
