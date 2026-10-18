using Godot;

namespace HeroArena
{
    /// <summary>
    /// Toxis – Alchemist class. Wields the Corrosive Catalyst and Rupture Siphon.
    /// Inflicts stacking Corrosion that slows enemies and amplifies damage.
    /// Vaporizes into toxic mist to phase through swarms; Caustic Eruption detonates all corroded targets in cascading chain-reactions and siphons HP.
    /// </summary>
    public partial class Toxis : HeroBase
    {
        private const float BASE_ACID_DAMAGE = 30f;
        private const float BASE_ACID_SPEED = 430f;
        private const float BASE_ACID_COOLDOWN = 0.20f;
        private const float BASE_ERUPTION_DAMAGE = 80f;
        private const float BASE_ERUPTION_RADIUS = 260f;
        private const float ERUPTION_COOLDOWN = 22f;

        private float _acidCooldown = 0f;
        private float _eruptionCooldown = 0f;

        private readonly ToxisCatalystState _catalyst = new();

        public override float AbilityCooldownRemaining => Mathf.Max(0f, _eruptionCooldown);
        public override float AbilityCooldownTotal => ERUPTION_COOLDOWN;

        protected override void OnReady()
        {
            MaxHealth = 95f;
            MoveSpeed = 240f;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;

            if (_acidCooldown > 0f) _acidCooldown -= dt;
            if (_eruptionCooldown > 0f) _eruptionCooldown -= dt;

            ApplyCorrosionAuras();
        }

        public override void Attack()
        {
            if (_acidCooldown > 0f) return;
            LobAcidCanister();
            _acidCooldown = BASE_ACID_COOLDOWN * Stats.AttackCooldownMult;
        }

        public override void Dodge()
        {
            // Vapor Mist Phase: Invulnerable + phases through enemies
            StartDodge(0.28f, 0.75f * Stats.DodgeCooldownMult);
            AudioManager.Instance?.PlaySound(SoundCue.Dodge, -6f);

            var grid = GameManager.Instance?.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(GlobalPosition, 90f, out int count, out bool _);
                for (int i = 0; i < count; i++)
                {
                    _catalyst.AddCorrosion(hits[i], 2);
                }
            }

            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.AcidPool, 85f);
        }

        public override void UseAbility()
        {
            if (_eruptionCooldown > 0f) return;
            ActivateCausticEruption();
        }

        private void LobAcidCanister()
        {
            var pool = GameManager.Instance?.PoolManager;
            if (pool == null) return;

            Vector2 baseDir = GetAimDirection();
            float dmg = LevelProgression.CalcKineticDamage(BASE_ACID_DAMAGE, Level) * Stats.DamageMult;

            var data = new ProjectileData(GlobalPosition, baseDir, BASE_ACID_SPEED, dmg, DamageType.Acid);
            var p = pool.GetProjectile(data);
            if (p != null)
            {
                p.PiercingCount = Stats.ProjectilePiercing ? 5 : 3;
            }

            int extraBolts = Stats.ProjectileCountBonus;
            for (int i = 0; i < extraBolts; i++)
            {
                float spreadAngle = ((i + 1) % 2 == 1 ? 1 : -1) * 0.12f * ((i / 2) + 1);
                Vector2 spreadDir = baseDir.Rotated(spreadAngle);
                var spreadData = new ProjectileData(GlobalPosition, spreadDir, BASE_ACID_SPEED, dmg, DamageType.Acid);
                var extra = pool.GetProjectile(spreadData);
                if (extra != null)
                {
                    extra.PiercingCount = Stats.ProjectilePiercing ? 5 : 3;
                }
            }

            var grid = GameManager.Instance?.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(GlobalPosition + (baseDir * 100f), 110f, out int count, out bool _);
                for (int i = 0; i < count; i++)
                {
                    _catalyst.AddCorrosion(hits[i], 1);
                }
            }

            AudioManager.Instance?.PlaySound(SoundCue.ShootAcid, -8f);
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Acid);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.AcidPool, 45f);
        }

        private void ActivateCausticEruption()
        {
            _eruptionCooldown = ERUPTION_COOLDOWN;
            float baseDmg = LevelProgression.CalcKineticDamage(BASE_ERUPTION_DAMAGE, Level)
                * Stats.DamageMult * Stats.AoeRadiusMult;
            float radius = BASE_ERUPTION_RADIUS * Stats.AoeRadiusMult;

            var pool = GameManager.Instance?.PoolManager;
            if (pool != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = Mathf.DegToRad(i * 30f);
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var data = new ProjectileData(GlobalPosition, dir, BASE_ACID_SPEED * 0.95f, baseDmg * 0.5f, DamageType.Acid);
                    var p = pool.GetProjectile(data);
                    if (p != null) p.PiercingCount = 3;
                }
            }

            var detonatedList = _catalyst.DetonateAll();
            int totalStacks = 0;

            foreach (var kvp in detonatedList)
            {
                int entityId = kvp.Key;
                int stacks = kvp.Value;
                totalStacks += stacks;

                if (EnemyBase.TryGetById(entityId, out var enemy) && enemy != null
                    && enemy.State != EnemyAIState.Dead)
                {
                    float ruptureDmg = ToxisCatalystState.CalcRuptureDamage(baseDmg, stacks);
                    enemy.TakeDamage(ruptureDmg, DamageType.Acid);
                    EventBus.Instance.EmitDecalRequested(enemy.GlobalPosition, DecalType.AcidPool, 60f);
                }
            }

            if (totalStacks > 0)
            {
                float healing = ToxisCatalystState.CalcSiphonHealing(totalStacks);
                SetCurrentHealth(CurrentHealth + healing);
                EventBus.Instance.EmitPowerupCollected($"+{(int)healing} HP Siphoned");
            }

            var grid = GameManager.Instance?.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(GlobalPosition, radius, out int count, out bool _);
                for (int i = 0; i < count; i++)
                {
                    if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                        && enemy.State != EnemyAIState.Dead)
                    {
                        enemy.TakeDamage(baseDmg, DamageType.Acid);
                    }
                }
            }

            AudioManager.Instance?.PlaySound(SoundCue.Ability, -2f);
            AudioManager.Instance?.PlaySound(SoundCue.ShootAcid, -4f);
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Acid);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.AcidPool, radius);
            EventBus.Instance.EmitPowerupCollected("CausticEruption");
        }

        private void ApplyCorrosionAuras()
        {
            var grid = GameManager.Instance?.SpatialGrid;
            if (grid == null) return;

            int[] hits = grid.QueryRadius(GlobalPosition, 350f, out int count, out bool _);
            for (int i = 0; i < count; i++)
            {
                int entityId = hits[i];
                int stacks = _catalyst.GetCorrosion(entityId);
                if (stacks > 0 && EnemyBase.TryGetById(entityId, out var enemy) && enemy != null)
                {
                    float slowFactor = _catalyst.GetSpeedMultiplier(entityId);
                    enemy.Velocity *= slowFactor;
                }
            }
        }
    }
}
