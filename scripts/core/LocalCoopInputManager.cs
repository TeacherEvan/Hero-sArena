using Godot;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Autoload that maps physical input devices to local co-op players.
    /// Godot's InputMap is global, so each player uses suffixed actions
    /// (move_left_p1, move_left_p2, ...). Joypad device index doubles as the
    /// player index (device 0 → P1, device 1 → P2, ...). Keyboard/mouse is
    /// always P1.
    ///
    /// Thin engine wrapper over the Godot-free <see cref="PlayerInput"/>;
    /// device↔player mapping lives here because it requires the SceneTree.
    /// </summary>
    public partial class LocalCoopInputManager : Node
    {
        public static LocalCoopInputManager Instance { get; private set; } = null!;

        private readonly List<PlayerInput> _players = new();
        private readonly Dictionary<int, int> _deviceToPlayer = new();

        public override void _Ready()
        {
            Instance = this;
            // Keyboard/mouse is always player 1 (P1). Joypad device-to-player
            // mapping can be enabled once the joypad axis binding is confirmed
            // in this engine binding (GetJoypadAxis/GetJoypadCount not present).
            _players.Add(PlayerInput.P1);
        }

        /// <summary>Register a joypad device as a co-op player (P2-P4).
        /// Call from input wiring when gamepad support is confirmed.</summary>
        public bool RegisterJoypadDevice(int device)
        {
            if (_deviceToPlayer.ContainsKey(device)) return false;
            int playerIdx = _players.Count; // next free player slot
            if (playerIdx >= PlayerInput.All.Count) return false;
            _deviceToPlayer[device] = playerIdx;
            _players.Add(PlayerInput.All[playerIdx]);
            GD.Print($"LocalCoop: joypad device {device} -> player {playerIdx + 1}");
            return true;
        }

        /// <summary>True when any local player exists (P1 always present).</summary>
        public bool HasPlayers => _players.Count > 0;

        /// <summary>Connected local player count (1–4).</summary>
        public int PlayerCount => _players.Count;

        /// <summary>The PlayerInput for a 0-based player index.</summary>
        public PlayerInput? GetPlayer(int index) =>
            index >= 0 && index < _players.Count ? _players[index] : null;

        /// <summary>Resolve which player (if any) originated a joypad event.</summary>
        public int GetPlayerForDevice(int device) =>
            _deviceToPlayer.TryGetValue(device, out var p) ? p : -1;

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null!;
            base._ExitTree();
        }
    }
}
