using System.Drawing;

namespace Daoris.Desktop;

/// <summary>
/// The few colours the NATIVE chrome paints with, once the window is frameless (SURF7 / D56).
/// </summary>
/// <remarks>
/// <para><b>These are copies of <c>tokens.css</c>, and the copy is the price of a frameless
/// window.</b> The DWM border, the form fill behind the splash and the caption buttons are painted
/// by Windows and by the framework, neither of which can read a stylesheet. So the values are
/// duplicated — and because a duplicated palette marked "keep in sync" is exactly the drift shape
/// this family removes elsewhere (frontend architecture §2), <c>ChromePaletteTests</c> parses
/// <c>tokens.css</c> and fails when the two disagree. The copy is kept to the values actually
/// painted rather than the whole palette, so there is less of it to drift.</para>
///
/// <para>It lives in this half, not in the window, so it can be tested at all: the window is
/// WinForms and has no test project by construction. What stays there is the mapping onto the
/// framework's own types, which has no branches in it.</para>
///
/// <para><b><see cref="Surface"/> must equal the app strip's background exactly.</b> The caption
/// rectangle is cut out of the WebView2, so any difference between this colour and the strip's shows
/// as a visible seam beside the buttons — the one place where "close enough" is visible at a glance.</para>
/// </remarks>
public sealed record ChromePalette(
    Color Page,
    Color Surface,
    Color Line,
    Color Ink,
    Color Hover,
    Color Pressed,
    Color CloseHover,
    Color ClosePressed,
    Color CloseGlyphHot)
{
    /// <summary>Warm paper: <c>--page</c>, <c>--raised</c>, <c>--line</c>, <c>--ink</c>.</summary>
    public static readonly ChromePalette Light = new(
        Page: Rgb("#faf9f6"),
        Surface: Rgb("#ffffff"),
        Line: Rgb("#e3e0d8"),
        Ink: Rgb("#1a1a18"),
        // A hovered caption button lifts to the line colour, pressed to line-strong — the same two
        // steps every other quiet control on the page uses.
        Hover: Rgb("#e3e0d8"),
        Pressed: Rgb("#cfcabe"),
        // Close goes red on hover, which is platform convention and not ours to reinvent. The hue is
        // D41's validated `--st-declined`, so even the OS gesture speaks the palette.
        CloseHover: Rgb("#9e2f24"),
        ClosePressed: Rgb("#7e251c"),
        CloseGlyphHot: Rgb("#faf9f6"));

    /// <summary>The dark set — its own validated values, never a filter over the light one.</summary>
    public static readonly ChromePalette Dark = new(
        Page: Rgb("#16161a"),
        Surface: Rgb("#1e1e23"),
        Line: Rgb("#2e2e34"),
        Ink: Rgb("#e8e6df"),
        Hover: Rgb("#2e2e34"),
        Pressed: Rgb("#3d3d45"),
        CloseHover: Rgb("#c74534"),
        ClosePressed: Rgb("#a3372a"),
        CloseGlyphHot: Rgb("#faf9f6"));

    /// <summary>The set for a theme, by the one flag the page sends over <c>SET_THEME</c>.</summary>
    public static ChromePalette For(bool dark) => dark ? Dark : Light;

    /// <summary>Whether this is the dark set.</summary>
    public bool IsDark => ReferenceEquals(this, Dark);

    /// <summary>
    /// The five tokens this palette copies, by the name <c>tokens.css</c> gives them — the map the
    /// drift test walks. The caption-button colours are not here: they are this window's own
    /// arrangement of the same values, not a copy of anything.
    /// </summary>
    public IReadOnlyDictionary<string, Color> Tokens => new Dictionary<string, Color>
    {
        ["--page"] = Page,
        ["--raised"] = Surface,
        ["--line"] = Line,
        ["--ink"] = Ink,
        ["--st-declined"] = CloseHover,
    };

    private static Color Rgb(string hex) => Color.FromArgb(
        Convert.ToInt32(hex.Substring(1, 2), 16),
        Convert.ToInt32(hex.Substring(3, 2), 16),
        Convert.ToInt32(hex.Substring(5, 2), 16));
}
