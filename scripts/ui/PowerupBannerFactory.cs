using Godot;

namespace HeroArena
{
    /// <summary>
    /// Creates a transient banner (Label) with an auto-cleanup timer. The
    /// timer is attached to <paramref name="host"/> and the banner is added
    /// to <paramref name="list"/>. After <paramref name="lifetime"/> seconds
    /// the timer fires and removes the banner.
    ///
    /// Invariant: the timer's Timeout callback must free BOTH the banner and
    /// the timer on every return path, including the early-return when the
    /// banner has already been evicted by a cap-eviction. Regression for the
    /// HUD ShowPowerup leak (see fix/hud-timer-leak-regression).
    /// </summary>
    public static class PowerupBannerFactory
    {
        public static void Spawn(Node host, Container list, string text, float lifetime = 5f)
        {
            if (host == null || list == null) return;

            var lbl = new Label { Text = text };
            list.AddChild(lbl);
            var timer = new Timer { WaitTime = lifetime, OneShot = true };
            timer.Timeout += () =>
            {
                // BUG-GUARD: every return path here must QueueFree() the timer.
                // If the banner was already evicted by a cap-eviction in the
                // caller's ShowPowerup, lbl is invalid and we still need to
                // free the timer (otherwise the timer leaks).
                if (!Godot.GodotObject.IsInstanceValid(lbl))
                {
                    timer.QueueFree();
                    return;
                }
                if (lbl.IsInsideTree()) lbl.GetParent()?.RemoveChild(lbl);
                lbl.QueueFree();
                timer.QueueFree();
            };
            host.AddChild(timer);
            timer.Start();
        }
    }
}
