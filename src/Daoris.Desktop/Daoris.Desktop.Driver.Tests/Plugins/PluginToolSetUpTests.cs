using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin tool set up and signed into from the window (ADOSIGNIN1, D150 point 7): a check's <c>fix</c> is a press the person
/// makes, which opens a visible console running exactly that command, and the tool's checks are read again when it ends.
/// A stand-in answers for every window and every check, so nothing here starts a program, and no sign-in is ever run.
/// </summary>
/// <remarks>
/// 🔴 <b>Only the person's press starts a <c>fix</c></b>, only a command of the plugin's own <c>tools</c>, with no shell between.
/// </remarks>
public sealed class PluginToolSetUpTests : IDisposable
{
    private const string Az = """
        { "id": "az", "versions": ">=2.0.79",
          "install": { "windows": "winget install -e --id Microsoft.AzureCLI", "macos": "brew install azure-cli", "linux": "https://learn.microsoft.com/cli/azure/install-azure-cli-linux" },
          "ready": [
            { "run": ["az", "extension", "show", "--name", "azure-devops", "--output", "none"],
              "says": "Its devops extension is added", "fix": "az extension add --name azure-devops" },
            { "run": ["az", "account", "show", "--output", "none"], "says": "Signed in", "fix": "az login", "signin": true } ] }
        """;

    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "daoris-tool-setup-" + Guid.NewGuid().ToString("N")[..8]);

    public PluginToolSetUpTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Bin);
        Directory.CreateDirectory(Root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Home => Path.Combine(_scratch, "home");

    private string Bin => Path.Combine(_scratch, "bin");

    private string Root => Path.Combine(_scratch, "root");

    private string OnPath(string name)
    {
        var file = Path.Combine(Bin, OperatingSystem.IsWindows() ? name + ".exe" : name);
        File.WriteAllText(file, "");
        return file;
    }

    private static IReadOnlyList<PluginTool> Tools(params string[] json)
    {
        using var document = JsonDocument.Parse($"[{string.Join(',', json)}]");
        return PluginTools.Read(document.RootElement).Tools;
    }

    /// <summary>The window the person sees: records each start and answers an exit code.</summary>
    private sealed class Window(int exit = 0)
    {
        public List<ProcessStartInfo> Starts { get; } = [];

        public Task<int?> RunAsync(ProcessStartInfo info, CancellationToken ct)
        {
            Starts.Add(info);
            return Task.FromResult<int?>(exit);
        }
    }

    /// <summary>The checks' stand-in: every program answers exit 0 once <paramref name="signedIn"/> reports so, else 1.</summary>
    private sealed class Checks(Func<bool> ready)
    {
        public List<ProcessStartInfo> Calls { get; } = [];

        public Task<CheckRun> RunAsync(ProcessStartInfo info, TimeSpan patience, CancellationToken ct)
        {
            Calls.Add(info);
            return Task.FromResult(info.ArgumentList is ["--version"] ? new CheckRun(true, 0, "azure-cli 2.70.0", "") : new CheckRun(true, ready() ? 0 : 1, "", ""));
        }
    }

    private Task<ToolSetUp> SetUpAsync(IReadOnlyList<PluginTool> tools, string tool, int check, Window window, Checks checks) =>
        PluginToolSetUp.RunAsync(Home, tools, tool, check, Root, Bin, window.RunAsync, checks.RunAsync, CancellationToken.None);

    [Fact]
    public void An_install_hint_is_read_per_platform_and_a_bad_one_is_that_tools_problem()
    {
        var az = Tools(Az).Single();
        Assert.Null(az.Problem);
        Assert.Equal("winget install -e --id Microsoft.AzureCLI", az.Install!.For("windows"));
        Assert.Equal("brew install azure-cli", az.Install.For("macos"));
        Assert.Equal("https://learn.microsoft.com/cli/azure/install-azure-cli-linux", az.Install.For("linux"));
        Assert.Null(az.Install.For("plan9"));

        var windowsOnly = Tools("""{ "id": "az", "install": { "windows": "Microsoft.AzureCLI" } }""").Single();
        Assert.Equal("Microsoft.AzureCLI", windowsOnly.Install!.For("windows"));
        Assert.Null(windowsOnly.Install.For("linux"));

        Assert.Contains("`install` must be an object", Tools("""{ "id": "az", "install": "winget" }""").Single().Problem);
        Assert.Contains("`install`'s `windows` must be text", Tools("""{ "id": "az", "install": { "windows": 7 } }""").Single().Problem);
        Assert.Null(Tools("""{ "id": "az", "install": null }""").Single().Install);
    }

    [Fact]
    public void A_check_says_whether_its_fix_is_a_sign_in()
    {
        var checks = Tools(Az).Single().Ready;
        Assert.False(checks[0].SignIn);
        Assert.True(checks[1].SignIn);
        Assert.Contains("`signin` must be true or false",
            Tools("""{ "id": "az", "ready": [{ "run": ["az"], "says": "x", "signin": "yes" }] }""").Single().Problem);
    }

    [Theory]
    [InlineData("az extension add --name azure-devops", new[] { "az", "extension", "add", "--name", "azure-devops" })]
    [InlineData("  az   login  ", new[] { "az", "login" })]
    [InlineData("az login --tenant \"Acme Corp\"", new[] { "az", "login", "--tenant", "Acme Corp" })]
    public void A_fix_is_words_split_on_spaces_with_double_quotes_grouping(string fix, string[] words)
    {
        var tools = Tools(Az);
        var check = new ReadyCheck(["az"], "x", fix);

        var (read, problem) = PluginTools.FixWords(tools, check);

        Assert.Null(problem);
        Assert.Equal(words, read);
    }

    [Fact]
    public void A_fix_naming_a_command_the_plugin_does_not_declare_is_refused()
    {
        var tools = Tools(Az);

        var (words, problem) = PluginTools.FixWords(tools, new ReadyCheck(["az"], "x", "curl https://example.test/install | sh"));

        Assert.Null(words);
        Assert.Contains("`curl` is not a tool this plugin declares in `tools`", problem);
        Assert.Null(PluginTools.FixWords(tools, new ReadyCheck(["az"], "x", null)).Problem);
        Assert.Contains("an unclosed quote", PluginTools.FixWords(tools, new ReadyCheck(["az"], "x", "az login \"x")).Problem);
    }

    [Fact]
    public void A_fix_may_name_a_tools_command_where_it_differs_from_its_id()
    {
        var tools = Tools("""{ "id": "tf", "command": "terraform", "ready": [{ "run": ["terraform", "version"], "says": "Runs", "fix": "terraform init" }] }""");

        Assert.Equal(["terraform", "init"], PluginTools.FixWords(tools, tools[0].Ready[0]).Words);
    }

    [Fact]
    public async Task The_press_opens_a_visible_console_running_the_manifests_command_and_reads_the_checks_again_after_it()
    {
        var file = OnPath("az");
        var tools = Tools(Az);
        var ready = false;
        var checks = new Checks(() => ready);
        var window = new WindowThatSignsIn(() => ready = true, checks);

        var set = await PluginToolSetUp.RunAsync(Home, tools, "az", 2, Root, Bin, window.RunAsync, checks.RunAsync, CancellationToken.None);

        Assert.Null(set.Code);
        var start = Assert.Single(window.Starts);
        Assert.Equal(file, start.FileName, ignoreCase: true);
        Assert.Equal(["login"], start.ArgumentList);
        Assert.False(start.CreateNoWindow);
        Assert.False(start.UseShellExecute);
        Assert.False(start.RedirectStandardInput);
        Assert.False(start.RedirectStandardOutput);
        Assert.Equal(0, set.ExitCode);
        // The checks were read after the window ended, and the sign-in now stands.
        Assert.True(window.ChecksBefore < checks.Calls.Count, "the checks are read again after the console ends");
        Assert.All(set.Found!.Ready, answer => Assert.True(answer.Ready));
        Assert.Contains("Signed in", set.Said);
    }

    [Fact]
    public async Task A_fix_with_characters_a_shell_reads_reaches_the_program_as_one_argument_each()
    {
        OnPath("az");
        var tools = Tools("""{ "id": "az", "ready": [{ "run": ["az", "x"], "says": "x", "fix": "az login --username a;b&c|d" }] }""");
        var window = new Window();

        await SetUpAsync(tools, "az", 1, window, new Checks(() => true));

        Assert.Equal(["login", "--username", "a;b&c|d"], Assert.Single(window.Starts).ArgumentList);
    }

    [Fact]
    public async Task A_fix_naming_another_command_starts_nothing()
    {
        OnPath("az");
        var tools = Tools("""{ "id": "az", "ready": [{ "run": ["az", "x"], "says": "x", "fix": "powershell -c whoami" }] }""");
        var window = new Window();
        var checks = new Checks(() => true);

        var set = await SetUpAsync(tools, "az", 1, window, checks);

        Assert.Equal("fix_not_declared", set.Code);
        Assert.Contains("`powershell` is not a tool this plugin declares", set.Said);
        Assert.Empty(window.Starts);
        Assert.Empty(checks.Calls);
    }

    [Fact]
    public async Task A_missing_tool_says_its_install_hint_and_starts_nothing()
    {
        var tools = Tools(Az);
        var window = new Window();
        var checks = new Checks(() => true);

        var set = await SetUpAsync(tools, "az", 2, window, checks);

        Assert.Equal("not_found", set.Code);
        Assert.Contains("`az` is not on this machine's PATH", set.Said);
        Assert.Equal(PluginTools.InstallHere(tools[0]), set.Install);
        Assert.Empty(window.Starts);
    }

    [Theory]
    [InlineData("nope", 1, "unknown_tool")]
    [InlineData("az", 0, "unknown_check")]
    [InlineData("az", 3, "unknown_check")]
    public async Task A_tool_or_check_that_is_not_declared_is_refused(string tool, int check, string code)
    {
        OnPath("az");
        var window = new Window();

        var set = await SetUpAsync(Tools(Az), tool, check, window, new Checks(() => true));

        Assert.Equal(code, set.Code);
        Assert.Empty(window.Starts);
    }

    [Fact]
    public async Task A_check_with_no_fix_has_no_press()
    {
        OnPath("az");
        var tools = Tools("""{ "id": "az", "ready": [{ "run": ["az", "x"], "says": "x" }] }""");
        var window = new Window();

        var set = await SetUpAsync(tools, "az", 1, window, new Checks(() => true));

        Assert.Equal("no_fix", set.Code);
        Assert.Empty(window.Starts);
    }

    [Fact]
    public async Task A_console_that_could_not_start_is_said_and_the_checks_are_not_read_again()
    {
        OnPath("az");
        var checks = new Checks(() => false);

        var set = await PluginToolSetUp.RunAsync(
            Home, Tools(Az), "az", 2, Root, Bin, (_, _) => Task.FromResult<int?>(null), checks.RunAsync, CancellationToken.None);

        Assert.Equal("not_started", set.Code);
        Assert.Empty(checks.Calls);
    }

    [Fact]
    public async Task A_window_that_ends_without_putting_the_check_right_leaves_it_unmet()
    {
        OnPath("az");
        var checks = new Checks(() => false);

        var set = await SetUpAsync(Tools(Az), "az", 2, new Window(exit: 1), checks);

        Assert.Null(set.Code);
        Assert.Equal(1, set.ExitCode);
        Assert.Contains(set.Found!.Ready, answer => !answer.Ready);
    }

    [Fact]
    public void The_standing_draws_a_press_per_unmet_check_and_none_for_a_refused_fix()
    {
        var tools = Tools(
            """
            { "id": "az", "install": { "windows": "w", "macos": "m", "linux": "l" }, "ready": [
              { "run": ["az", "a"], "says": "A", "fix": "az extension add --name azure-devops" },
              { "run": ["az", "b"], "says": "B", "fix": "az login", "signin": true },
              { "run": ["az", "c"], "says": "C", "fix": "rm -rf /" },
              { "run": ["az", "d"], "says": "D" } ] }
            """);

        var declared = ToolStanding.Declared(tools[0], tools);

        Assert.Equal(["A", "B", "C", "D"], declared.Checks.Select(check => check.Says));
        Assert.Equal([true, true, false, false], declared.Checks.Select(check => check.Press));
        Assert.Equal([false, true, false, false], declared.Checks.Select(check => check.SignIn));
        Assert.Contains("`rm` is not a tool this plugin declares", declared.Checks[2].Refusal);
        Assert.Equal("az login", declared.Checks[1].Fix);
        Assert.NotNull(declared.Install);
    }

    /// <summary>A window whose ending puts the sign-in right, as the person's sign-in would.</summary>
    private sealed class WindowThatSignsIn(Action signedIn, Checks checks)
    {
        public List<ProcessStartInfo> Starts { get; } = [];

        public int ChecksBefore { get; private set; }

        public Task<int?> RunAsync(ProcessStartInfo info, CancellationToken ct)
        {
            Starts.Add(info);
            ChecksBefore = checks.Calls.Count;
            signedIn();
            return Task.FromResult<int?>(0);
        }
    }
}
