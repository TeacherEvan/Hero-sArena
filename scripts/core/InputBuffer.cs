using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// 6-frame (100ms @ 60Hz) input buffer for responsive action registration.
    /// </summary>
    public partial class InputBuffer : Node
    {
        private const int BUFFER_FRAMES = 6;

        private readonly struct BufferedAction
        {
            public readonly StringName Action;
            public readonly int FrameStamp;
            public BufferedAction(StringName action, int frame) { Action = action; FrameStamp = frame; }
        }

        private readonly BufferedAction[] _buffer = new BufferedAction[32];
        private int _bufferCount = 0;
        private int _frameCounter = 0;

        public override void _PhysicsProcess(double delta)
        {
            _frameCounter++;
            ProcessBuffer();
        }

        /// <summary>Record an action press to the buffer.</summary>
        public void BufferAction(StringName action)
        {
            if (_bufferCount >= _buffer.Length) return;
            _buffer[_bufferCount++] = new BufferedAction(action, _frameCounter);
        }

        /// <summary>Returns true and removes the action if it is currently buffered.</summary>
        public bool ConsumeAction(StringName action)
        {
            for (int i = 0; i < _bufferCount; i++)
            {
                if (_buffer[i].Action == action)
                {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Returns true if the action is currently buffered (without consuming).</summary>
        public bool IsBuffered(StringName action)
        {
            for (int i = 0; i < _bufferCount; i++)
                if (_buffer[i].Action == action) return true;
            return false;
        }

        // ── Internal ─────────────────────────────────────────────────────────
        private void ProcessBuffer()
        {
            // Remove expired entries
            for (int i = _bufferCount - 1; i >= 0; i--)
            {
                if (_frameCounter - _buffer[i].FrameStamp >= BUFFER_FRAMES)
                    RemoveAt(i);
            }
        }

        private void RemoveAt(int index)
        {
            _bufferCount--;
            if (index < _bufferCount)
                _buffer[index] = _buffer[_bufferCount];
        }
    }
}
