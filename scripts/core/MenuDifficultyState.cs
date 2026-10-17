namespace HeroArena
{
    /// <summary>
    /// Pure difficulty-menu logic (R2). MainMenu delegates 1:1: it parses /
    /// serializes the persisted selection and decides button highlight
    /// states. Godot-free so xUnit covers it headlessly.
    /// </summary>
    public static class MenuDifficultyState
    {
        /// <summary>ConfigFile section/key the menu persists the choice under.</summary>
        public const string ConfigSection = "game";
        public const string ConfigKey = "difficulty";

        /// <summary>Maps a persisted raw value to a Difficulty; missing/invalid → Normal.</summary>
        public static Difficulty Parse(string? raw) => raw switch
        {
            nameof(Difficulty.Easy) => Difficulty.Easy,
            nameof(Difficulty.Hard) => Difficulty.Hard,
            _ => Difficulty.Normal,
        };

        /// <summary>Serializes a Difficulty for persistence.</summary>
        public static string Serialize(Difficulty d) => d switch
        {
            Difficulty.Easy => nameof(Difficulty.Easy),
            Difficulty.Hard => nameof(Difficulty.Hard),
            _ => nameof(Difficulty.Normal),
        };

        /// <summary>Whether the menu button for <paramref name="button"/> should show pressed.</summary>
        public static bool IsSelected(Difficulty current, Difficulty button) => current == button;
    }
}
