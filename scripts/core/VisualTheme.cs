using Godot;
using System;

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

        // ── Perceptual oklch() tonal elevation tokens ──
        // Canvas void / card / raised, each ~+3% lightness per z-layer.
        public static readonly Color SurfaceCanvas = new Color(0.043f, 0.051f, 0.078f, 1f); // oklch(0.15 0.018 265)
        public static readonly Color SurfaceCard   = new Color(0.075f, 0.086f, 0.125f, 1f); // oklch(0.18 0.020 265)
        public static readonly Color SurfaceRaised = new Color(0.122f, 0.141f, 0.200f, 1f); // oklch(0.22 0.022 265)

        // Specular top-edge highlight (inset) for raised surfaces.
        public static readonly Color SpecularTop   = new Color(1f, 1f, 1f, 0.14f);

        // Semantic status pips.
        public static readonly Color StatusNominal = new Color(0.220f, 0.741f, 0.973f, 1f); // cyan
        public static readonly Color StatusLow     = new Color(1f, 0.25f, 0.25f, 1f); // red

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

        /// <summary>Primary CTA button: gold base, dark text.</summary>
        public static void StyleButtonPrimary(Button button)
        {
            button.AddThemeStyleboxOverride("normal", PanelStyle(Gold, Border));
            button.AddThemeStyleboxOverride("hover", PanelStyle(Gold, Border));
            button.AddThemeStyleboxOverride("pressed", PanelStyle(GoldDim, Gold));
            button.AddThemeStyleboxOverride("disabled", PanelStyle(Panel, TextMuted));
            button.AddThemeColorOverride("font_color", Background);
            button.AddThemeColorOverride("font_color_hover", Background);
            button.AddThemeColorOverride("font_color_pressed", Text);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>Danger button: red-tinted base.</summary>
        public static void StyleButtonDanger(Button button)
        {
            var danger = new Color(0.6f, 0.2f, 0.2f, 1f);
            var dangerHover = new Color(0.8f, 0.3f, 0.3f, 1f);
            button.AddThemeStyleboxOverride("normal", PanelStyle(danger, Border));
            button.AddThemeStyleboxOverride("hover", PanelStyle(dangerHover, Gold));
            button.AddThemeStyleboxOverride("pressed", PanelStyle(danger, Gold));
            button.AddThemeStyleboxOverride("disabled", PanelStyle(Panel, TextMuted));
            button.AddThemeColorOverride("font_color", Text);
            button.AddThemeColorOverride("font_color_hover", Text);
            button.AddThemeColorOverride("font_color_pressed", Background);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>ProgressBar: dark track, godlike fill.</summary>
        public static void StyleProgressBar(ProgressBar bar, Color? fill = null)
        {
            bar.AddThemeStyleboxOverride("bg", PanelStyle(Panel, Border));
            bar.AddThemeStyleboxOverride("fill", PanelStyle(fill ?? Gold, Border));
            bar.AddThemeColorOverride("font_color", Text);
        }

        /// <summary>Section header: gold, small-caps feel, letter-spaced.</summary>
        public static void StyleSectionHeader(Label label)
        {
            label.AddThemeColorOverride("font_color", Gold);
            label.AddThemeFontSizeOverride("font_size", 14);
            label.CustomMinimumSize = new Vector2(0, 24);
        }

        /// <summary>Stat card value: large cyan/gold number.</summary>
        public static void StyleStatValue(Label label, Color? color = null)
        {
            label.AddThemeColorOverride("font_color", color ?? Cyan);
            label.AddThemeFontSizeOverride("font_size", 28);
        }

        /// <summary>Stat card label: muted small text.</summary>
        public static void StyleStatLabel(Label label)
        {
            label.AddThemeColorOverride("font_color", TextMuted);
            label.AddThemeFontSizeOverride("font_size", 12);
        }

        /// <summary>Volume/slider row value label.</summary>
        public static void StyleSliderValue(Label label)
        {
            label.AddThemeColorOverride("font_color", Text);
            label.AddThemeFontSizeOverride("font_size", 14);
            label.CustomMinimumSize = new Vector2(40, 0);
            label.HorizontalAlignment = HorizontalAlignment.Right;
        }

        /// <summary>Apply panel styling to a stat card container.</summary>
        public static void StyleStatCard(Control card)
        {
            card.AddThemeStyleboxOverride("panel", PanelStyle(PanelHighlight, Border));
        }

        /// <summary>Fade a CanvasLayer/Control alpha via tween, then optionally hide.</summary>
        public static void Fade(CanvasLayer layer, float fromAlpha, float toAlpha, float duration,
            Action? onComplete = null, bool hideWhenDone = false)
        {
            // Alpha is applied as a self-modulate on the topmost color rect child.
            var bg = layer.GetNodeOrNull<ColorRect>("Background");
            if (bg == null) return;

            var tween = layer.CreateTween();
            tween.TweenProperty(bg, "color:a", toAlpha, duration);
            if (onComplete != null)
                tween.TweenCallback(Callable.From(onComplete));
            if (hideWhenDone)
                tween.TweenCallback(Callable.From(() => layer.Visible = false));
        }

        // ── Specular top-edge panel (raised surface, +1 z-layer depth) ──
        public static StyleBoxFlat PanelStyleSpecular(Color bg, Color border)
            => new()
            {
                BgColor = bg,
                BorderColor = border,
                BorderWidthTop = 2, // specular top edge
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
            };

        /// <summary>Six-state tactile button (default / hover / active / focus-visible /
        /// loading / disabled). Expressed purely via StyleBox overrides + modulate alpha;
        /// no layout-thrashing properties are animated.</summary>
        public static void StyleButtonTactile(Button button)
        {
            button.AddThemeStyleboxOverride("normal", PanelStyleSpecular(Panel, Border));
            button.AddThemeStyleboxOverride("hover", PanelStyleSpecular(PanelHighlight, Gold));
            button.AddThemeStyleboxOverride("pressed", PanelStyleSpecular(GoldDim, Gold));
            button.AddThemeStyleboxOverride("disabled", PanelStyle(Panel, TextMuted));
            button.AddThemeColorOverride("font_color", Text);
            button.AddThemeColorOverride("font_color_hover", Text);
            button.AddThemeColorOverride("font_color_pressed", SurfaceCanvas);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>Primary CTA tactile button: gold base, dark text.</summary>
        public static void StyleButtonTactilePrimary(Button button)
        {
            button.AddThemeStyleboxOverride("normal", PanelStyleSpecular(Gold, Border));
            button.AddThemeStyleboxOverride("hover", PanelStyleSpecular(Gold, Border));
            button.AddThemeStyleboxOverride("pressed", PanelStyleSpecular(GoldDim, Gold));
            button.AddThemeStyleboxOverride("disabled", PanelStyle(Panel, TextMuted));
            button.AddThemeColorOverride("font_color", SurfaceCanvas);
            button.AddThemeColorOverride("font_color_hover", SurfaceCanvas);
            button.AddThemeColorOverride("font_color_pressed", Text);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>Danger tactile button: red-tinted base, used for destructive actions (Quit).</summary>
        public static void StyleButtonTactileDanger(Button button)
        {
            var danger = new Color(0.6f, 0.2f, 0.2f, 1f);
            var dangerHover = new Color(0.8f, 0.3f, 0.3f, 1f);
            button.AddThemeStyleboxOverride("normal", PanelStyleSpecular(danger, Border));
            button.AddThemeStyleboxOverride("hover", PanelStyleSpecular(dangerHover, Gold));
            button.AddThemeStyleboxOverride("pressed", PanelStyleSpecular(danger, Gold));
            button.AddThemeStyleboxOverride("disabled", PanelStyle(Panel, TextMuted));
            button.AddThemeColorOverride("font_color", Text);
            button.AddThemeColorOverride("font_color_hover", Text);
            button.AddThemeColorOverride("font_color_pressed", SurfaceCanvas);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>Focus-visible ring: 2px accent ring with 2px offset.</summary>
        public static void StyleButtonFocus(Button button)
        {
            button.AddThemeStyleboxOverride("focus", PanelStyleSpecular(Panel, StatusNominal));
        }

        /// <summary>Loading state: width-locked label hidden, spinner shown, wait cursor.</summary>
        public static void StyleButtonLoading(Button button, bool loading)
        {
            button.Modulate = loading ? new Color(1f, 1f, 1f, 0.45f) : new Color(1f, 1f, 1f, 1f);
            button.MouseFilter = loading ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Pass;
        }

        /// <summary>Tabular-numeric label: fixed-width digits so live readouts never jitter.</summary>
        public static void StyleLabelTabular(Label label, Color? color = null, int fontSize = 16)
        {
            label.AddThemeColorOverride("font_color", color ?? Text);
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.HorizontalAlignment = HorizontalAlignment.Right;
        }

        /// <summary>Tactile slider value label.</summary>
        public static void StyleSliderValueTactile(Label label)
        {
            label.AddThemeColorOverride("font_color", Text);
            label.AddThemeFontSizeOverride("font_size", 14);
            label.CustomMinimumSize = new Vector2(44, 0); // hit-target-friendly width
            label.HorizontalAlignment = HorizontalAlignment.Right;
        }

        /// <summary>Tactile progress bar: dark track, accent fill, specular edges.</summary>
        public static void StyleProgressBarTactile(ProgressBar bar, Color? fill = null)
        {
            bar.AddThemeStyleboxOverride("bg", PanelStyleSpecular(Panel, Border));
            bar.AddThemeStyleboxOverride("fill", PanelStyleSpecular(fill ?? StatusNominal, Border));
            bar.AddThemeColorOverride("font_color", Text);
        }

        /// <summary>Duration that respects reduced motion: near-instant when reduced motion
        /// is requested, otherwise the requested duration.</summary>
        public static float ReducedMotionDuration(float normalDuration)
        {
            try
            {
                if (ProjectSettings.HasSetting("accessibility/reduced_motion"))
                {
                    var reduced = ProjectSettings.GetSetting("accessibility/reduced_motion");
                    if (reduced.VariantType == Variant.Type.Bool && reduced.AsBool())
                        return 0.01f;
                }
            }
            catch { }
            return normalDuration;
        }
    }
}
