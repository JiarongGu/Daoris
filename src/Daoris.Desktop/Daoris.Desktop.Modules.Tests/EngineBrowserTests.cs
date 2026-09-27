namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Daoris's own browser on the engine it ships (D85, CHR3): `daoris-browser`, a process of its own
/// showing the engine's own window. The process and the window are the engine's; what is here is every
/// judgement around them — the arguments the shell writes and the browser parses, where its profile
/// is, which language it asks for, where the executable is found, and which targets are windows.
/// </summary>
public sealed class EngineBrowserTests : Bridge
{
    private static readonly string Home = Path.Combine(Path.GetTempPath(), "daoris-engine-home");

    /// <summary>
    /// Under the home (D63), and apart from the WebView2 window's old `browser/profile`, which a
    /// machine that ran it still holds: another engine's files, never read as this one's.
    /// </summary>
    [Fact]
    public void Its_profile_is_the_homes_and_not_the_webview_windows()
    {
        Assert.Equal(Path.Combine(Home, "browser", "engine"), EngineBrowser.ProfileFolder(Home));
        Assert.NotEqual(Path.Combine(Home, "browser", "profile"), EngineBrowser.ProfileFolder(Home));
        Assert.Equal(Path.Combine(Home, "browser", "engine", "Default"),
            EngineBrowser.ProfileDirectory(EngineBrowser.ProfileFolder(Home)));
    }

    [Fact]
    public void What_the_shell_writes_the_browser_reads_back()
    {
        var written = new EngineBrowserOptions(EngineBrowser.ProfileFolder(Home), 9422, Parent: 4242, Background: true);

        Assert.Equal(written, EngineBrowserOptions.Parse(written.ToArguments(), out var problem));
        Assert.Null(problem);

        var plain = written with { Parent = null, Background = false };
        Assert.Equal(["--profile", plain.Profile, "--port", "9422"], plain.ToArguments());
        Assert.Equal(plain, EngineBrowserOptions.Parse(plain.ToArguments(), out _));
    }

    /// <summary>With no profile the engine would choose a folder of its own, under the user profile (D63).</summary>
    [Theory]
    [InlineData(new[] { "--port", "9422" }, "--profile")]
    [InlineData(new[] { "--profile", "relative/engine", "--port", "9422" }, "--profile")]
    [InlineData(new[] { "--profile", "@", "--port", "80" }, "--port")]
    [InlineData(new[] { "--profile", "@", "--port", "nine" }, "--port")]
    [InlineData(new[] { "--profile", "@" }, "--port")]
    [InlineData(new[] { "--profile", "@", "--port", "9422", "--parent", "0" }, "--parent")]
    [InlineData(new[] { "--profile", "@", "--port", "9422", "--incognito" }, "--incognito")]
    public void Anything_it_cannot_start_on_is_refused_and_named(string[] arguments, string named)
    {
        var withHome = arguments.Select(a => a == "@" ? EngineBrowser.ProfileFolder(Home) : a).ToArray();

        Assert.Null(EngineBrowserOptions.Parse(withHome, out var problem));
        Assert.Contains(named, problem);
    }

    /// <summary>The two locales an install keeps: the engine is never asked for one that was left out.</summary>
    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-Hans-CN", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("en-AU", "en-US")]
    [InlineData("fr-FR", "en-US")]
    [InlineData("", "en-US")]
    public void It_asks_the_engine_for_a_language_the_install_keeps(string culture, string locale) =>
        Assert.Equal(locale, EngineBrowser.Locale(culture));

    /// <summary>What the install carries first, then a hand-assembled folder, then the workspace build.</summary>
    [Fact]
    public void It_is_found_where_the_install_puts_it_before_the_workspace_build()
    {
        var workspace = Directory.CreateTempSubdirectory("daoris-engine-");
        try
        {
            File.WriteAllText(Path.Combine(workspace.FullName, "daoris.json"), "{}");
            var shell = Directory.CreateDirectory(Path.Combine(workspace.FullName, "install")).FullName;

            var candidates = EngineBrowser.Candidates(shell);
            Assert.Equal(Path.Combine(shell, "app", "daoris-browser", EngineBrowser.ExecutableName), candidates[0]);
            Assert.Equal(Path.Combine(shell, "daoris-browser", EngineBrowser.ExecutableName), candidates[1]);
            Assert.All(candidates.Skip(2), c => Assert.StartsWith(
                Path.Combine(workspace.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Browser", "bin"), c));
            Assert.Null(EngineBrowser.Locate(shell));

            var built = candidates[^1];
            Directory.CreateDirectory(Path.GetDirectoryName(built)!);
            File.WriteAllText(built, "");
            Assert.Equal(built, EngineBrowser.Locate(shell));

            var installed = candidates[0];
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            File.WriteAllText(installed, "");
            Assert.Equal(installed, EngineBrowser.Locate(shell));
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    /// <summary>A folder with no workspace above it offers the install's two places and nothing else.</summary>
    [Fact]
    public void Outside_a_workspace_it_looks_only_where_an_install_puts_it()
    {
        var install = Directory.CreateTempSubdirectory("daoris-install-");
        try
        {
            Assert.Equal(2, EngineBrowser.Candidates(install.FullName).Count);
        }
        finally
        {
            install.Delete(recursive: true);
        }
    }

    /// <summary>A window the person sees is a page; the engine's own parts and workers are not.</summary>
    [Fact]
    public void Its_pages_are_the_targets_a_person_sees()
    {
        const string list = """
            [
              { "id": "A", "type": "page", "url": "chrome://newtab/" },
              { "id": "B", "type": "browser_ui", "url": "chrome://omnibox-popup.top-chrome/" },
              { "id": "C", "type": "service_worker", "url": "https://site.example/sw.js" },
              { "id": "D", "type": "page", "url": "https://site.example/" },
              { "type": "page", "url": "no id" }
            ]
            """;

        Assert.Equal(["A", "D"], EngineBrowser.Pages(list));
        Assert.Empty(EngineBrowser.Pages("{}"));
        Assert.Empty(EngineBrowser.Pages("[]"));
    }

    /// <summary>An engine that is not running answers nothing, and nothing is not a crash.</summary>
    [Fact]
    public async Task An_engine_that_is_not_there_answers_nothing()
    {
        using var engine = new EngineCdp(InAppBrowser.FreePort());

        Assert.False(await engine.AnswersAsync());
        Assert.Null(await engine.PagesAsync());
    }
}
