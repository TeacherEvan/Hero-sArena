using System;
using System.Collections.Generic;

namespace HeroArena
{
    /// <summary>
    /// Describes which InputMap actions + joypad device belong to one local
    /// co-op player. Pure data — no engine dependencies — so player bindings
    /// are trivially unit-testable and persistable.
    ///
    /// Godot's <see cref="Godot.InputMap"/> is global (no per-player
    /// scoping), so the standard couch-co-op pattern is to suffix action
    /// names per player (`move_left_p1`, `move_left_p2`, ...) and use the
    /// joypad device index as the player index (device 0 → P1, device 1 → P2).
    /// </summary>
    public sealed class PlayerInput
    {
        public int PlayerIndex { get; }
        public int JoypadDevice { get; }   // -1 = keyboard/mouse only
        public string MoveLeft { get; }
        public string MoveRight { get; }
        public string MoveUp { get; }
        public string MoveDown { get; }
        public string Attack { get; }
        public string Dodge { get; }
        public string Ability { get; }
        public string CycleWeaponPrev { get; }
        public string CycleWeaponNext { get; }

        public PlayerInput(int playerIndex, int joypadDevice, string suffix = "")
        {
            PlayerIndex = playerIndex;
            JoypadDevice = joypadDevice;
            var s = suffix;
            MoveLeft = $"move_left{s}";
            MoveRight = $"move_right{s}";
            MoveUp = $"move_up{s}";
            MoveDown = $"move_down{s}";
            Attack = $"attack{s}";
            Dodge = $"dodge{s}";
            Ability = $"ability{s}";
            CycleWeaponPrev = $"weapon_prev{s}";
            CycleWeaponNext = $"weapon_next{s}";
        }

        /// <summary>Player 1: keyboard/mouse, device -1.</summary>
        public static PlayerInput P1 { get; } = new(0, -1, "_p1");
        /// <summary>Player 2: joypad device 1.</summary>
        public static PlayerInput P2 { get; } = new(1, 1, "_p2");
        /// <summary>Player 3: joypad device 2.</summary>
        public static PlayerInput P3 { get; } = new(2, 2, "_p3");
        /// <summary>Player 4: joypad device 3.</summary>
        public static PlayerInput P4 { get; } = new(3, 3, "_p4");

        /// <summary>All predefined players, in order.</summary>
        public static IReadOnlyList<PlayerInput> All { get; } =
            new List<PlayerInput> { P1, P2, P3, P4 };

        /// <summary>Get by 0-based player index.</summary>
        public static PlayerInput? Get(int playerIndex) =>
            playerIndex >= 0 && playerIndex < All.Count ? All[playerIndex] : null;
    }
}
