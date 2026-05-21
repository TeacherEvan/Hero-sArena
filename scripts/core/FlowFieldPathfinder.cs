using Godot;
using System;
using System.Threading;

namespace HeroArena
{
    /// <summary>
    /// Flow-field pathfinder on a 128×128 grid running on a dedicated worker thread.
    /// Updates at ~4 Hz (every 250 ms). Thread-safe reads via double-buffering.
    /// </summary>
    public partial class FlowFieldPathfinder : Node
    {
        public const int GRID_W = 128;
        public const int GRID_H = 128;
        public const int CELL_COUNT = GRID_W * GRID_H; // 16 384

        [Export] public float CellWorldSize { get; set; } = 16f;
        [Export] public Vector2 GridOrigin { get; set; } = Vector2.Zero;

        // Direction encoding: 0=None, 1..8 = 8-directional
        // 0=None, 1=E, 2=NE, 3=N, 4=NW, 5=W, 6=SW, 7=S, 8=SE
        private static readonly Vector2[] DirVectors = new Vector2[]
        {
            Vector2.Zero,
            new( 1,  0), new( 1, -1), new( 0, -1), new(-1, -1),
            new(-1,  0), new(-1,  1), new( 0,  1), new( 1,  1),
        };

        // Double-buffered direction arrays
        private readonly byte[] _bufA = new byte[CELL_COUNT];
        private readonly byte[] _bufB = new byte[CELL_COUNT];
        private byte[] _readBuf;
        private byte[] _writeBuf;

        private readonly bool[] _blocked = new bool[CELL_COUNT];
        private readonly object _targetLock = new();
        private Vector2 _targetWorldPos = Vector2.Zero;

        private Thread? _workerThread;
        private volatile bool _running;
        private readonly AutoResetEvent _wakeEvent = new(false);

        // BFS queue pre-allocated
        private readonly int[] _bfsQueue = new int[CELL_COUNT];

        // Static direction tables – allocated once, reused across all BFS calls
        private static readonly int[] DirDx  = {  1,  1,  0, -1, -1, -1,  0,  1 };
        private static readonly int[] DirDy  = {  0, -1, -1, -1,  0,  1,  1,  1 };
        private static readonly byte[] OppDir = {  5,  6,  7,  8,  1,  2,  3,  4 };

        public override void _Ready()
        {
            _readBuf = _bufA;
            _writeBuf = _bufB;

            _running = true;
            _workerThread = new Thread(WorkerLoop) { IsBackground = true, Name = "FlowField" };
            _workerThread.Start();
        }

        public override void _ExitTree()
        {
            _running = false;
            _wakeEvent.Set();
            _workerThread?.Join(500);
        }

        // ── Public API (called from main thread) ──────────────────────────────
        public void UpdateTargetPosition(Vector2 worldPos)
        {
            lock (_targetLock) { _targetWorldPos = worldPos; }
            _wakeEvent.Set();
        }

        /// <summary>Returns flow direction vector for a world-space position.</summary>
        public Vector2 GetFlowDirection(Vector2 worldPos)
        {
            var cell = WorldToGrid(worldPos);
            if (cell.X < 0 || cell.X >= GRID_W || cell.Y < 0 || cell.Y >= GRID_H)
                return Vector2.Zero;
            byte dir = _readBuf[cell.Y * GRID_W + cell.X];
            return DirVectors[dir < DirVectors.Length ? dir : 0];
        }

        public Vector2I WorldToGrid(Vector2 worldPos)
        {
            var rel = worldPos - GridOrigin;
            return new Vector2I(
                Mathf.FloorToInt(rel.X / CellWorldSize),
                Mathf.FloorToInt(rel.Y / CellWorldSize));
        }

        public Vector2 GridToWorld(Vector2I gridPos)
        {
            return GridOrigin + new Vector2(
                gridPos.X * CellWorldSize + CellWorldSize * 0.5f,
                gridPos.Y * CellWorldSize + CellWorldSize * 0.5f);
        }

        public void SetBlocked(Vector2I cell, bool blocked)
        {
            if (cell.X < 0 || cell.X >= GRID_W || cell.Y < 0 || cell.Y >= GRID_H) return;
            _blocked[cell.Y * GRID_W + cell.X] = blocked;
        }

        // ── Worker thread ─────────────────────────────────────────────────────
        private void WorkerLoop()
        {
            while (_running)
            {
                _wakeEvent.WaitOne(250); // wake on signal OR every 250 ms (4 Hz)
                if (!_running) break;
                Vector2 target;
                lock (_targetLock) { target = _targetWorldPos; }
                ComputeFlowField(target);
                SwapBuffers();
            }
        }

        private void ComputeFlowField(Vector2 targetWorld)
        {
            var targetCell = WorldToGrid(targetWorld);

            // Clear write buffer
            Array.Fill(_writeBuf, (byte)0);

            int head = 0, tail = 0;
            int tx = Mathf.Clamp(targetCell.X, 0, GRID_W - 1);
            int ty = Mathf.Clamp(targetCell.Y, 0, GRID_H - 1);

            // Cost field (ushort) local to this call
            Span<ushort> cost = stackalloc ushort[CELL_COUNT];
            cost.Fill(ushort.MaxValue);

            int startIdx = ty * GRID_W + tx;
            cost[startIdx] = 0;
            _bfsQueue[tail++] = startIdx;

            while (head != tail)
            {
                int idx = _bfsQueue[head++];
                if (head >= CELL_COUNT) head = 0;
                int cy = idx / GRID_W;
                int cx = idx % GRID_W;
                ushort newCost = (ushort)(cost[idx] + 1);

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DirDx[d];
                    int ny = cy + DirDy[d];
                    if (nx < 0 || nx >= GRID_W || ny < 0 || ny >= GRID_H) continue;
                    int nIdx = ny * GRID_W + nx;
                    if (_blocked[nIdx]) continue;
                    if (cost[nIdx] <= newCost) continue;
                    cost[nIdx] = newCost;
                    _writeBuf[nIdx] = OppDir[d];
                    _bfsQueue[tail++] = nIdx;
                    if (tail >= CELL_COUNT) tail = 0;
                }
            }
        }

        private void SwapBuffers()
        {
            // Atomic pointer swap (safe – main thread only reads, never writes)
            var tmp = _readBuf;
            _readBuf = _writeBuf;
            _writeBuf = tmp;
        }
    }
}
