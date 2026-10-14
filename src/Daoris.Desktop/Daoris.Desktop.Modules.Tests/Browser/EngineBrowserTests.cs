namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Daoris's own browser on the engine it ships (D85, CHR3): `daoris-browser`, a process of its own
/// showing the engine's own window — since CHR8 (D99) the application's own executable started with the
/// browser's argument, on the kit's engine. The process and the window are the engine's; what is here is
/// every judgement around them — which start is the browser, the arguments the shell writes and the
/// browser parses, where its profile is, which language it asks for, and which targets are windows.
/// </summary>
public sealed class EngineBrowserTests : Bridge
{
    private static new readonly string Home = Path.Combine(Path.GetTempPath(), "daoris-engine-home");

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
        Assert.Equal(["--daoris-browser", $"--daoris-profile={plain.Profile}", "--daoris-port=9422"], plain.ToArguments());
        Assert.Equal(plain, EngineBrowserOptions.Parse(plain.ToArguments(), out _));
    }

    /// <summary>
    /// CHR8: the application is the browser only when its first argument says so. First, so an engine
    /// process (`--type=`, which Chromium starts from the same executable) is never taken for it, and
    /// the window's own start is never taken for it either.
    /// </summary>
    [Fact]
    public void The_application_is_the_browser_only_when_the_browsers_argument_comes_first()
    {
        Assert.True(EngineBrowser.IsBrowserProcess(["--daoris-browser", "--daoris-port=9422"]));
        Assert.True(EngineBrowser.IsBrowserProcess(["--daoris-browser"]));

        Assert.False(EngineBrowser.IsBrowserProcess([]));
        Assert.False(EngineBrowser.IsBrowserProcess(["--app-root", "D:/install"]));
        Assert.False(EngineBrowser.IsBrowserProcess(["--type=renderer", "--daoris-browser"]));
        Assert.False(EngineBrowser.IsBrowserProcess(["--daoris-browser-x"]));
        Assert.False(EngineBrowser.IsBrowserProcess(["--DAORIS-BROWSER"]));
    }

    /// <summary>
    /// CHR8: the browser's command line is Chromium's too, and Chromium takes a switch's value only after
    /// `=`, so a value given as the next word would reach it as a loose argument. Every argument is one
    /// switch, and every one is Daoris's by its prefix, which no Chromium switch has.
    /// </summary>
    [Fact]
    public void Every_argument_is_one_switch_of_daoris_own()
    {
        var written = new EngineBrowserOptions(Path.Combine(Home, "with space", "engine"), 9422, Parent: 4242, Background: true);

        Assert.Equal(
            ["--daoris-browser", $"--daoris-profile={written.Profile}", "--daoris-port=9422", "--daoris-parent=4242", "--daoris-background"],
            written.ToArguments());
        Assert.All(written.ToArguments(), argument => Assert.StartsWith("--daoris-", argument));
        Assert.Equal(written, EngineBrowserOptions.Parse(written.ToArguments(), out _));
    }

    /// <summary>With no profile the engine would choose a folder of its own, under the user profile (D63).</summary>
    [Theory]
    [InlineData(new[] { "--daoris-browser", "--daoris-port=9422" }, "--daoris-profile")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile=relative/engine", "--daoris-port=9422" }, "--daoris-profile")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile=", "--daoris-port=9422" }, "--daoris-profile")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile=@", "--daoris-port=80" }, "--daoris-port")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile=@", "--daoris-port=nine" }, "--daoris-port")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile=@" }, "--daoris-port")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile=@", "--daoris-port=9422", "--daoris-parent=0" }, "--daoris-parent")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile=@", "--daoris-port=9422", "--incognito" }, "--incognito")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile", "@", "--daoris-port=9422" }, "--daoris-profile")]
    [InlineData(new[] { "--daoris-browser", "--daoris-profile=@", "--daoris-port=9422", "--daoris-background=yes" }, "--daoris-background")]
    [InlineData(new[] { "--daoris-profile=@", "--daoris-port=9422" }, "--daoris-browser")]
    public void Anything_it_cannot_start_on_is_refused_and_named(string[] arguments, string named)
    {
        var withHome = arguments.Select(a => a.Replace("@", EngineBrowser.ProfileFolder(Home), StringComparison.Ordinal)).ToArray();

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

    /// <summary>
    /// CHR8, held on the application's own source because nothing else can hold it: the kit starts
    /// Chromium as the app is composed and a process runs one Chromium, so the browser is decided before
    /// anything else in <c>Main</c>, and it is started with the kit's <c>ChromiumBrowserProcess.Start</c>,
    /// which hands it none of the app's handles (a <c>Process.Start</c> kept the app from exiting).
    /// </summary>
    [Fact]
    public void The_application_decides_on_the_browser_first_and_starts_it_through_the_kit()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App");
        var program = File.ReadAllText(Path.Combine(app, "Program.cs"));
        var main = program[program.IndexOf("static void Main(", StringComparison.Ordinal)..];

        var routed = main.IndexOf("EngineBrowser.IsBrowserProcess(args)", StringComparison.Ordinal);
        Assert.True(routed > 0, "Main does not ask whether it is the browser");
        foreach (var later in new[] { "InstallHome.Establish(", "MachineLog.Open(", "CreateBuilder(", "UseChromiumEngine(" })
        {
            var at = main.IndexOf(later, StringComparison.Ordinal);
            Assert.True(at > routed, $"`{later}` runs before Main has decided whether it is the browser");
        }

        Assert.Contains("ChromiumBrowserProcess.Run(", File.ReadAllText(Path.Combine(app, "BrowserProcess.cs")));

        var host = File.ReadAllText(Path.Combine(app, "EngineBrowserHost.cs"));
        Assert.Contains("ChromiumBrowserProcess.Start(options.ToArguments())", host);
        Assert.DoesNotContain("EngineBrowser.Locate", host);
    }

    /// <summary>The retired executable's project is gone with it: one engine in the install (CHR8).</summary>
    [Fact]
    public void There_is_no_second_engine_to_build()
    {
        var desktop = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");

        // The project file, not the folder: a checkout that built the old project keeps its ignored
        // `bin/` and `obj/` after the pull, and that leftover is not a second engine.
        Assert.False(
            File.Exists(Path.Combine(desktop, "Daoris.Desktop.Browser", "Daoris.Desktop.Browser.csproj")),
            "the CefSharp browser's project is still here");
        Assert.DoesNotContain("CefSharp", File.ReadAllText(Path.Combine(desktop, "Directory.Packages.props")));
    }

    /// <summary>Walk up to the workspace root — the tests run from `bin/Debug/net10.0`.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
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
