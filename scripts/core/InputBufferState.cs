using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free frame input buffer extracted from the InputBuffer Node
    /// (the last #[Trait("Category","GodotRuntime")] suite, plan T3.1-rest):
    /// 6-frame (~100ms @ 60Hz) action buffer with overwrite-free ring
    /// semantics — entries expire by frame stamp, the 33rd insert while full
    /// is silently dropped, and a consumed action frees a slot immediately.
    /// Keys are plain strings; the Node converts Godot StringName at the
    /// boundary (its reflection-built tests pinned StringName equality,
    /// including a null/null branch — preserved here exactly).
    /// The Node keeps only the engine wiring: _PhysicsProcess ticking drives
    /// TickFrame + ProcessBuffer, exactly in that order, as before.
    /// </summary>
    public class InputBufferState
    {
        /// <summary>Buffer lifetime: 6 frames ≈ 100ms at 60Hz physics.</summary>
        public const int BufferFrames = 6;

        /// <summary>Fixed-capacity storage: BufferCapacity entries, no growth.</summary>
        public const int BufferCapacity = 32;

        private readonly struct BufferedAction
        {
            public readonly string Action;
            public readonly int FrameStamp;
            public BufferedAction(string action, int frameStamp) { Action = action; FrameStamp = frameStamp; }
        }

        private readonly BufferedAction[] _buffer = new BufferedAction[BufferCapacity];
        private int _bufferCount = 0;
        private int _frameCounter = 0;

        /// <summary>Entries currently buffered (for UI/tests only).</summary>
        public int BufferedCount => _bufferCount;

        /// <summary>Advances the frame clock (once per physics tick).</summary>
        public void TickFrame() => _frameCounter++;

        /// <summary>Record an action press. Dropped silently when full.</summary>
        public void BufferAction(string action)
        {
            if (_bufferCount >= _buffer.Length) return;
            _buffer[_bufferCount++] = new BufferedAction(action, _frameCounter);
        }

        /// <summary>Returns true and removes the action if it is currently buffered.</summary>
        public bool ConsumeAction(string action)
        {
            for (int i = 0; i < _bufferCount; i++)
            {
                if (Matches(_buffer[i].Action, action))
                {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Returns true if the action is currently buffered (without consuming).</summary>
        public bool IsBuffered(string action)
        {
            for (int i = 0; i < _bufferCount; i++)
                if (Matches(_buffer[i].Action, action)) return true;
            return false;
        }

        /// <summary>
        /// Removes expired entries (frame stamp older than BufferFrames).
        /// The Node calls this after TickFrame each physics tick.
        /// </summary>
        public void ProcessBuffer()
        {
            for (int i = _bufferCount - 1; i >= 0; i--)
            {
                if (_frameCounter - _buffer[i].FrameStamp >= BufferFrames)
                    RemoveAt(i);
            }
        }

        private void RemoveAt(int index)
        {
            _bufferCount--;
            if (index < _bufferCount)
                _buffer[index] = _buffer[_bufferCount];
        }

        /// <summary>
        /// Preserved from the Node: StringName equality modulo null — a null
        /// stored entry and a null key are considered equal (the pre-extraction
        /// tests pinned this; nothing in game produces a null key, but the
        /// branch is behavior, not accident).
        /// </summary>
        private static bool Matches(string stored, string key)
            => stored == key || (stored == null && key == null);
    }
}
