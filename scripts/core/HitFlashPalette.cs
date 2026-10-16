namespace HeroArena
{
    /// <summary>
    /// Pure damage-type color mapping and flash fade curve for HitFlash.
    /// Extracted from the HitFlash CanvasLayer (the F-31 consumer) so the
    /// xUnit suite can assert all 6 DamageType branches and the fade math
    /// headlessly; HitFlash delegates here and adds only the overlay wiring.
    /// Godot-free (returns an RGB triple, not a Color) to match the
    /// ProgressionFormulas precedent; the Node wrapper converts to Color.
    ///
    /// kinetic = red, energy = cyan, lightning = yellow,
    /// acid = green, fire = orange, explosive = white.
    /// Unmapped types fall back to pure red.
    /// </summary>
    public static class HitFlashPalette
    {
        public const float Duration = 0.08f;
        public const float MaxAlpha = 0.25f;

        public static (float R, float G, float B) ColorFor(DamageType type) => type switch
        {
            DamageType.Kinetic => (1f, 0.2f, 0.2f),
            DamageType.Energy => (0.3f, 0.8f, 1f),
            DamageType.Lightning => (1f, 1f, 0.3f),
            DamageType.Acid => (0.4f, 1f, 0.4f),
            DamageType.Fire => (1f, 0.5f, 0.1f),
            DamageType.Explosive => (1f, 1f, 1f),
            _ => (1f, 0f, 0f),
        };

        /// <summary>
        /// Overlay alpha for the fraction of flash time
        /// <paramref name="remaining"/> left (Duration → 0). Returns 0 once
        /// the flash has elapsed, which is the signal to stop processing.
        /// </summary>
        public static float AlphaFor(float remaining)
        {
            if (remaining <= 0f) return 0f;
            return MaxAlpha * (remaining / Duration);
        }
    }
}
