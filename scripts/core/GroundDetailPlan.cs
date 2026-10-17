using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free planning for the living ground (the illusion layer behind
    /// <c>scripts/vfx/LivingGround.cs</c>): the arena floor is scattered with
    /// deterministic, position-hashed detail — grass tufts, pebbles and cracks
    /// — generated per grid cell from a fixed seed, so the same spot always
    /// grows the same detail (no popping, no stored state). Tufts sway with
    /// the shared <see cref="AmbientWindState"/>.
    ///
    /// Pure functions only: the node enumerates visible cells, calls
    /// <see cref="GenerateRegion"/> into a pre-allocated buffer, and draws.
    /// Covered by GroundDetailPlanTests (determinism, bounds, budgets, sway).
    /// </summary>
    public static class GroundDetailPlan
    {
        /// <summary>World-space size of one detail cell.</summary>
        public const int CellSize = 160;

        /// <summary>Hard cap of items emitted per cell.</summary>
        public const int MaxItemsPerCell = 6;

        /// <summary>Buffer cap: a busy viewport never draws more than this many items.</summary>
        public const int MaxItemsPerCall = 512;

        /// <summary>Sway is clamped to this many pixels (positive and negative).</summary>
        public const float MaxSway = 12f;

        /// <summary>Detail is inset from the cell border so items never straddle seams.</summary>
        public const float CellMargin = 14f;

        /// <summary>Kinds of ground detail.</summary>
        public enum ItemKind : byte
        {
            Tuft,
            Pebble,
            Crack,
        }

        /// <summary>One ground detail item in world space.</summary>
        public struct Item
        {
            public float X;
            public float Y;
            public ItemKind Kind;
            public float Size;     // tuft height / pebble radius / crack length, px
            public float Shade01;  // 0..1 tone variation for the node's draw
            public float Rotation; // radians (crack direction, pebble lean)
        }

        /// <summary>
        /// Integer-lattice hash (x, y, seed) → uint. Position-based, so
        /// detail is stable across frames, runs and machines with no state
        /// kept anywhere.
        /// </summary>
        public static uint Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = seed + 0x9E3779B9u;
                h = (h ^ (uint)x) * 0x85EBCA6Bu;
                h = (h ^ (uint)y) * 0xC2B2AE35u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Hash → float in [0, 1).</summary>
        public static float Hash01(int x, int y, uint seed)
            => (Hash(x, y, seed) & 0xFFFFFFu) / 16777216f;

        /// <summary>
        /// Generates the detail items of one cell into <paramref name="buffer"/>
        /// (clamped to <paramref name="maxItems"/> and the buffer length).
        /// Returns the item count written.
        /// </summary>
        public static int Generate(int cellX, int cellY, uint seed, Item[] buffer, int maxItems)
            => Generate(cellX, cellY, seed, buffer, 0, maxItems);

        /// <summary>
        /// Generates one cell's detail at <paramref name="offset"/> in
        /// <paramref name="buffer"/>. Returns the item count written.
        /// Offset-based so region generation composes without scratch state.
        /// </summary>
        public static int Generate(int cellX, int cellY, uint seed, Item[] buffer, int offset, int maxItems)
        {
            if (buffer == null || buffer.Length == 0 || maxItems <= 0) return 0;
            if (offset < 0 || offset >= buffer.Length) return 0;

            int budget = Math.Min(Math.Min(maxItems, buffer.Length - offset), MaxItemsPerCell);
            uint h0 = Hash(cellX, cellY, seed);
            int count = 1 + (int)(h0 % 5u); // 1..5
            if (count > budget) count = budget;

            float baseX = cellX * (float)CellSize;
            float baseY = cellY * (float)CellSize;
            float span = CellSize - 2f * CellMargin;

            for (int i = 0; i < count; i++)
            {
                uint h = Hash(cellX + i * 4099, cellY - i * 7919, seed ^ 0xA5A5A5A5u);
                float fx = (h & 0xFFu) / 256f;
                float fy = ((h >> 8) & 0xFFu) / 256f;
                int roll = (int)((h >> 16) % 100u);
                float sizeRoll = ((h >> 20) & 0xFFu) / 256f;
                float shade = 0.35f + 0.65f * (((h >> 24) & 0xFFu) / 256f);
                float rotation = ((h >> 12) & 0xFFu) / 256f * MathF.PI * 2f;

                ItemKind kind;
                float size;
                if (roll < 52) // tufts dominate: the ground is alive
                {
                    kind = ItemKind.Tuft;
                    size = 7f + 8f * sizeRoll;
                }
                else if (roll < 82)
                {
                    kind = ItemKind.Pebble;
                    size = 1.5f + 1.7f * sizeRoll;
                }
                else
                {
                    kind = ItemKind.Crack;
                    size = 10f + 16f * sizeRoll;
                }

                buffer[offset + i] = new Item
                {
                    X = baseX + CellMargin + fx * span,
                    Y = baseY + CellMargin + fy * span,
                    Kind = kind,
                    Size = size,
                    Shade01 = shade,
                    Rotation = rotation,
                };
            }
            return count;
        }

        /// <summary>
        /// Generates every cell intersecting the rect (row-major scan order,
        /// deterministic) into <paramref name="buffer"/>, stopping at
        /// <paramref name="maxItems"/>. When the region's worst-case item count
        /// exceeds the budget the scan thins cells with a deterministic stride,
        /// so a too-large view loses detail evenly instead of losing its
        /// bottom edge. Returns the count written.
        /// </summary>
        public static int GenerateRegion(
            float minX, float minY, float maxX, float maxY, uint seed, Item[] buffer, int maxItems)
        {
            if (buffer == null || buffer.Length == 0 || maxItems <= 0) return 0;
            if (maxX < minX || maxY < minY) return 0;

            CellRangeFor(minX, minY, maxX, maxY, out int x0, out int y0, out int x1, out int y1);

            // Even thinning when the worst case overflows the budget: a
            // deterministic stride keeps detail spread over the whole region
            // instead of losing its bottom edge to the row-major early stop.
            long worstCase = (long)(x1 - x0 + 1) * (y1 - y0 + 1) * MaxItemsPerCell;
            int stride = worstCase <= maxItems ? 1 : (int)((worstCase + maxItems - 1) / maxItems);

            int written = 0;
            int cell = 0;
            for (int cy = y0; cy <= y1 && written < maxItems; cy++)
            {
                for (int cx = x0; cx <= x1 && written < maxItems; cx++, cell++)
                {
                    if (stride > 1 && cell % stride != 0) continue;
                    written += Generate(cx, cy, seed, buffer, written, maxItems - written);
                }
            }
            return written;
        }

        /// <summary>Inclusive cell range covering a world-space rect (floor division, negatives safe).</summary>
        public static void CellRangeFor(
            float minX, float minY, float maxX, float maxY,
            out int x0, out int y0, out int x1, out int y1)
        {
            x0 = (int)MathF.Floor(minX / CellSize);
            y0 = (int)MathF.Floor(minY / CellSize);
            x1 = (int)MathF.Floor(maxX / CellSize);
            y1 = (int)MathF.Floor(maxY / CellSize);
        }

        /// <summary>
        /// Wind sway offset for a ground item: the shared wind vector pushes
        /// it, and a position-hashed flutter keeps every item breathing on its
        /// own phase. Deterministic for (time, wind, position); bounded by
        /// <see cref="MaxSway"/> so tufts never visibly tear.
        /// </summary>
        public static void Sway(
            float time, float windX, float windY, float x, float y, out float swayX, out float swayY)
        {
            int ix = (int)MathF.Floor(x);
            int iy = (int)MathF.Floor(y);
            float phase = Hash01(ix, iy, 0x51ED5EEDu) * MathF.PI * 2f;

            float flutterX = MathF.Sin(time * 1.9f + phase) * 1.6f;
            float flutterY = MathF.Cos(time * 1.5f + phase) * 1.1f;

            swayX = Math.Clamp(windX * 0.14f + flutterX, -MaxSway, MaxSway);
            swayY = Math.Clamp(windY * 0.10f + flutterY, -MaxSway, MaxSway);
        }
    }
}
