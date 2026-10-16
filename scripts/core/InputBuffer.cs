using Godot;
using System;

namespace HeroArena
{
    /// <summary>
    /// 6-frame (100ms @ 60Hz) input buffer for responsive action registration.
    /// The buffer logic lives in the Godot-free <see cref="InputBufferState"/>
    /// (plan T3.1-rest); this Node keeps only engine wiring and delegates 1:1.
    /// </summary>
    public partial class InputBuffer : Node
    {
        private readonly InputBufferState _state = new();

        public override void _Ready()
        {
            SetPhysicsProcess(true);
        }

        public override void _PhysicsProcess(double delta)
        {
            _state.TickFrame();
            _state.ProcessBuffer();
        }

        /// <summary>Record an action press to the buffer.</summary>
        public void BufferAction(StringName action)
        {
            _state.BufferAction(action);
        }

        /// <summary>Returns true and removes the action if it is currently buffered.</summary>
        public bool ConsumeAction(StringName action)
        {
            return _state.ConsumeAction(action);
        }

        /// <summary>Returns true if the action is currently buffered (without consuming).</summary>
        public bool IsBuffered(StringName action)
        {
            return _state.IsBuffered(action);
        }
    }
}
