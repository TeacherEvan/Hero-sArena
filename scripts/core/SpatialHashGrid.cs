using Godot;
using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Custom spatial hash grid for O(1) average-case collision queries.
    /// Pre-allocates result buffers to avoid heap allocation in hot paths.
    /// </summary>
    public class SpatialHashGrid
    {
        private readonly int _cellSize;
        private readonly Dictionary<long, List<int>> _cells = new();

        // Per-entity tracking so we can remove/update efficiently
        private readonly Dictionary<int, (int cellX, int cellY, float radius)> _entityData = new();

        // Pre-allocated result buffer to avoid List allocation in hot path
        private readonly int[] _resultBuffer;
        private int _resultCount;

        public SpatialHashGrid(int cellSize = 64, int maxResults = 512)
        {
            _cellSize = cellSize;
            _resultBuffer = new int[maxResults];
        }

        // ── Insertion / removal ───────────────────────────────────────────────
        public void Insert(int entityId, Vector2 pos, float radius)
        {
            int cx = Mathf.FloorToInt(pos.X / _cellSize);
            int cy = Mathf.FloorToInt(pos.Y / _cellSize);
            _entityData[entityId] = (cx, cy, radius);
            AddToCells(entityId, pos, radius);
        }

        public void Remove(int entityId)
        {
            if (!_entityData.TryGetValue(entityId, out var data)) return;
            RemoveFromCells(entityId, data.cellX, data.cellY, data.radius);
            _entityData.Remove(entityId);
        }

        public void Update(int entityId, Vector2 newPos, float radius)
        {
            Remove(entityId);
            Insert(entityId, newPos, radius);
        }

        public void Clear()
        {
            _cells.Clear();
            _entityData.Clear();
        }

        // ── Queries ───────────────────────────────────────────────────────────
        /// <summary>Returns pre-allocated result buffer slice. Count in out param.</summary>
        public int[] QueryRadius(Vector2 center, float radius, out int count)
        {
            _resultCount = 0;
            int minCx = Mathf.FloorToInt((center.X - radius) / _cellSize);
            int maxCx = Mathf.FloorToInt((center.X + radius) / _cellSize);
            int minCy = Mathf.FloorToInt((center.Y - radius) / _cellSize);
            int maxCy = Mathf.FloorToInt((center.Y + radius) / _cellSize);

            float r2 = radius * radius;
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    long key = HashKey(cx, cy);
                    if (!_cells.TryGetValue(key, out var list)) continue;
                    foreach (int id in list)
                    {
                        if (_resultCount >= _resultBuffer.Length) goto done;
                        _resultBuffer[_resultCount++] = id;
                    }
                }
            }
            done:
            count = _resultCount;
            return _resultBuffer;
        }

        public int[] QueryAABB(Rect2 bounds, out int count)
        {
            _resultCount = 0;
            int minCx = Mathf.FloorToInt(bounds.Position.X / _cellSize);
            int maxCx = Mathf.FloorToInt(bounds.End.X / _cellSize);
            int minCy = Mathf.FloorToInt(bounds.Position.Y / _cellSize);
            int maxCy = Mathf.FloorToInt(bounds.End.Y / _cellSize);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    long key = HashKey(cx, cy);
                    if (!_cells.TryGetValue(key, out var list)) continue;
                    foreach (int id in list)
                    {
                        if (_resultCount >= _resultBuffer.Length) goto done;
                        _resultBuffer[_resultCount++] = id;
                    }
                }
            }
            done:
            count = _resultCount;
            return _resultBuffer;
        }

        // ── Internal helpers ──────────────────────────────────────────────────
        private void AddToCells(int entityId, Vector2 pos, float radius)
        {
            int minCx = Mathf.FloorToInt((pos.X - radius) / _cellSize);
            int maxCx = Mathf.FloorToInt((pos.X + radius) / _cellSize);
            int minCy = Mathf.FloorToInt((pos.Y - radius) / _cellSize);
            int maxCy = Mathf.FloorToInt((pos.Y + radius) / _cellSize);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    long key = HashKey(cx, cy);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<int>(8);
                        _cells[key] = list;
                    }
                    list.Add(entityId);
                }
            }
        }

        private void RemoveFromCells(int entityId, int cx, int cy, float radius)
        {
            int minCx = cx - Mathf.CeilToInt(radius / _cellSize);
            int maxCx = cx + Mathf.CeilToInt(radius / _cellSize);
            int minCy = cy - Mathf.CeilToInt(radius / _cellSize);
            int maxCy = cy + Mathf.CeilToInt(radius / _cellSize);

            for (int x = minCx; x <= maxCx; x++)
            {
                for (int y = minCy; y <= maxCy; y++)
                {
                    long key = HashKey(x, y);
                    if (_cells.TryGetValue(key, out var list))
                        list.Remove(entityId);
                }
            }
        }

        private static long HashKey(int cx, int cy)
            => ((long)(uint)cx) | ((long)(uint)cy << 32);
    }
}
