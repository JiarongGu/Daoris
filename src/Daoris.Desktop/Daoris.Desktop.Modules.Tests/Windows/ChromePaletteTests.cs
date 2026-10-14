using System.Drawing;
using System.Text.RegularExpressions;
using Daoris.Desktop;

namespace Daoris.Desktop.Tests;

/// <summary>
/// The native chrome copies five of the page's colour tokens, because Windows cannot read a
/// stylesheet (SURF7 / D56). This is what stops the copy drifting.
/// </summary>
/// <remarks>
/// A duplicated theme file marked "keep in sync" is the exact shape this family refuses elsewhere
/// (frontend architecture §2, where a sibling's hand-duplicated theme was the reason NOT to adopt its
/// styling stack). The duplication here is forced — the DWM border and the caption buttons are
/// painted natively — so the answer is not to avoid the copy but to make it impossible to let it rot:
/// this parses the stylesheet and fails when a value moves on one side only.
/// </remarks>
public sealed class ChromePaletteTests
{
    /// <summary>
    /// `tokens.css`, found from the test binary. Walking up to the workspace root rather than taking
    /// a relative hop count, so the test survives a change of output layout.
    /// </summary>
    private static string TokensCss()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Daoris.Web", "src", "tokens.css");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "tokens.css was not found above the test binary. If the web package moved, this test is "
            + "the thing that tells you the native chrome's palette no longer has a source of truth.");
    }

    /// <summary>
    /// The declarations from one `:root` block — the first is light, the one inside the
    /// `prefers-color-scheme: dark` media query is dark.
    /// </summary>
    private static Dictionary<string, Color> Declarations(string css, bool dark)
    {
        var blocks = Regex.Matches(css, @":root\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);
        Assert.True(blocks.Count >= 2, "tokens.css should declare a light :root and a dark one.");

        // The dark block is the one inside the media query; it is the second `:root` in the file.
        var body = blocks[dark ? 1 : 0].Groups["body"].Value;

        return Regex.Matches(body, @"(?<name>--[a-z-]+)\s*:\s*(?<value>#[0-9a-fA-F]{6})\s*;")
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => ColorTranslator.FromHtml(match.Groups["value"].Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryCopiedTokenMatchesTheStylesheet(bool dark)
    {
        var css = Declarations(TokensCss(), dark);
        var palette = ChromePalette.For(dark);

        foreach (var (token, painted) in palette.Tokens)
        {
            Assert.True(css.TryGetValue(token, out var declared),
                $"`{token}` is painted by the native chrome but no longer declared in tokens.css.");
            Assert.Equal(declared.ToArgb(), painted.ToArgb());
        }
    }

    /// <summary>
    /// The parser is the half that fails silently: a regex that stops matching finds no tokens, every
    /// loop above runs zero times, and the suite goes green having checked nothing.
    /// </summary>
    [Fact]
    public void TheStylesheetIsActuallyBeingRead()
    {
        var light = Declarations(TokensCss(), dark: false);
        var dark = Declarations(TokensCss(), dark: true);

        Assert.True(light.Count >= 10, $"only {light.Count} light tokens parsed — the reader has stopped reading.");
        Assert.True(dark.Count >= 10, $"only {dark.Count} dark tokens parsed — the reader has stopped reading.");
        // And the two blocks are genuinely different, so neither is being read twice.
        Assert.NotEqual(light["--page"].ToArgb(), dark["--page"].ToArgb());
    }

    [Fact]
    public void ForPicksTheSetTheThemeFlagNames()
    {
        Assert.Same(ChromePalette.Dark, ChromePalette.For(dark: true));
        Assert.Same(ChromePalette.Light, ChromePalette.For(dark: false));
        Assert.True(ChromePalette.Dark.IsDark);
        Assert.False(ChromePalette.Light.IsDark);
    }

    /// <summary>
    /// The seam rule: the caption cluster is cut out of the WebView2, so the colour behind it has to
    /// be the app strip's own background — `--raised`, which is what the strip uses.
    /// </summary>
    [Fact]
    public void TheCaptionSurfaceIsTheStripsOwnBackground()
    {
        foreach (var palette in new[] { ChromePalette.Light, ChromePalette.Dark })
        {
            Assert.Equal(palette.Tokens["--raised"].ToArgb(), palette.Surface.ToArgb());
        }
    }
}
