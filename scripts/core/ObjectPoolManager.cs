using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Pre-allocates ALL projectiles and decals at startup.
    /// Zero runtime instantiation during gameplay.
    /// </summary>
    public partial class ObjectPoolManager : Node
    {
        public const int MAX_PROJECTILES = 5000;
        public const int MAX_DECALS = 10000;
        public const int MAX_DESTRUCTIBLES = 1000;

        [Export] public PackedScene ProjectileScene { get; set; } = null!;
        [Export] public PackedScene DecalScene { get; set; } = null!;
        [Export] public PackedScene DestructibleScene { get; set; } = null!;

        private readonly ProjectileBase[] _projectiles = new ProjectileBase[MAX_PROJECTILES];
        private readonly DecalInstance[] _decals = new DecalInstance[MAX_DECALS];
        private readonly DestructibleObject[] _destructibles = new DestructibleObject[MAX_DESTRUCTIBLES];

        // Simple free-list stacks using indices
        private readonly int[] _freeProjectiles = new int[MAX_PROJECTILES];
        private int _freeProjectileTop = 0;

        private readonly int[] _freeDecals = new int[MAX_DECALS];
        private int _freeDecalTop = 0;

        private readonly int[] _freeDestructibles = new int[MAX_DESTRUCTIBLES];
        private int _freeDestructibleTop = 0;

        // Oldest-first circular tracking for decal eviction
        private int _decalEvictHead = 0;
        private readonly int[] _activeDecalOrder = new int[MAX_DECALS];
        private int _activeDecalCount = 0;

        // Tracks whether each decal index is currently checked-out (active in game world)
        private readonly bool[] _isDecalActiveInPool = new bool[MAX_DECALS];
        private readonly bool[] _isProjectileActiveInPool = new bool[MAX_PROJECTILES];
        private readonly bool[] _isDestructibleActiveInPool = new bool[MAX_DESTRUCTIBLES];

        public bool IsReady { get; private set; }

        public override void _Ready()
        {
            if (ProjectileScene == null || DecalScene == null || DestructibleScene == null)
            {
                // In a real project DestructibleScene would also be checked, but to avoid
                // breaking existing scenes that don't have it assigned, we'll just skip
                // allocation if it's null, or warn. We'll throw only if Projectile/Decal are null.
                if (ProjectileScene == null || DecalScene == null)
                {
                    throw new InvalidOperationException(
                        "ObjectPoolManager: ProjectileScene/DecalScene not assigned. " +
                        "Wire them in project.godot or the scene that instantiates this autoload.");
                }
            }

            PreAllocateProjectiles();
            PreAllocateDecals();
            if (DestructibleScene != null)
            {
                PreAllocateDestructibles();
            }
            IsReady = true;
        }

        private void RequireReady()
        {
            if (!IsReady)
                throw new InvalidOperationException("ObjectPoolManager used before _Ready completed.");
        }

        // ── Pre-allocation ────────────────────────────────────────────────────
        private void PreAllocateProjectiles()
        {
            var parent = new Node();
            parent.Name = "ProjectilePool";
            AddChild(parent);

            for (int i = 0; i < MAX_PROJECTILES; i++)
            {
                var p = ProjectileScene.Instantiate<ProjectileBase>();
                parent.AddChild(p);
                p.Deactivate();
                _projectiles[i] = p;
                _freeProjectiles[_freeProjectileTop++] = i;
            }
        }

        private void PreAllocateDecals()
        {
            var parent = new Node();
            parent.Name = "DecalPool";
            AddChild(parent);

            for (int i = 0; i < MAX_DECALS; i++)
            {
                var d = DecalScene.Instantiate<DecalInstance>();
                parent.AddChild(d);
                d.Deactivate();
                _decals[i] = d;
                _freeDecals[_freeDecalTop++] = i;
            }
        }

        private void PreAllocateDestructibles()
        {
            var parent = new Node();
            parent.Name = "DestructiblePool";
            AddChild(parent);

            for (int i = 0; i < MAX_DESTRUCTIBLES; i++)
            {
                var d = DestructibleScene.Instantiate<DestructibleObject>();
                parent.AddChild(d);
                d.Deactivate();
                _destructibles[i] = d;
                _freeDestructibles[_freeDestructibleTop++] = i;
            }
        }

        // ── Projectile API ────────────────────────────────────────────────────
        public ProjectileBase? GetProjectile(ProjectileData data)
        {
            RequireReady();
            if (_freeProjectileTop == 0) return null;
            int idx = _freeProjectiles[--_freeProjectileTop];
            var p = _projectiles[idx];
            p.Activate(data, idx);
            _isProjectileActiveInPool[idx] = true;
            return p;
        }

        public void ReturnProjectile(ProjectileBase p)
        {
            if (!_isProjectileActiveInPool[p.PoolIndex]) return; // already returned
            p.Deactivate();
            _isProjectileActiveInPool[p.PoolIndex] = false;
            _freeProjectiles[_freeProjectileTop++] = p.PoolIndex;
        }

        // ── Decal API ─────────────────────────────────────────────────────────
        private int EvictOldestActiveDecal()
        {
            // Evict the oldest *still-active* decal from the circular order buffer
            int idx = -1;
            while (_activeDecalCount > 0 && idx < 0)
            {
                int candidate = _activeDecalOrder[_decalEvictHead];
                _decalEvictHead = (_decalEvictHead + 1) % MAX_DECALS;
                _activeDecalCount--;
                if (_isDecalActiveInPool[candidate])
                {
                    idx = candidate;
                    _decals[idx].Deactivate();
                    _isDecalActiveInPool[idx] = false;
                }
                // else: already returned via ReturnDecal; skip
            }
            return idx;
        }

        public DecalInstance? GetDecal(Vector2 pos, DecalType type, float size)
        {
            RequireReady();
            int idx = -1;

            if (_freeDecalTop > 0)
            {
                idx = _freeDecals[--_freeDecalTop];
            }
            else
            {
                idx = EvictOldestDecal();
                if (idx < 0) return null; // pool exhausted (shouldn't happen with 10k)
            }

            var d = _decals[idx];
            d.Activate(pos, type, size, idx);
            _isDecalActiveInPool[idx] = true;

            // Record this index at the tail of the circular order buffer
            int tail = (_decalEvictHead + _activeDecalCount) % MAX_DECALS;
            _activeDecalOrder[tail] = idx;
            _activeDecalCount++;

            return d;
        }

        public void ReturnDecal(DecalInstance d)
        {
            if (!_isDecalActiveInPool[d.PoolIndex]) return; // already returned
            d.Deactivate();
            _isDecalActiveInPool[d.PoolIndex] = false;
            _freeDecals[_freeDecalTop++] = d.PoolIndex;
        }

        // ── Destructible API ──────────────────────────────────────────────────
        public DestructibleObject? GetDestructible(Vector2 pos)
        {
            RequireReady();
            if (_freeDestructibleTop == 0 || DestructibleScene == null) return null;
            int idx = _freeDestructibles[--_freeDestructibleTop];
            var d = _destructibles[idx];
            d.Activate(pos, idx);
            _isDestructibleActiveInPool[idx] = true;
            return d;
        }

        public void ReturnDestructible(DestructibleObject d)
        {
            // If it wasn't spawned from the pool (e.g. placed in editor directly),
            // PoolIndex will be -1. It is already deactivated by DestructibleObject.Destroy(),
            // so we simply ignore it here. It will be naturally freed when its parent map unloads.
            if (d.PoolIndex < 0 || d.PoolIndex >= MAX_DESTRUCTIBLES)
            {
                return;
            }

            if (!_isDestructibleActiveInPool[d.PoolIndex]) return; // already returned
            d.Deactivate();
            _isDestructibleActiveInPool[d.PoolIndex] = false;
            _freeDestructibles[_freeDestructibleTop++] = d.PoolIndex;
        }
    }
}
