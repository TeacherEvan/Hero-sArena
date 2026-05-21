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

        [Export] public PackedScene ProjectileScene { get; set; } = null!;
        [Export] public PackedScene DecalScene { get; set; } = null!;

        private readonly ProjectileBase[] _projectiles = new ProjectileBase[MAX_PROJECTILES];
        private readonly DecalInstance[] _decals = new DecalInstance[MAX_DECALS];

        // Simple free-list stacks using indices
        private readonly int[] _freeProjectiles = new int[MAX_PROJECTILES];
        private int _freeProjectileTop = 0;

        private readonly int[] _freeDecals = new int[MAX_DECALS];
        private int _freeDecalTop = 0;

        // Oldest-first circular tracking for decal eviction
        private int _decalEvictHead = 0;
        private readonly int[] _activeDecalOrder = new int[MAX_DECALS];
        private int _activeDecalCount = 0;

        // Tracks whether each decal index is currently checked-out (active in game world)
        private readonly bool[] _isDecalActiveInPool = new bool[MAX_DECALS];

        public override void _Ready()
        {
            PreAllocateProjectiles();
            PreAllocateDecals();
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

        // ── Projectile API ────────────────────────────────────────────────────
        public ProjectileBase? GetProjectile(Vector2 pos, Vector2 dir, float speed, float damage, DamageType type)
        {
            if (_freeProjectileTop == 0) return null;
            int idx = _freeProjectiles[--_freeProjectileTop];
            var p = _projectiles[idx];
            p.Activate(pos, dir, speed, damage, type, idx);
            return p;
        }

        public void ReturnProjectile(ProjectileBase p)
        {
            p.Deactivate();
            _freeProjectiles[_freeProjectileTop++] = p.PoolIndex;
        }

        // ── Decal API ─────────────────────────────────────────────────────────
        public DecalInstance? GetDecal(Vector2 pos, DecalType type, float size)
        {
            int idx;
            if (_freeDecalTop > 0)
            {
                idx = _freeDecals[--_freeDecalTop];
            }
            else
            {
                // Evict the oldest *still-active* decal from the circular order buffer
                idx = -1;
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
    }
}
