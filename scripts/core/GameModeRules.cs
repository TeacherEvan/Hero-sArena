using System;

namespace HeroArena
{
    /// <summary>
    /// Crimsonland's five survival modes, each with distinct win/lose rules.
    /// Hero's Arena previously only had endless Survival; this adds Rush,
    /// Blitz, Weapon Picker, and Nukefism so the game can compete on
    /// mode variety in a tournament.
    ///
    /// Pure data + mode rules — headlessly testable.
    /// </summary>
    public enum GameMode
    {
        Survival,   // Survive as long as possible; endless waves.
        Rush,       // Survive a fixed time limit; waves keep coming.
        Blitz,      // Kill a target number of enemies as fast as possible.
        WeaponPicker, // Start with a random weapon; no weapon pickups drop.
        Nukefism,   // No guns — only bombs/power-ups on the ground can kill.
    }

    /// <summary>
    /// Mode-specific win/loss rules evaluated each wave/frame by GameManager.
    /// Pure — no engine dependencies.
    /// </summary>
    public sealed class GameModeRules
    {
        public GameMode Mode { get; }
        public int? RushTimeLimitSeconds { get; }   // Rush: survive this long
        public int? BlitzKillTarget { get; }        // Blitz: kill this many
        public bool WeaponPickupsDisabled { get; }  // Weapon Picker / Nukefism
        public bool GunsDisabled { get; }           // Nukefism: no weapons at all

        private GameModeRules(GameMode mode,
            int? rushTimeLimitSeconds = null,
            int? blitzKillTarget = null,
            bool weaponPickupsDisabled = false,
            bool gunsDisabled = false)
        {
            Mode = mode;
            RushTimeLimitSeconds = rushTimeLimitSeconds;
            BlitzKillTarget = blitzKillTarget;
            WeaponPickupsDisabled = weaponPickupsDisabled;
            GunsDisabled = gunsDisabled;
        }

        public static GameModeRules SurvivalRules() =>
            new(GameMode.Survival);

        public static GameModeRules RushRules(int timeLimitSeconds) =>
            new(GameMode.Rush, rushTimeLimitSeconds: timeLimitSeconds);

        public static GameModeRules BlitzRules(int killTarget) =>
            new(GameMode.Blitz, blitzKillTarget: killTarget);

        public static GameModeRules WeaponPickerRules() =>
            new(GameMode.WeaponPicker, weaponPickupsDisabled: true);

        public static GameModeRules NukefismRules() =>
            new(GameMode.Nukefism, weaponPickupsDisabled: true, gunsDisabled: true);

        /// <summary>Human-readable mode name.</summary>
        public string DisplayName => Mode switch
        {
            GameMode.Survival => "Survival",
            GameMode.Rush => "Rush",
            GameMode.Blitz => "Blitz",
            GameMode.WeaponPicker => "Weapon Picker",
            GameMode.Nukefism => "Nukefism",
            _ => Mode.ToString(),
        };

        /// <summary>One-line mode description for the menu.</summary>
        public string Description => Mode switch
        {
            GameMode.Survival => "Endless waves. Survive as long as you can.",
            GameMode.Rush => "Survive the time limit. Waves never stop.",
            GameMode.Blitz => "Reach the kill target as fast as possible.",
            GameMode.WeaponPicker => "Start with a random weapon. No pickups drop.",
            GameMode.Nukefism => "No guns. Only bombs and power-ups can kill.",
            _ => "",
        };
    }

    /// <summary>
    /// Result of evaluating the current mode's completion condition.
    /// </summary>
    public readonly struct GameModeResult
    {
        public bool IsComplete { get; init; }
        public bool IsGameOver { get; init; }
        public string? Message { get; init; }
    }
}
