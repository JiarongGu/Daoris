using System.Drawing;

namespace Daoris.Desktop;

/// <summary>
/// The few colours the NATIVE chrome paints with, once the window is frameless.
/// </summary>
/// <remarks>
/// <para><b>These are copies of <c>tokens.css</c>, and the copy is the price of a frameless
/// window.</b> The DWM border, the form fill behind the splash and the caption buttons are painted
/// by Windows and by the framework, neither of which can read a stylesheet. So the values are
/// duplicated — and because a duplicated palette marked "keep in sync" is exactly the drift shape
/// this family removes elsewhere, <c>ChromePaletteTests</c> parses
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
    /// <summary>Warm paper: <c>--page</c>, <c>--raised</c>, <c>--line</c>, <c>--ink</c>, from the page's role set.</summary>
    public static readonly ChromePalette Light = new(
        Page: Rgb("#f7f6f2"),
        Surface: Rgb("#ffffff"),
        Line: Rgb("#d5d3cb"),
        Ink: Rgb("#1a1a18"),
        // A hovered caption button wears `--hover`, the field every quiet control on the page lifts to
        // under the pointer, and pressed the card edge, `--line`, one step further.
        Hover: Rgb("#ecebe5"),
        Pressed: Rgb("#d5d3cb"),
        // Close goes red on hover, which is platform convention and not ours to reinvent. The hue is
        // `--fail` and its glyph `--on-fail`, so even the OS gesture speaks the palette.
        CloseHover: Rgb("#d1322a"),
        ClosePressed: Rgb("#a8201a"),
        CloseGlyphHot: Rgb("#ffffff"));

    /// <summary>The dark set — its own validated values, never a filter over the light one.</summary>
    public static readonly ChromePalette Dark = new(
        Page: Rgb("#131312"),
        Surface: Rgb("#1b1b19"),
        Line: Rgb("#3a3936"),
        Ink: Rgb("#ecebe6"),
        Hover: Rgb("#2a2a27"),
        Pressed: Rgb("#3a3936"),
        CloseHover: Rgb("#ee5a50"),
        ClosePressed: Rgb("#d1322a"),
        CloseGlyphHot: Rgb("#111110"));

    /// <summary>The set for a theme, by the one flag the page sends over <c>SET_THEME</c>.</summary>
    public static ChromePalette For(bool dark) => dark ? Dark : Light;

    /// <summary>Whether this is the dark set.</summary>
    public bool IsDark => ReferenceEquals(this, Dark);

    /// <summary>
    /// The tokens this palette copies, by the name <c>tokens.css</c> gives them — the map the
    /// drift test walks. The pressed colours are not here: they are this window's own arrangement
    /// of the same values, not a copy of anything.
    /// </summary>
    public IReadOnlyDictionary<string, Color> Tokens => new Dictionary<string, Color>
    {
        ["--page"] = Page,
        ["--raised"] = Surface,
        ["--line"] = Line,
        ["--ink"] = Ink,
        ["--hover"] = Hover,
        ["--fail"] = CloseHover,
        ["--on-fail"] = CloseGlyphHot,
    };

    private static Color Rgb(string hex) => Color.FromArgb(
        Convert.ToInt32(hex.Substring(1, 2), 16),
        Convert.ToInt32(hex.Substring(3, 2), 16),
        Convert.ToInt32(hex.Substring(5, 2), 16));
}
