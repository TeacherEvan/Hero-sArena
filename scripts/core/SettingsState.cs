using System;

namespace HeroArena
{
    /// <summary>
    /// Godot-free settings + lifetime-stats persistence (<c>user://settings.cfg</c>).
    /// Pure parse/serialize + defaults; the Settings/Dashboard UI nodes do the
    /// engine I/O so this stays headlessly testable (cf. <see cref="MenuDifficultyState"/>).
    /// </summary>
    public static class SettingsState
    {
        public const string ConfigSection = "game";
        public const string ConfigKeyMaster = "master_volume";
        public const string ConfigKeySfx = "sfx_volume";
        public const string ConfigKeyMusic = "music_volume";
        public const string ConfigKeyFullscreen = "fullscreen";
        public const string ConfigKeyVSync = "vsync";
        public const string ConfigKeyDifficulty = "difficulty";

        // Lifetime run stats accumulated across runs.
        public const string ConfigKeyLifetimeKills = "lifetime_kills";
        public const string ConfigKeyLifetimeScore = "lifetime_score";
        public const string ConfigKeyLifetimeWaves = "lifetime_waves";
        public const string ConfigKeyLifetimePlaySeconds = "lifetime_play_seconds";

        public const string DefaultDifficulty = nameof(Difficulty.Normal);
        public const float DefaultVolume = 1f;

        public static Difficulty ParseDifficulty(string? raw) => raw switch
        {
            nameof(Difficulty.Easy) => Difficulty.Easy,
            nameof(Difficulty.Hard) => Difficulty.Hard,
            _ => Difficulty.Normal,
        };

        public static string SerializeDifficulty(Difficulty d) => d switch
        {
            Difficulty.Easy => nameof(Difficulty.Easy),
            Difficulty.Hard => nameof(Difficulty.Hard),
            _ => nameof(Difficulty.Normal),
        };

        public static float ClampVolume(float v) => MathF.Min(MathF.Max(v, 0f), 1f);

        public static float ParseVolume(string? raw, float defaultValue)
        {
            if (float.TryParse(raw, out var v)) return ClampVolume(v);
            return defaultValue;
        }

        public static bool ParseBool(string? raw, bool defaultValue) => raw switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => defaultValue,
        };

        public static string SerializeBool(bool v) => v ? "1" : "0";
        public static string SerializeFloat(float v) => v.ToString("0.00");
        public static string SerializeInt(long v) => v.ToString();
        public static long ParseInt(string? raw, long defaultValue) =>
            long.TryParse(raw, out var v) ? v : defaultValue;
    }
}
