using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What `daoris-driver` was asked for (DRV8a, D104). Run with no verb to read its usage, it started a
/// headless loop on the install's home, and that loop took a quest two seconds before the desktop's
/// own. The loop is asked for by its verb, and everything else is the usage.
/// </summary>
public sealed class DriverCommandTests
{
    /// <summary>🔴 No verb is no loop: the usage, and nothing started.</summary>
    [Fact]
    public void A_bare_invocation_asks_for_no_loop()
    {
        Assert.Null(DriverCommand.Read([], out var problem));
        Assert.Null(problem);
    }

    /// <summary>The loop by its verb, in each of its modes, and the spelling scripts already use.</summary>
    [Theory]
    [InlineData(new[] { "drive" }, LoopMode.Watch, false)]
    [InlineData(new[] { "drive", "--once" }, LoopMode.Once, false)]
    [InlineData(new[] { "drive", "--until-idle" }, LoopMode.UntilIdle, false)]
    [InlineData(new[] { "drive", "--share" }, LoopMode.Watch, true)]
    [InlineData(new[] { "drive", "--until-idle", "--share" }, LoopMode.UntilIdle, true)]
    [InlineData(new[] { "--once" }, LoopMode.Once, false)]
    [InlineData(new[] { "--until-idle" }, LoopMode.UntilIdle, false)]
    [InlineData(new[] { "--until-idle", "--share" }, LoopMode.UntilIdle, true)]
    public void The_loop_is_asked_for_by_name(string[] args, LoopMode mode, bool share)
    {
        var asked = DriverCommand.Read(args, out var problem);

        Assert.Null(problem);
        Assert.Equal(new LoopRequest(mode, share), asked);
    }

    /// <summary>
    /// Anything else starts nothing, and says what was not understood — a word nobody answers used to fall
    /// through to the watch loop, which is how reading the usage became driving.
    /// </summary>
    [Theory]
    [InlineData(new[] { "watch" }, "`watch`")]
    [InlineData(new[] { "--share" }, "`--share`")]
    [InlineData(new[] { "drive", "--forever" }, "`--forever`")]
    [InlineData(new[] { "drive", "--once", "--until-idle" }, "one")]
    public void Anything_else_asks_for_no_loop_and_says_what_was_not_understood(string[] args, string named)
    {
        Assert.Null(DriverCommand.Read(args, out var problem));
        Assert.NotNull(problem);
        Assert.Contains(named, problem);
    }

    /// <summary>Asking for the usage is an answer, not a mistake.</summary>
    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Asking_for_help_is_no_loop_and_no_problem(string word)
    {
        Assert.Null(DriverCommand.Read([word], out var problem));
        Assert.Null(problem);
        Assert.True(DriverCommand.AskedForHelp([word]));
        Assert.False(DriverCommand.AskedForHelp([]));
    }

    /// <summary>
    /// The usage a bare invocation prints names every verb the host answers — read from the host's own
    /// source, so a verb added there without a line here is a failing test rather than a door nobody
    /// can find.
    /// </summary>
    [Fact]
    public void The_usage_names_every_verb_the_host_answers()
    {
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var verbs = Regex.Matches(program, @"args is \[""(?<verb>[a-z]+)""")
            .Select(match => match.Groups["verb"].Value)
            .Distinct()
            .ToList();

        Assert.True(verbs.Count >= 8, $"expected the host's verbs, found {string.Join(", ", verbs)}");
        foreach (var verb in verbs.Append("drive"))
        {
            Assert.Contains($"\n  {verb} ", DriverCommand.Usage.ReplaceLineEndings("\n"));
        }
    }

    /// <summary>
    /// WSR6: bringing a repository up to date after its pull request merged is a `trees` verb, and the host's
    /// terminal door says it in the usage — the list first, `--yes` the press. WSR7 (D112): `--all` takes every
    /// repository with a checkout, beside those holding Daoris's branches, and the host reads it.
    /// </summary>
    [Fact]
    public void The_usage_names_bringing_a_repository_up_to_date()
    {
        Assert.Contains("| sync [--repository <name>] [--all] [--yes]", DriverCommand.Usage);
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));
        Assert.Contains("case [\"sync\", ..]", program);
        Assert.Contains("args.Contains(\"--all\") ? SyncScope.Everything", program);
        Assert.Contains("| sync [--repository <name>] [--all] [--yes]]", program);
        // WSR7: what was not fetched is said once, first, on the list and on the press alike.
        Assert.Equal(2, program.Split("SyncWords.NotFetched(").Length - 1);
    }

    /// <summary>
    /// PLUGDIST1a: a plugin package is installed from a terminal, a door the usage names, and the host asks for it
    /// before the `plugins` words the kit answers, which would otherwise take it.
    /// </summary>
    [Fact]
    public void The_usage_names_installing_a_plugin_package_and_the_host_routes_it_first()
    {
        Assert.Contains("\n  plugins install <file.nupkg>\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var install = program.IndexOf("if (args is [\"plugins\", \"install\", .. var installArgs])", StringComparison.Ordinal);
        var plugins = program.IndexOf("if (args is [\"plugins\", .. var pluginsArgs])", StringComparison.Ordinal);
        Assert.True(install > 0 && plugins > install, $"install at {install}, the other plugins words at {plugins}");
        Assert.Contains("PluginPackageCommand.Install(installArgs, Console.Out, DaorisHome.Resolve())", program);
    }

    /// <summary>
    /// LAYOUT7 (D117 §6.1): the set-up press's terminal door is named in the usage, routed by the host to its console,
    /// and spoken in the library's words, which <c>SetupPressTests</c> holds.
    /// </summary>
    [Fact]
    public void The_usage_names_the_set_up_press_and_the_host_routes_it_to_the_librarys_words()
    {
        Assert.Contains("\n  setup <repository> [--plan]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.Equal("usage: daoris-driver setup <repository> [--plan]", SetupCommand.Usage);

        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"setup\", .. var setupArgs])", program);
        Assert.Contains("SetupConsole.RunAsync(setupArgs, log)", program);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "SetupConsole.cs"));
        Assert.Contains("SetupCommand.Problem(args)", console);
        Assert.Contains("new SetupWorld(service, home), config, SetupWorld.DoorOf(config, home)", console);
    }

    /// <summary>
    /// WSSETUP6 (D124 §4.5): the workspace plan's terminal door is named in the usage, each form on its own line, and the host's
    /// set-up console hands the words that name a workspace to the library's command, with the real world and this host's log.
    /// </summary>
    [Fact]
    public void The_usage_names_the_workspace_plan_and_the_set_up_console_routes_it_to_the_librarys_words()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  setup --workspace <name> [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]\n", usage);
        Assert.Contains("\n  setup --workspace <name> --pause | --resume | --stop\n", usage);
        Assert.Contains("setup --workspace <name> --pause | --resume | --stop", WorkspaceSetupCommand.Usage);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "SetupConsole.cs"));
        var workspace = console.IndexOf("WorkspaceSetupCommand.Asks(args)", StringComparison.Ordinal);
        Assert.True(workspace >= 0 && workspace < console.IndexOf("SetupCommand.Problem(args)", StringComparison.Ordinal),
            "the set-up console asks whether the words name a workspace before it reads them as one repository's");
        Assert.Contains("WorkspaceSetupCommand.Problem(args)", console);
        Assert.Contains("new WorkspaceSetupWorld(service, home)", console);
        Assert.Contains("SessionLog.WriteSetup(log, line)", console);
    }

    /// <summary>
    /// UPDATE1 (D139 §3, D50): an install's update is a door the usage names, routed by the host to the library's words, which
    /// <c>UpdateCommandTests</c> holds, with this host's log and no service.
    /// </summary>
    [Fact]
    public void The_usage_names_the_update_and_the_host_routes_it_to_the_librarys_words()
    {
        Assert.Contains(
            "\n  update [--install <folder>]  ·  update --when-idle | --now | --cancel [--install <folder>]\n",
            DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.StartsWith("usage: daoris-driver update [--install <folder>]", UpdateCommand.Usage);

        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"update\", .. var updateArgs])", program);
        Assert.Contains("UpdateCommand.Run(", program);
    }

    /// <summary>
    /// WSSETUP5 (D124 §3.1, §4.5): following each line when the person asks is a door the usage names, routed by the host to
    /// its console with this host's log, and spoken in the library's words, which <c>RegisterCommandTests</c> holds.
    /// </summary>
    [Fact]
    public void The_usage_names_registering_from_the_line_and_the_host_routes_it_to_the_librarys_words()
    {
        Assert.Contains("\n  register [--repository <name>]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.Equal("usage: daoris-driver register [--repository <name>]", RegisterCommand.Usage);

        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"register\", .. var registerArgs])", program);
        Assert.Contains("RegisterConsole.RunAsync(registerArgs, log)", program);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "RegisterConsole.cs"));
        Assert.Contains("RegisterCommand.Problem(args)", console);
        Assert.Contains("SessionLog.WriteFollowed(log, followed)", console);
        Assert.Contains("new RegistrationWorld(service, home, config)", console);
    }

    /// <summary>
    /// SESSUX1g (D126 §7.1): Sessions' terminal door is named in the usage, each form on its own line, and routed by the host
    /// to its console with this host's log, where the archive's and the delete's lines say the terminal's door; the words
    /// are the library's, which <c>SessionsCommandTests</c> holds.
    /// </summary>
    [Fact]
    public void The_usage_names_sessions_and_the_host_routes_it_to_the_librarys_words()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  sessions [--group you|review|working|later|ended|archived] [--repository <name>] [--json]\n", usage);
        Assert.Contains("\n  sessions stop <id>  ·  sessions finish <id> [--note \"…\"]  ·  sessions decline <id> --reason \"…\"\n", usage);
        Assert.Contains("\n  sessions archive <id>… | --ended [--yes]  ·  sessions unarchive <id>…  ·  sessions delete <id>\n", usage);
        Assert.Contains("sessions stop <id>", SessionsCommand.Usage);
        // MSG1e (D137 §5.2): what the screen's box says to a session, a terminal says too.
        Assert.Contains("\n  sessions say <id> \"…\" [--file <path>]…\n", usage);
        Assert.Contains("sessions say <id> \"…\" [--file <path>]…", SessionsCommand.Usage);

        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"sessions\", .. var sessionsArgs])", program);
        Assert.Contains("SessionsConsole.RunAsync(sessionsArgs, log)", program);
        // Every loop on the home watches the requests: the headless loop as the desktop's does.
        Assert.Contains("new SessionRequestWatch(home, processes, () => service)", program);
        // MSG1e: the headless loop's own record of each session shows the words it keeps, and its look is nudged to take them up.
        Assert.Contains("Say = new LoopWords(processes, () => service) { Events = events, Nudge = () => watching?.Nudge() }.HoldAsync", program);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "SessionsConsole.cs"));
        Assert.Contains("SessionsCommand.Read(args, out var problem)", console);
        Assert.Contains("SessionsCommand.RunAsync(ask, new SessionsWorld(service, home, config, door, log)", console);
    }

    private static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver.Host")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("the desktop source tree was not found above the test binary");
    }
}
