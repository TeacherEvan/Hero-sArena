using Godot;

namespace HeroArena
{
    public enum HealerState { FleePlayer, FindCluster, ChannelAura }

    /// <summary>Healer – flees player, finds ally cluster, channels 5% max HP/s AoE heal.</summary>
    public partial class Healer : EnemyBase
    {
        private const float FLEE_RADIUS = 300f;
        private const float HEAL_RADIUS = 150f;
        private const float HEAL_RATE = 0.05f; // 5% max HP per second
        private const float HEAL_TICK = 0.1f;

        private HealerState _aiState = HealerState.FindCluster;
        private float _healTick = 0f;

        protected override void OnSpawn()
        {
            MaxHealth = 60f;
            CurrentHealth = MaxHealth;
            MoveSpeed = 120f;
            Damage = 0f;
            ExpValue = 40;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            float dt = (float)delta;
            if (_aiState == HealerState.ChannelAura)
            {
                _healTick += dt;
                if (_healTick >= HEAL_TICK)
                {
                    _healTick = 0f;
                    HealNearby(dt);
                }
            }
        }

        protected override void UpdateAI()
        {
            if (_hero == null) return;
            float distToHero = DistanceToHero();
            if (distToHero < FLEE_RADIUS)
                _aiState = HealerState.FleePlayer;
            else
            {
                var grid = GameManager.Instance.SpatialGrid;
                if (grid != null)
                {
                    grid.QueryRadius(GlobalPosition, HEAL_RADIUS, out int count);
                    _aiState = count > 2 ? HealerState.ChannelAura : HealerState.FindCluster;
                }
            }
        }

        protected override void Move(float dt)
        {
            if (State == EnemyAIState.Dead || _hero == null) return;
            Vector2 dir;
            if (_aiState == HealerState.FleePlayer)
                dir = (GlobalPosition - _hero.GlobalPosition).Normalized();
            else
                dir = GameManager.Instance.FlowField?.GetFlowDirection(GlobalPosition)
                      ?? (_hero.GlobalPosition - GlobalPosition).Normalized();

            float speed = _aiState == HealerState.ChannelAura ? 0f : MoveSpeed;
            Velocity = dir * speed;
            MoveAndSlide();
        }

        private void HealNearby(float dt)
        {
            var grid = GameManager.Instance.SpatialGrid;
            if (grid == null) return;
            int[] nearby = grid.QueryRadius(GlobalPosition, HEAL_RADIUS, out int count);
            // Healing amount per tick
            // In a full implementation, entity id → EnemyBase lookup would be resolved here
        }
    }
}
