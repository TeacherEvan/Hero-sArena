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
    ///
    /// Surfaces are built from a single recipe so every control shares the
    /// same geometry: 8px radius, a 1px rim, a 2px specular top edge and a
    /// short drop shadow. Only colours change per role - never geometry - so
    /// the UI never "jumps" between states.
    ///
    /// Godot constraint notes (verified against Godot 4.3):
    ///   * <see cref="StyleBoxFlat"/> has a single BorderColor, so the
    ///     specular top edge is expressed as a 2px top border in the rim
    ///     colour rather than an inset white line.
    ///   * ProgressBar's track stylebox is themed as "background", not "bg".
    ///   * The "focus" stylebox is drawn *over* the state stylebox, so it
    ///     must have DrawCenter = false or it hides the button fill.
    ///   * Containers (VBox/HBox) do not draw the "panel" stylebox; only
    ///     Panel/PanelContainer do. Elevated surfaces behind a container are
    ///     therefore attached as a sibling via <see cref="AttachSurface"/>, or
    ///     expressed as per-row chips via <see cref="StyleChip"/>.
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

        // Rim colours: the 1px light edge that lifts a surface off the void.
        public static readonly Color RimSoft       = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color RimBright     = new Color(1f, 1f, 1f, 0.22f);
        public static readonly Color RimPressed    = new Color(1f, 1f, 1f, 0.06f);

        // Drop shadow used by every raised surface (short, hard, dark).
        public static readonly Color DropShadow    = new Color(0f, 0f, 0f, 0.40f);

        // Semantic status pips.
        public static readonly Color StatusNominal = new Color(0.220f, 0.741f, 0.973f, 1f); // cyan
        public static readonly Color StatusLow     = new Color(1f, 0.25f, 0.25f, 1f); // red

        // Danger (destructive action) ramp.
        public static readonly Color DangerBase    = new Color(0.60f, 0.20f, 0.20f, 1f);
        public static readonly Color DangerHover   = new Color(0.80f, 0.30f, 0.30f, 1f);
        public static readonly Color DangerPressed = new Color(0.42f, 0.14f, 0.14f, 1f);

        // Gold ramp for the primary CTA.
        public static readonly Color GoldBright    = new Color(0.941f, 0.851f, 0.541f, 1f);
        public static readonly Color GoldPressed   = new Color(0.780f, 0.667f, 0.353f, 1f);

        /// <summary>Corner radius shared by every raised surface (buttons, cards).</summary>
        public const int CornerRadius = 8;

        /// <summary>Radius used by small chips (stat rows, value readouts).</summary>
        public const int ChipRadius = 6;

        // ── Stylebox recipes ────────────────────────────────────────────────

        /// <summary>
        /// Legacy flat panel (no radius) - kept for surfaces that must stay
        /// pixel-sharp (progress tracks, dividers).
        /// </summary>
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

        /// <summary>
        /// Elevated surface: 8px radius, 1px rim, 2px specular top edge and a
        /// short drop shadow. This is the shared geometry for every control.
        /// </summary>
        public static StyleBoxFlat PanelStyleSpecular(Color bg, Color border)
        {
            var box = new StyleBoxFlat
            {
                BgColor = bg,
                BorderColor = border,
                BorderWidthTop = 2, // specular top edge
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusTopLeft = CornerRadius,
                CornerRadiusTopRight = CornerRadius,
                CornerRadiusBottomLeft = CornerRadius,
                CornerRadiusBottomRight = CornerRadius,
                ShadowColor = DropShadow,
                ShadowSize = 3,
                ShadowOffset = new Vector2(0, 2),
                AntiAliasing = true,
            };
            return box;
        }

        /// <summary>Card surface: like <see cref="PanelStyleSpecular"/> but with a
        /// softer rim (cards are larger, so a hard rim reads as noise).</summary>
        public static StyleBoxFlat SurfaceBox(Color bg, Color border)
        {
            var box = PanelStyleSpecular(bg, border);
            box.BorderWidthTop = 1;
            box.ShadowSize = 6;
            box.ShadowOffset = new Vector2(0, 4);
            return box;
        }

        /// <summary>Focus ring: border only (DrawCenter = false) so it never hides
        /// the button fill underneath, expanded 2px for the classic offset.</summary>
        public static StyleBoxFlat FocusRing()
        {
            var ring = new StyleBoxFlat
            {
                BgColor = Colors.Transparent,
                BorderColor = StatusNominal,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusTopLeft = CornerRadius + 2,
                CornerRadiusTopRight = CornerRadius + 2,
                CornerRadiusBottomLeft = CornerRadius + 2,
                CornerRadiusBottomRight = CornerRadius + 2,
                DrawCenter = false,
                AntiAliasing = true,
            };
            ring.SetExpandMargin(Side.Left, 2f);
            ring.SetExpandMargin(Side.Right, 2f);
            ring.SetExpandMargin(Side.Top, 2f);
            ring.SetExpandMargin(Side.Bottom, 2f);
            return ring;
        }

        /// <summary>Progress track: flat, 3px radius, hairline border.</summary>
        private static StyleBoxFlat TrackBox(Color bg, Color border)
            => new()
            {
                BgColor = bg,
                BorderColor = border,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                AntiAliasing = true,
            };

        // ── Control applicators ─────────────────────────────────────────────

        /// <summary>Dark panel look with a thin border and near-white text.</summary>
        public static void StylePanel(Control control, Color? bg = null, Color? border = null)
        {
            control.AddThemeStyleboxOverride("panel", PanelStyle(bg ?? Panel, border ?? Border));
            control.AddThemeColorOverride("font_color", Text);
        }

        /// <summary>
        /// Make a centred column hug its content vertically.
        ///
        /// The scenes size their columns to a fixed box (e.g. 680x600), which
        /// leaves the card that sits behind them half empty. Clearing the
        /// vertical offsets lets the container fall back to its combined
        /// minimum size while GrowVertical = BOTH keeps it centred on its
        /// anchor.
        /// </summary>
        public static void FitColumnHeight(Control column)
        {
            column.OffsetTop = 0f;
            column.OffsetBottom = 0f;
            column.GrowVertical = Control.GrowDirection.Both;
        }

        /// <summary>
        /// Attach an elevated surface *behind* a control.
        ///
        /// Containers (VBox/HBox) never draw the "panel" stylebox, so a menu
        /// column has no visible surface. This adds a sibling Panel that
        /// mirrors the target's anchors/offsets, expanded by
        /// <paramref name="padding"/>, and pushes it behind the content.
        ///
        /// The parent must not be a Container (a Container would lay the
        /// panel out as a sibling item). For cards inside a container use
        /// <see cref="StyleChip"/> instead.
        /// </summary>
        public static Panel? AttachSurface(Control target, int padding = 18,
            Color? bg = null, Color? border = null)
        {
            var parent = target.GetParent();
            if (parent is Container) return null; // container would manage it
            if (parent is not Node) return null;

            var existing = parent.GetNodeOrNull<Panel>(target.Name + "Surface");
            if (existing != null) return existing;

            var panel = new Panel
            {
                Name = target.Name + "Surface",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            panel.AddThemeStyleboxOverride("panel", SurfaceBox(bg ?? SurfaceCard, border ?? Border));
            parent.AddChild(panel);

            // The surface mirrors the target's live rect, so it follows the
            // target when the layout resizes it (containers clamp their size
            // to their content, which the offsets alone cannot express).
            target.Resized += () => SyncSurface(panel, target, padding);
            SyncSurface(panel, target, padding);

            // Draw behind the content it decorates, but in front of any
            // backdrop sibling (a full-screen Background ColorRect would
            // otherwise swallow the surface).
            int targetIndex = target.GetIndex();
            if (targetIndex > 0) parent.MoveChild(panel, targetIndex);
            else parent.MoveChild(panel, 0);
            return panel;
        }

        /// <summary>Copy the target's live rect onto a surface, plus padding.</summary>
        private static void SyncSurface(Panel surface, Control target, int padding)
        {
            if (surface == null || target == null) return;
            surface.AnchorLeft = target.AnchorLeft;
            surface.AnchorTop = target.AnchorTop;
            surface.AnchorRight = target.AnchorRight;
            surface.AnchorBottom = target.AnchorBottom;
            surface.GrowHorizontal = target.GrowHorizontal;
            surface.GrowVertical = target.GrowVertical;
            surface.Position = target.Position - new Vector2(padding, padding);
            surface.Size = target.Size + new Vector2(padding * 2, padding * 2);
        }

        /// <summary>
        /// Row chip: a rounded, translucent plate behind a label. Used for
        /// stat rows that live inside a Container (which cannot host a
        /// panel surface of its own).
        /// </summary>
        public static void StyleChip(Label label, Color? tint = null)
        {
            var bg = tint ?? new Color(1f, 1f, 1f, 0.04f);
            var chip = new StyleBoxFlat
            {
                BgColor = bg,
                BorderColor = new Color(1f, 1f, 1f, 0.05f),
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusTopLeft = ChipRadius,
                CornerRadiusTopRight = ChipRadius,
                CornerRadiusBottomLeft = ChipRadius,
                CornerRadiusBottomRight = ChipRadius,
                ContentMarginLeft = 10f,
                ContentMarginRight = 10f,
                ContentMarginTop = 3f,
                ContentMarginBottom = 3f,
                AntiAliasing = true,
            };
            label.AddThemeStyleboxOverride("normal", chip);
        }

        /// <summary>Label: godlike text color.</summary>
        public static void StyleLabel(Label label, Color? color = null)
            => label.AddThemeColorOverride("font_color", color ?? Gold);

        /// <summary>Muted secondary text.</summary>
        public static void StyleLabelMuted(Label label)
            => label.AddThemeColorOverride("font_color", TextMuted);

        /// <summary>Screen title: large gold display type with a soft shadow.</summary>
        public static void StyleTitle(Label label, Color? color = null, int fontSize = 40)
        {
            label.AddThemeColorOverride("font_color", color ?? Gold);
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.55f));
            label.AddThemeConstantOverride("shadow_offset_x", 0);
            label.AddThemeConstantOverride("shadow_offset_y", 2);
            label.AddThemeConstantOverride("shadow_outline_size", 0);
        }

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
            button.AddThemeStyleboxOverride("normal", PanelStyle(DangerBase, Border));
            button.AddThemeStyleboxOverride("hover", PanelStyle(DangerHover, Gold));
            button.AddThemeStyleboxOverride("pressed", PanelStyle(DangerPressed, Gold));
            button.AddThemeStyleboxOverride("disabled", PanelStyle(Panel, TextMuted));
            button.AddThemeColorOverride("font_color", Text);
            button.AddThemeColorOverride("font_color_hover", Text);
            button.AddThemeColorOverride("font_color_pressed", Background);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>ProgressBar: dark track, godlike fill.</summary>
        public static void StyleProgressBar(ProgressBar bar, Color? fill = null)
        {
            bar.AddThemeStyleboxOverride("background", TrackBox(Panel, Border));
            bar.AddThemeStyleboxOverride("fill", TrackBox(fill ?? Gold, Border));
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

        /// <summary>
        /// Stat row: a "Name: value" line on a chip plate.
        ///
        /// The value stays in the same label as the name, so the row is
        /// self-describing instead of a column of bare numbers.
        /// </summary>
        public static void StyleStatRow(Label label, bool prominent = true, Color? color = null)
        {
            label.AddThemeColorOverride("font_color", color ?? (prominent ? Text : TextMuted));
            label.AddThemeFontSizeOverride("font_size", prominent ? 20 : 14);
            StyleChip(label, prominent ? new Color(1f, 1f, 1f, 0.05f) : new Color(1f, 1f, 1f, 0.03f));
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
            card.AddThemeStyleboxOverride("panel", SurfaceBox(SurfaceRaised, Border));
        }

        /// <summary>Fade a CanvasLayer/Control alpha via tween, then optionally hide.</summary>
        public static void Fade(CanvasLayer layer, float fromAlpha, float toAlpha, float duration,
            Action? onComplete = null, bool hideWhenDone = false)
        {
            // Alpha is applied as a self-modulate on the topmost color rect child.
            var bg = layer.GetNodeOrNull<ColorRect>("Background");
            if (bg == null) return;

            var tween = layer.CreateTween();
            tween.TweenProperty(bg, "color:a", toAlpha, ReducedMotionDuration(duration));
            if (onComplete != null)
                tween.TweenCallback(Callable.From(onComplete));
            if (hideWhenDone)
                tween.TweenCallback(Callable.From(() => layer.Visible = false));
        }

        // ── Tactile (six-state) control specification ───────────────────────

        /// <summary>
        /// Six-state tactile button (default / hover / active / focus-visible /
        /// loading / disabled).
        ///
        /// States are expressed purely through StyleBox overrides plus a
        /// modulate alpha for the loading state - no layout-thrashing
        /// property (width/height/margin/padding) is ever animated, and no
        /// per-frame allocation happens.
        /// </summary>
        public static void StyleButtonTactile(Button button)
        {
            button.AddThemeStyleboxOverride("normal", PanelStyleSpecular(SurfaceCard, RimSoft));
            button.AddThemeStyleboxOverride("hover", PanelStyleSpecular(SurfaceRaised, RimBright));
            button.AddThemeStyleboxOverride("pressed", PressedBox(SurfaceCard, RimPressed));
            button.AddThemeStyleboxOverride("hover_pressed", PressedBox(SurfaceRaised, RimPressed));
            button.AddThemeStyleboxOverride("disabled", DisabledBox());
            button.AddThemeStyleboxOverride("focus", FocusRing());
            button.AddThemeColorOverride("font_color", Text);
            button.AddThemeColorOverride("font_color_hover", Text);
            button.AddThemeColorOverride("font_color_pressed", Text);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>Primary CTA tactile button: gold base, dark text.</summary>
        public static void StyleButtonTactilePrimary(Button button)
        {
            button.AddThemeStyleboxOverride("normal", PanelStyleSpecular(Gold, new Color(1f, 1f, 1f, 0.30f)));
            button.AddThemeStyleboxOverride("hover", PanelStyleSpecular(GoldBright, new Color(1f, 1f, 1f, 0.45f)));
            button.AddThemeStyleboxOverride("pressed", PressedBox(GoldPressed, new Color(1f, 1f, 1f, 0.18f)));
            button.AddThemeStyleboxOverride("hover_pressed", PressedBox(GoldPressed, new Color(1f, 1f, 1f, 0.18f)));
            button.AddThemeStyleboxOverride("disabled", DisabledBox());
            button.AddThemeStyleboxOverride("focus", FocusRing());
            button.AddThemeColorOverride("font_color", SurfaceCanvas);
            button.AddThemeColorOverride("font_color_hover", SurfaceCanvas);
            button.AddThemeColorOverride("font_color_pressed", SurfaceCanvas);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>Danger tactile button: red-tinted base, used for destructive actions (Quit).</summary>
        public static void StyleButtonTactileDanger(Button button)
        {
            button.AddThemeStyleboxOverride("normal", PanelStyleSpecular(DangerBase, RimSoft));
            button.AddThemeStyleboxOverride("hover", PanelStyleSpecular(DangerHover, RimBright));
            button.AddThemeStyleboxOverride("pressed", PressedBox(DangerPressed, RimPressed));
            button.AddThemeStyleboxOverride("hover_pressed", PressedBox(DangerPressed, RimPressed));
            button.AddThemeStyleboxOverride("disabled", DisabledBox());
            button.AddThemeStyleboxOverride("focus", FocusRing());
            button.AddThemeColorOverride("font_color", Text);
            button.AddThemeColorOverride("font_color_hover", Text);
            button.AddThemeColorOverride("font_color_pressed", Text);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        /// <summary>
        /// Segmented-control variant (difficulty pickers): compact, and the
        /// selected segment lights up gold so the current choice is obvious
        /// without relying on colour alone.
        /// </summary>
        public static void StyleButtonTactileSegment(Button button)
        {
            button.AddThemeStyleboxOverride("normal", SegmentBox(SurfaceCard, RimSoft));
            button.AddThemeStyleboxOverride("hover", SegmentBox(SurfaceRaised, RimBright));
            button.AddThemeStyleboxOverride("pressed", SegmentBox(Gold, new Color(1f, 1f, 1f, 0.30f)));
            button.AddThemeStyleboxOverride("hover_pressed", SegmentBox(GoldBright, new Color(1f, 1f, 1f, 0.45f)));
            button.AddThemeStyleboxOverride("disabled", DisabledBox());
            button.AddThemeStyleboxOverride("focus", FocusRing());
            button.AddThemeColorOverride("font_color", Text);
            button.AddThemeColorOverride("font_color_hover", Text);
            button.AddThemeColorOverride("font_color_pressed", SurfaceCanvas);
            button.AddThemeColorOverride("font_color_disabled", TextMuted);
        }

        private static StyleBoxFlat SegmentBox(Color bg, Color border)
        {
            var box = PanelStyleSpecular(bg, border);
            box.CornerRadiusTopLeft = 5;
            box.CornerRadiusTopRight = 5;
            box.CornerRadiusBottomLeft = 5;
            box.CornerRadiusBottomRight = 5;
            box.ShadowSize = 2;
            return box;
        }

        /// <summary>
        /// Pressed geometry: the specular top edge collapses to 1px and the
        /// drop shadow is dropped, so the control reads as pushed into the
        /// surface (a "depression" instead of a colour swap).
        /// </summary>
        private static StyleBoxFlat PressedBox(Color bg, Color border)
        {
            var box = PanelStyleSpecular(bg, border);
            box.BorderWidthTop = 1;
            box.ShadowSize = 0;
            box.ShadowOffset = Vector2.Zero;
            return box;
        }

        private static StyleBoxFlat DisabledBox()
        {
            var box = PanelStyleSpecular(new Color(SurfaceCard, 0.55f), new Color(1f, 1f, 1f, 0.05f));
            box.ShadowSize = 0;
            box.ShadowOffset = Vector2.Zero;
            return box;
        }

        /// <summary>
        /// Focus-visible ring: 2px accent ring with a 2px offset.
        ///
        /// The ring is border-only (DrawCenter = false). Drawing a filled
        /// stylebox here would paint over the button's own state fill and
        /// turn a focused primary CTA into a dark slab.
        /// </summary>
        public static void StyleButtonFocus(Button button)
        {
            button.AddThemeStyleboxOverride("focus", FocusRing());
        }

        /// <summary>
        /// Loading state: the control stays laid out (no width change) but is
        /// dimmed and stops accepting input.
        ///
        /// The button is disabled rather than made click-through: a
        /// MouseFilter of Pass would let the click fall through to whatever
        /// sits behind the control.
        /// </summary>
        public static void StyleButtonLoading(Button button, bool loading)
        {
            button.Disabled = loading;
            button.Modulate = loading ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 1f);
        }

        /// <summary>
        /// Tabular-numeric label: fixed-width digits so live readouts never
        /// jitter as their values change.
        ///
        /// Godot's bundled font already ships tabular figures, so no font
        /// override is required; the label keeps its natural (left) alignment
        /// and its own font size - forcing a right alignment here used to
        /// leave the HUD stat stack ragged.
        /// </summary>
        public static void StyleLabelTabular(Label label, Color? color = null, int fontSize = 0)
        {
            label.AddThemeColorOverride("font_color", color ?? Text);
            if (fontSize > 0)
                label.AddThemeFontSizeOverride("font_size", fontSize);
        }

        /// <summary>Tactile slider value label.</summary>
        public static void StyleSliderValueTactile(Label label)
        {
            label.AddThemeColorOverride("font_color", Text);
            label.AddThemeFontSizeOverride("font_size", 14);
            label.CustomMinimumSize = new Vector2(52, 0); // hit-target-friendly width
            label.HorizontalAlignment = HorizontalAlignment.Right;
        }

        /// <summary>
        /// Tactile slider: dark track with a celestial filled portion.
        ///
        /// Godot themes the HSlider track as "slider" and the filled part as
        /// "grabber_area"; without these overrides the Settings screen keeps
        /// the stock light-grey Godot widget.
        /// </summary>
        public static void StyleSlider(HSlider slider)
        {
            var track = new StyleBoxFlat
            {
                BgColor = SurfaceCard,
                BorderColor = Border,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                ContentMarginTop = 3f,
                ContentMarginBottom = 3f,
                AntiAliasing = true,
            };
            var fill = new StyleBoxFlat
            {
                BgColor = StatusNominal,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                ContentMarginTop = 3f,
                ContentMarginBottom = 3f,
                AntiAliasing = true,
            };
            slider.AddThemeStyleboxOverride("slider", track);
            slider.AddThemeStyleboxOverride("grabber_area", fill);
            slider.AddThemeStyleboxOverride("grabber_area_highlight", fill);
        }

        /// <summary>Tactile check button: recoloured text and a custom celestial
        /// switch so the Settings screen does not keep the stock Godot widget.</summary>
        public static void StyleCheckButton(CheckButton check)
        {
            check.AddThemeColorOverride("font_color", Text);
            check.AddThemeColorOverride("font_hover_color", Text);
            check.AddThemeColorOverride("font_pressed_color", Gold);
            check.AddThemeColorOverride("font_hover_pressed_color", Gold);
            check.AddThemeColorOverride("font_focus_color", Text);
            check.AddThemeColorOverride("font_disabled_color", TextMuted);
            check.AddThemeIconOverride("checked", SwitchIcon(true));
            check.AddThemeIconOverride("unchecked", SwitchIcon(false));
            check.AddThemeIconOverride("checked_disabled", SwitchIcon(false, 0.45f));
            check.AddThemeIconOverride("unchecked_disabled", SwitchIcon(false, 0.45f));
        }

        private static ImageTexture? _switchOn;
        private static ImageTexture? _switchOff;

        /// <summary>Procedural switch icon (rounded track + sliding knob).</summary>
        private static ImageTexture SwitchIcon(bool on, float opacity = 1f)
        {
            if (opacity >= 1f)
            {
                if (on && _switchOn != null) return _switchOn;
                if (!on && _switchOff != null) return _switchOff;
            }

            const int W = 30, H = 16;
            var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
            var trackOn = new Color(0.220f, 0.741f, 0.973f, 0.80f * opacity);
            var trackOff = new Color(1f, 1f, 1f, 0.08f * opacity);
            var rim = new Color(1f, 1f, 1f, 0.22f * opacity);
            var knob = on
                ? new Color(1f, 1f, 1f, 0.95f * opacity)
                : new Color(1f, 1f, 1f, 0.50f * opacity);

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (InRoundedRect(x, y, 0, 0, W - 1, H - 1, H / 2f))
                    {
                        var c = on ? trackOn : trackOff;
                        if (!InRoundedRect(x, y, 1, 1, W - 2, H - 2, (H / 2f) - 1f))
                            c = rim;
                        img.SetPixel(x, y, c);
                    }
                    int knobX = on ? W - 8 : 7;
                    if (InCircle(x, y, knobX, H / 2f, 5.5f))
                        img.SetPixel(x, y, knob);
                }
            }

            var tex = ImageTexture.CreateFromImage(img);
            if (opacity >= 1f)
            {
                if (on) _switchOn = tex; else _switchOff = tex;
            }
            return tex;
        }

        private static bool InRoundedRect(float x, float y, float left, float top, float right, float bottom, float radius)
        {
            if (x < left || x > right || y < top || y > bottom) return false;
            // A radius larger than half the box would invert the clamp range.
            float r = Mathf.Min(radius, Mathf.Min(right - left, bottom - top) * 0.5f);
            float cx = Mathf.Clamp(x, left + r, right - r);
            float cy = Mathf.Clamp(y, top + r, bottom - r);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool InCircle(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>
        /// Tactile progress bar: dark track, accent fill, hairline border.
        ///
        /// Godot 4 themes the track as "background" - the older "bg" item
        /// name is silently ignored, which left the HUD bars on the stock
        /// Godot look.
        /// </summary>
        public static void StyleProgressBarTactile(ProgressBar bar, Color? fill = null)
        {
            bar.AddThemeStyleboxOverride("background", TrackBox(SurfaceCard, Border));
            bar.AddThemeStyleboxOverride("fill", TrackBox(fill ?? StatusNominal, Border));
            bar.AddThemeColorOverride("font_color", Text);
            bar.AddThemeFontSizeOverride("font_size", 12);
        }

        /// <summary>
        /// Duration that respects reduced motion: near-instant when reduced
        /// motion is requested, otherwise the requested duration.
        ///
        /// Driven by the project setting "accessibility/reduced_motion".
        /// </summary>
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
