using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Godot-free entity-id registry. Extracted from EnemyBase's static
    /// dictionary (the F-1 regression site) so the xUnit suite can cover the
    /// registry semantics headlessly: any id that was never inserted —
    /// including negative ids, which callers never produce (they pass Godot
    /// InstanceIds, always positive) — resolves to null. EnemyBase delegates
    /// to a static <see cref="EntityRegistry{TEntity}"/> instance and keeps
    /// only the engine wiring.
    /// </summary>
    public class EntityRegistry<TEntity> where TEntity : class
    {
        private readonly Dictionary<int, TEntity> _byEntityId = new();

        public int Count => _byEntityId.Count;

        public void Register(int entityId, TEntity entity)
            => _byEntityId[entityId] = entity;

        public bool TryGet(int entityId, out TEntity? entity)
            => _byEntityId.TryGetValue(entityId, out entity!);

        public TEntity? Get(int entityId)
            => _byEntityId.TryGetValue(entityId, out TEntity? found) ? found : null;

        public bool Remove(int entityId)
            => _byEntityId.Remove(entityId);

        /// <summary>Returns all registered entities (may include dead ones;
        /// callers filter as needed).</summary>
        public IEnumerable<TEntity> GetAll() => _byEntityId.Values;
    }
}
