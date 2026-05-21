using Godot;

namespace HeroArena
{
    /// <summary>
    /// Manages the 10,000-decal pool via ObjectPoolManager.
    /// Listens for OnDecalRequested events from the EventBus.
    /// </summary>
    public partial class DecalSystem : Node
    {
        public override void _Ready()
        {
            EventBus.Instance.OnDecalRequested += HandleDecalRequest;
        }

        private void HandleDecalRequest(Vector2 pos, DecalType type, float size)
        {
            GameManager.Instance.PoolManager?.GetDecal(pos, type, size);
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnDecalRequested -= HandleDecalRequest;
        }
    }
}
