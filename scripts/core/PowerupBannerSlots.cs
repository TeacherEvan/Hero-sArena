namespace HeroArena
{
    /// <summary>
    /// Powerup banner slot selection, extracted from HUD.ShowPowerup so the
    /// selection rule is unit-testable headlessly. A new banner reuses the
    /// first expired slot (timer &lt;= 0); when every slot is active it
    /// recycles the oldest one (smallest remaining timer).
    /// </summary>
    public static class PowerupBannerSlots
    {
        public const int MaxBanners = 5;
        public const float Lifetime = 5f;

        /// <summary>
        /// Index of the banner slot to reuse for a new powerup, given the
        /// remaining times of all slots. Always returns a valid index for a
        /// non-empty <paramref name="timers"/> array.
        /// </summary>
        public static int SelectSlot(float[] timers)
        {
            int targetIdx = 0;
            float minTime = float.MaxValue;

            for (int i = 0; i < timers.Length; i++)
            {
                if (timers[i] <= 0f)
                {
                    return i;
                }
                if (timers[i] < minTime)
                {
                    minTime = timers[i];
                    targetIdx = i;
                }
            }

            return targetIdx;
        }
    }
}
