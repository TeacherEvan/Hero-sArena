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

        private float _currentHealth;

        public override void _Ready()
        {
            _currentHealth = Health;
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

            // Notify systems
            EventBus.Instance.EmitEnvironmentDestroyed(GlobalPosition, 64f);
            EventBus.Instance.EmitDecalRequested(GlobalPosition, DecalType.CraterMark, 64f);

            // Unblock flow field cell
            var ff = GameManager.Instance.FlowField;
            if (ff != null)
            {
                var cell = ff.WorldToGrid(GlobalPosition);
                ff.SetBlocked(cell, false);
            }

            QueueFree();
        }
    }
}
