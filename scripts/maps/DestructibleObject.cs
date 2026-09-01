using Godot;

namespace HeroArena
{
    /// <summary>
    /// Destructible cover/wall. On destruction emits environment-destroyed event
    /// and marks the cell blocked in the flow field.
    /// </summary>
    public partial class DestructibleObject : Node2D
    {
        [Export] public float Health { get; set; } = 100f;
        public bool IsDestroyed { get; private set; } = false;
        public int PoolIndex { get; private set; } = -1;
        public bool IsActive { get; private set; } = true;

        private float _currentHealth;

        public override void _Ready()
        {
            _currentHealth = Health;
        }

        public void Activate(Vector2 pos, int poolIndex)
        {
            GlobalPosition = pos;
            PoolIndex = poolIndex;
            IsActive = true;
            IsDestroyed = false;
            _currentHealth = Health;
            Visible = true;
            ProcessMode = ProcessModeEnum.Inherit;
        }

        public void Deactivate()
        {
            IsActive = false;
            Visible = false;
            ProcessMode = ProcessModeEnum.Disabled;
        }

        public void TakeDamage(float amount)
        {
            if (IsDestroyed) return;
            _currentHealth -= amount;
            if (_currentHealth <= 0f) Destroy();
        }

        private void Destroy()
        {
            IsDestroyed = true;
            // Immediately hide and disable the object to avoid QueueFree overhead.
            Deactivate();

            // Unblock the flow field first so any listener that re-queries the
            // pathfinder (e.g. AI rerouting) sees consistent state.
            var ff = GameManager.Instance.FlowField;
            if (ff != null)
            {
                var cell = ff.WorldToGrid(GlobalPosition);
                ff.SetBlocked(cell, false);
            }

            // Now notify systems.
            EventBus.Instance.EmitEnvironmentDestroyed(GlobalPosition, 64f);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.CraterMark, 64f);

            // If this object came from the global pool (dynamically spawned), return it.
            // If it was map-placed, the pool manager will just ignore it (it's already deactivated).
            if (GameManager.Instance.PoolManager != null)
            {
                GameManager.Instance.PoolManager.ReturnDestructible(this);
            }
        }
    }
}
