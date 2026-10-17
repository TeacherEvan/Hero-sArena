using Godot;

namespace HeroArena
{
    /// <summary>
    /// Ignis – Pyromancer class. Wields the Combustion Overheat Engine.
    /// Builds Heat as he attacks; enters Blazing and Overheat states for extreme fire rates and piercing lances.
    /// Venting heat during dodge unleashes a thermal knockback shockwave; Inferno Nova consumes all heat for catastrophic damage.
    /// </summary>
    public partial class Ignis : HeroBase
    {
        private const float BASE_FLAME_DAMAGE = 18f;
        private const float BASE_FLAME_SPEED = 480f;
        private const float BASE_FIRE_COOLDOWN = 0.14f;
        private const float BASE_NOVA_DAMAGE = 100f;
        private const float BASE_NOVA_RADIUS = 280f;
        private const float NOVA_COOLDOWN = 20f;

        private float _fireCooldown = 0f;
        private float _novaCooldown = 0f;

        private readonly IgnisCombustionState _combustion = new();

        public float CurrentHeat => _combustion.CurrentHeat;
        public CombustionStage Stage => _combustion.Stage;
        public override float AbilityCooldownRemaining => Mathf.Max(0f, _novaCooldown);
        public override float AbilityCooldownTotal => NOVA_COOLDOWN;

        protected override void OnReady()
        {
            MaxHealth = 110f;
            MoveSpeed = 220f;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;

            if (_fireCooldown > 0f) _fireCooldown -= dt;
            if (_novaCooldown > 0f) _novaCooldown -= dt;

            // Heat slowly cools down when not firing
            _combustion.CoolDown(dt);
            QueueRedraw();
        }

        public override void Attack()
        {
            if (_fireCooldown > 0f) return;
            FireCombustionStream();

            float cooldownMult = _combustion.GetAttackCooldownMultiplier() * Stats.AttackCooldownMult;
            _fireCooldown = BASE_FIRE_COOLDOWN * cooldownMult;
        }

        public override void Dodge()
        {
            StartDodge(0.24f, 0.7f * Stats.DodgeCooldownMult);
            AudioManager.Instance?.PlaySound(SoundCue.Dodge, -6f);

            // Thermal Venting Shockwave if at or above Blazing stage
            if (_combustion.Stage != CombustionStage.Normal)
            {
                float vented = _combustion.VentHeat(35f);
                VentThermalShockwave(vented);
            }
            else
            {
                EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.ScorchMark, 60f);
            }
        }

        public override void UseAbility()
        {
            if (_novaCooldown > 0f) return;
            ActivateInfernoNova();
        }

        private void FireCombustionStream()
        {
            var pool = GameManager.Instance.PoolManager;
            if (pool == null) return;

            // Generate heat per volley
            _combustion.AddHeat(4.5f);

            Vector2 baseDir = GetAimDirection();
            float baseDmg = LevelProgression.CalcKineticDamage(BASE_FLAME_DAMAGE, Level) * Stats.DamageMult;

            // Damage and speed scale with heat stage
            bool isOverheat = _combustion.Stage == CombustionStage.Overheat;
            bool isBlazing = _combustion.Stage == CombustionStage.Blazing;

            float effectiveDmg = baseDmg * (isOverheat ? 1.45f : (isBlazing ? 1.2f : 1.0f));
            float effectiveSpeed = BASE_FLAME_SPEED * (isOverheat ? 1.35f : (isBlazing ? 1.15f : 1.0f));

            var data = new ProjectileData(GlobalPosition, baseDir, effectiveSpeed, effectiveDmg, DamageType.Fire);
            var p = pool.GetProjectile(data);
            if (p != null)
            {
                p.PiercingCount = isOverheat ? 5 : (Stats.ProjectilePiercing ? 2 : 0);
            }

            int extraBolts = Stats.ProjectileCountBonus + (isOverheat ? 2 : (isBlazing ? 1 : 0));
            for (int i = 0; i < extraBolts; i++)
            {
                float spreadAngle = ((i + 1) % 2 == 1 ? 1 : -1) * 0.09f * ((i / 2) + 1);
                Vector2 spreadDir = baseDir.Rotated(spreadAngle);
                var spreadData = new ProjectileData(GlobalPosition, spreadDir, effectiveSpeed, effectiveDmg, DamageType.Fire);
                var extra = pool.GetProjectile(spreadData);
                if (extra != null)
                {
                    extra.PiercingCount = isOverheat ? 4 : (Stats.ProjectilePiercing ? 2 : 0);
                }
            }

            AudioManager.Instance?.PlaySound(SoundCue.ShootFire, -10f);
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Fire);
        }

        private void VentThermalShockwave(float ventedHeat)
        {
            float radius = 130f + (ventedHeat * 1.5f);
            float damage = (25f + ventedHeat * 0.8f) * Stats.DamageMult;

            var grid = GameManager.Instance.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(GlobalPosition, radius, out int count);
                for (int i = 0; i < count; i++)
                {
                    if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                        && enemy.State != EnemyAIState.Dead)
                    {
                        enemy.TakeDamage(damage, DamageType.Fire);
                        Vector2 pushDir = (enemy.GlobalPosition - GlobalPosition).Normalized();
                        enemy.Velocity += pushDir * 200f;
                    }
                }
            }

            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.ScorchMark, radius);
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Fire);
        }

        private void ActivateInfernoNova()
        {
            _novaCooldown = NOVA_COOLDOWN;
            float consumedHeat = _combustion.ConsumeAllHeat();

            float rawDmg = LevelProgression.CalcKineticDamage(BASE_NOVA_DAMAGE, Level) * Stats.DamageMult;
            float totalDamage = IgnisCombustionState.CalcNovaDamage(rawDmg, consumedHeat) * Stats.AoeRadiusMult;
            float totalRadius = IgnisCombustionState.CalcNovaRadius(BASE_NOVA_RADIUS, consumedHeat) * Stats.AoeRadiusMult;

            var grid = GameManager.Instance.SpatialGrid;
            if (grid != null)
            {
                int[] hits = grid.QueryRadius(GlobalPosition, totalRadius, out int count);
                for (int i = 0; i < count; i++)
                {
                    if (EnemyBase.TryGetById(hits[i], out var enemy) && enemy != null
                        && enemy.State != EnemyAIState.Dead)
                    {
                        enemy.TakeDamage(totalDamage, DamageType.Fire);
                    }
                }
            }

            AudioManager.Instance?.PlaySound(SoundCue.Ability, -2f);
            AudioManager.Instance?.PlaySound(SoundCue.Explosion, -4f);
            EventBus.Instance.EmitProjectileHit(GlobalPosition, DamageType.Fire);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.ScorchMark, totalRadius);
            EventBus.Instance.EmitPowerupCollected("InfernoNova");
        }

        public override void _Draw()
        {
            base._Draw();
            if (_combustion.CurrentHeat <= 0.1f) return;

            // Draw heat gauge bar above Ignis
            Vector2 barSize = new Vector2(40f, 5f);
            Vector2 barPos = new Vector2(-20f, -42f);

            // Background
            DrawRect(new Rect2(barPos, barSize), new Color(0.1f, 0.1f, 0.1f, 0.7f), true);

            // Fill with heat gradient color
            float fillWidth = (barSize.X - 2f) * (_combustion.CurrentHeat / IgnisCombustionState.MAX_HEAT);
            Color heatColor = _combustion.Stage switch
            {
                CombustionStage.Overheat => new Color(1.0f, 0.95f, 0.6f),
                CombustionStage.Blazing => new Color(1.0f, 0.5f, 0.05f),
                _ => new Color(0.9f, 0.25f, 0.05f)
            };

            DrawRect(new Rect2(barPos + new Vector2(1f, 1f), new Vector2(fillWidth, barSize.Y - 2f)), heatColor, true);
        }
    }
}
