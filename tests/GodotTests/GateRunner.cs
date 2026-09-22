using Godot;

namespace HeroArena.Tests
{
    /// <summary>
    /// SceneTree entry point for the Godot headless gate.
    /// Godot 4 requires scripts run via `-s` to inherit SceneTree/MainLoop,
    /// so CoreSystemTests (a Node) cannot be the entry point directly.
    /// This runner provides the minimal live tree the gate needs (notably
    /// the EventBus singleton for the HitFlash test), then hosts the
    /// CoreSystemTests node, which runs assertions in _Ready and quits
    /// non-zero on failure.
    /// Run with: godot --headless -s res://tests/GodotTests/GateRunner.cs
    /// (requires a .NET/mono-enabled Godot 4.3 build).
    /// </summary>
    public partial class GateRunner : SceneTree
    {
        public override void _Initialize()
        {
            var bus = new EventBus();
            Root.AddChild(bus);
            var tests = new CoreSystemTests();
            Root.AddChild(tests);
        }
    }
}
