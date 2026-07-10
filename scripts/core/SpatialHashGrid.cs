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
        private struct EntityData
        {
            public int MinCx;
            public int MaxCx;
            public int MinCy;
            public int MaxCy;
            public float Radius;
            public Vector2 Pos;
        }

        private readonly Dictionary<int, EntityData> _entityData = new();
        private readonly Dictionary<int, int> _queryStamps = new();
        private int _queryStamp;

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
            int minCx = Mathf.FloorToInt((pos.X - radius) / _cellSize);
            int maxCx = Mathf.FloorToInt((pos.X + radius) / _cellSize);
            int minCy = Mathf.FloorToInt((pos.Y - radius) / _cellSize);
            int maxCy = Mathf.FloorToInt((pos.Y + radius) / _cellSize);

            _entityData[entityId] = new EntityData { MinCx = minCx, MaxCx = maxCx, MinCy = minCy, MaxCy = maxCy, Radius = radius, Pos = pos };
            AddToCells(entityId, minCx, maxCx, minCy, maxCy);
        }

        public void Remove(int entityId)
        {
            if (!_entityData.TryGetValue(entityId, out var data)) return;
            RemoveFromCells(entityId, data.MinCx, data.MaxCx, data.MinCy, data.MaxCy);
            _entityData.Remove(entityId);
            _queryStamps.Remove(entityId);
        }

        public void Update(int entityId, Vector2 newPos, float radius)
        {
            if (!_entityData.TryGetValue(entityId, out var oldData))
            {
                Insert(entityId, newPos, radius);
                return;
            }

            int newMinCx = Mathf.FloorToInt((newPos.X - radius) / _cellSize);
            int newMaxCx = Mathf.FloorToInt((newPos.X + radius) / _cellSize);
            int newMinCy = Mathf.FloorToInt((newPos.Y - radius) / _cellSize);
            int newMaxCy = Mathf.FloorToInt((newPos.Y + radius) / _cellSize);

            if (oldData.MinCx == newMinCx && oldData.MaxCx == newMaxCx &&
                oldData.MinCy == newMinCy && oldData.MaxCy == newMaxCy)
            {
                // Bolt: ⚡ Fast path to skip unnecessary list allocations and manipulations
                // if the entity's bounding box hasn't crossed cell boundaries.
                oldData.Pos = newPos;
                oldData.Radius = radius;
                _entityData[entityId] = oldData;
                return;
            }

            RemoveFromCells(entityId, oldData.MinCx, oldData.MaxCx, oldData.MinCy, oldData.MaxCy);

            oldData.MinCx = newMinCx;
            oldData.MaxCx = newMaxCx;
            oldData.MinCy = newMinCy;
            oldData.MaxCy = newMaxCy;
            oldData.Pos = newPos;
            oldData.Radius = radius;
            _entityData[entityId] = oldData;

            AddToCells(entityId, newMinCx, newMaxCx, newMinCy, newMaxCy);
        }

        public void Clear()
        {
            _cells.Clear();
            _entityData.Clear();
            _queryStamps.Clear();
        }

        // ── Queries ───────────────────────────────────────────────────────────
        /// <summary>Returns pre-allocated result buffer slice. Count in out param.</summary>
        public int[] QueryRadius(Vector2 center, float radius, out int count)
        {
            _resultCount = 0;
            _queryStamp++;
            int minCx = Mathf.FloorToInt((center.X - radius) / _cellSize);
            int maxCx = Mathf.FloorToInt((center.X + radius) / _cellSize);
            int minCy = Mathf.FloorToInt((center.Y - radius) / _cellSize);
            int maxCy = Mathf.FloorToInt((center.Y + radius) / _cellSize);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    long key = HashKey(cx, cy);
                    if (!_cells.TryGetValue(key, out var list)) continue;
                    foreach (int id in list)
                    {
                        if (_resultCount >= _resultBuffer.Length) goto done;
                        if (!_entityData.TryGetValue(id, out var data)) continue;
                        if (_queryStamps.TryGetValue(id, out int stamp) && stamp == _queryStamp) continue;
                        float threshold = radius + data.Radius;
                        if (center.DistanceSquaredTo(data.Pos) <= threshold * threshold)
                        {
                            _queryStamps[id] = _queryStamp;
                            _resultBuffer[_resultCount++] = id;
                        }
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
        private void AddToCells(int entityId, int minCx, int maxCx, int minCy, int maxCy)
        {
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

        private void RemoveFromCells(int entityId, int minCx, int maxCx, int minCy, int maxCy)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    long key = HashKey(cx, cy);
                    if (_cells.TryGetValue(key, out var list))
                        list.Remove(entityId);
                }
            }
        }

        private static long HashKey(int cx, int cy)
            => ((long)(uint)cx) | ((long)(uint)cy << 32);
    }
}
