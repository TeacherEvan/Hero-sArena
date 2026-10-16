using Godot;

namespace HeroArena
{
    /// <summary>
    /// Dark, godlike visual theme.
    ///
    /// One cohesive palette plus applicators for Button, Label, ProgressBar and
    /// generic Control panels. Styling is applied in code at runtime so every
    /// UI panel shares the same aesthetic without hand-authoring Theme
    /// resources (which the headless gate here cannot validate).
    ///
    /// Palette:
    ///   Background     - deep void        (#0b0d14)
    ///   Panel          - UI panels        (#131620)
    ///   PanelHighlight - raised panels    (#1f2433)
    ///   Gold           - godlike gold     (#e3c76a)
    ///   Cyan           - celestial accent (#38bdf8)
    ///   Text / TextMuted / Border
    /// </summary>
    public static class VisualTheme
    {
        public static readonly Color Background    = new Color(0.043f, 0.051f, 0.078f, 1f);
        public static readonly Color Panel         = new Color(0.075f, 0.086f, 0.125f, 1f);
        public static readonly Color PanelHighlight= new Color(0.122f, 0.141f, 0.200f, 1f);
        public static readonly Color Gold          = new Color(0.890f, 0.780f, 0.416f, 1f);
        public static readonly Color GoldDim       = new Color(0.478f, 0.392f, 0.227f, 1f);
        public static readonly Color Cyan          = new Color(0.220f, 0.741f, 0.973f, 1f);
        public static readonly Color Text          = new Color(0.910f, 0.918f, 0.949f, 1f);
        public static readonly Color TextMuted     = new Color(0.545f, 0.576f, 0.659f, 1f);
        public static readonly Color Border        = new Color(0.165f, 0.188f, 0.251f, 1f);
        public static readonly Color LowHealth     = new Color(1f, 0.25f, 0.25f, 1f);

        public static StyleBoxFlat PanelStyle(Color bg, Color border)
            => new()
            {
                BgColor = bg,
                BorderColor = border,
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
            };

        /// <summary>Dark panel look with a thin border and near-white text.</summary>
        public static void StylePanel(Control control, Color? bg = null, Color? border = null)
        {
            control.AddThemeStyleboxOverride("panel", PanelStyle(bg ?? Panel, border ?? Border));
            control.AddThemeColorOverride("font_color", Text);
        }

        /// <summary>Raised godlike panel (slightly lighter, celestial border).</summary>
        public static void StylePanelRaised(Control control)
            => StylePanel(control, PanelHighlight, Gold);

        /// <summary>Label: godlike text color.</summary>
        public static void StyleLabel(Label label, Color? color = null)
            => label.AddThemeColorOverride("font_color", color ?? Gold);

        /// <summary>Muted secondary text.</summary>
        public static void StyleLabelMuted(Label label)
            => label.AddThemeColorOverride("font_color", TextMuted);

        /// <summary>Button: dark base, gold text, celestial hover.</summary>
        public static void StyleButton(Button button)
        {
            button.AddThemeStyleboxOverride("normal", PanelStyle(Panel, Border));
            button.AddThemeStyleboxOverride("hover", PanelStyle(PanelHighlight, Gold));
            button.AddThemeStyleboxOverride("pressed", PanelStyle(GoldDim, Gold));
            button.AddThemeStyleboxOverride("disabled", PanelStyle(Panel, TextMuted));
            button.AddThemeColorOverride("font_color", Gold);
            button.AddThemeColorOverride("font_color_hover", Text);
            button.AddThemeColorOverride("font_color_pressed", Text);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>ProgressBar: dark track, godlike fill.</summary>
        public static void StyleProgressBar(ProgressBar bar, Color? fill = null)
        {
            bar.AddThemeStyleboxOverride("bg", PanelStyle(Panel, Border));
            bar.AddThemeStyleboxOverride("fill", PanelStyle(fill ?? Gold, Border));
            bar.AddThemeColorOverride("font_color", Text);
        }
    }
}
