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
        Assert.Contains("| sync [--repository <name>] [--workspace <name>] [--all] [--yes]", DriverCommand.Usage);
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));
        Assert.Contains("case [\"sync\", ..]", program);
        Assert.Contains("var scope = TreesCommand.Scope(ask);", program);
        Assert.Contains("| sync [--repository <name>] [--workspace <name>] [--all] [--yes]]", program);
        // WSR7: what was not fetched is said once, first, on the list and on the press alike.
        Assert.Equal(2, program.Split("SyncWords.NotFetched(").Length - 1);
    }

    /// <summary>
    /// BRSCOPE1a (D150's BRSCOPE1 note, D50, WSP5): the terminal's clean-up and bringing up to date take one workspace's checkouts
    /// where the words name one, as its Branches tab does, and the usage says so. Each reads its words before the service is
    /// asked, refuses a workspace the registry does not name, and hands its look and its press the checkouts
    /// <see cref="TreesCommand.Take"/> took, which <c>TreesCommandTests</c> holds; the press each list suggests carries it.
    /// </summary>
    [Fact]
    public void The_terminals_clean_up_and_bringing_up_to_date_take_one_workspaces_checkouts()
    {
        Assert.Contains("| clean [--workspace <name>] [--yes]", DriverCommand.Usage);
        Assert.Contains("--workspace takes one workspace's checkouts alone", DriverCommand.Usage);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));
        int Count(string text) => console.Split(text).Length - 1;
        Assert.Contains("| clean [--workspace <name>] [--yes]", console);
        Assert.Equal(2, Count("if (TreesCommand.Read(args, out var problem) is not { } ask) return Usage(problem);"));
        Assert.Equal(2, Count("var taken = TreesCommand.Take(await service.RegistryAsync().ConfigureAwait(false), ask);"));
        Assert.Equal(2, Count("if (taken.Refusal is { } refusal)"));
        Assert.Equal(2, Count("var repositories = taken.Repositories;"));
        Assert.Contains("asking.CleanPlanAsync(repositories, inUse)", console);
        Assert.Contains("trees.CleanAsync(repositories, inUse)", console);
        Assert.Contains("trees.SyncAsync(repositories, inUse, only: null, fetch: true,", console);
        Assert.Equal(2, Count("TreesCommand.Press(ask)"));
        // No look reads the registry's checkouts itself any more: the one rule is the library's.
        Assert.DoesNotContain("args.Contains(\"--yes\")", console);
        Assert.DoesNotContain(".Where(row => !string.IsNullOrWhiteSpace(row.Root))", console);
    }

    /// <summary>
    /// LAND3: a failed or superseded attempt's branch is removed from a terminal by the session or the branch, a door the
    /// usage names and the host reads; the clean-up's list offers it beside such a row, and a landing asks which sessions run.
    /// </summary>
    [Fact]
    public void The_usage_names_removing_a_sessions_branch_by_the_session_or_the_branch()
    {
        Assert.Contains("remove <path|session|branch> [--repository <name>] [--force]", DriverCommand.Usage);
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));
        Assert.Contains("remove <path|session|branch> [--repository <name>] [--force]", program);
        Assert.Contains("RemoveSessionBranchAsync(trees, named, Option(args, \"--repository\"), force)", program);
        Assert.Contains("if (Offered(item) is { } offer)", program);
        Assert.Contains("landing.LandAsync(tree, subject, inUse:", program);
    }

    /// <summary>
    /// LAND3c: the terminal's `trees remove &lt;session|branch&gt;` is the driver's one discard, the call the screen's
    /// <c>DISCARD_SESSION_BRANCH</c> ends in, so it keeps a branch a live session's tree holds as the screen does. The host
    /// composes none of its pieces itself: no layout read, no removal by branch. <c>SessionBranchDiscardTests</c> holds the act.
    /// </summary>
    [Fact]
    public void The_terminals_branch_removal_is_the_drivers_one_discard()
    {
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));

        Assert.Contains("new SessionBranchDiscard(trees).DiscardNamedAsync(service, named, repository, force)", program);
        Assert.DoesNotContain("RemoveBranchAsync(", program);
        Assert.DoesNotContain("BranchOfTree(", program);
        Assert.DoesNotContain("FindBranches(", program);
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
        // MSG1f3 (D50, D137 §2.2): *Start a conversation with these words* at a terminal opens the terminal's own conversation,
        // as `chat` does, through the chat runner's one act the screen's `SESSION_START_FROM` calls, on the machine's adapter.
        Assert.Contains("\n  sessions start-from <id>\n", usage);
        Assert.Contains("StartFrom = (id, _) => ChatConsole.StartFromAsync(id)", console);
        var chat = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "ChatConsole.cs"));
        Assert.Contains("runner.StartFromAsync(session, config.Adapter, config, onEnded", chat);
    }

    /// <summary>
    /// EVID1b (D144 §5, D50): <c>quest check</c> is a door the usage names, routed by the host to its console inside the one
    /// catch with this host's log, where the read's <c>evidence.checked</c> line goes; asked for before the quest's other words,
    /// which would take it as a usage mistake. The words are the library's, which <c>QuestCheckCommandTests</c> holds.
    /// </summary>
    [Fact]
    public void The_usage_names_the_quest_check_and_the_host_routes_it_with_its_log()
    {
        Assert.Contains("\n  quest check <id> [--commit <sha>]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));

        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var log = program.IndexOf("using var log = MachineLog.Open(", StringComparison.Ordinal);
        var check = program.IndexOf("QuestCheckCommand.Asks(questArgs)", StringComparison.Ordinal);
        var questUsage = program.IndexOf("usage: daoris-driver quest delete <id>", StringComparison.Ordinal);
        Assert.True(check > log && questUsage > check, $"`quest check` asked for at {check}, the log opened at {log}, the quest's usage at {questUsage}");
        Assert.Contains("QuestCheckConsole.RunAsync(questArgs, log)", program);
        Assert.Contains("quest check <id> [--commit <sha>]", program[questUsage..]);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "QuestCheckConsole.cs"));
        Assert.Contains("QuestCheckCommand.Read(args, out var problem)", console);
        Assert.Contains("service.EvidenceLined += line => SessionLog.WriteEvidence(log, line);", console);
        Assert.Contains("QuestCheckCommand.RunAsync(ask, new QuestCheckWorld(service), Console.Out)", console);
    }

    /// <summary>
    /// QUESTCLOSE1 (D126's note, D50): <c>quest done</c> is the person's done at a terminal, the quest page's <i>Mark done…</i>
    /// beside it, routed by the host to the library's words before the quest's other words, which would take it as a usage
    /// mistake. The words are the library's, which <c>QuestDoneCommandTests</c> holds.
    /// </summary>
    [Fact]
    public void The_host_routes_the_persons_done_to_the_librarys_words()
    {
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var done = program.IndexOf("QuestDoneCommand.Asks(questArgs)", StringComparison.Ordinal);
        var questUsage = program.IndexOf("usage: daoris-driver quest delete <id>", StringComparison.Ordinal);
        Assert.True(done > 0 && questUsage > done, $"`quest done` asked for at {done}, the quest's usage at {questUsage}");
        Assert.Contains("QuestDoneCommand.Read(questArgs, out var doneProblem)", program);
        Assert.Contains("QuestDoneCommand.RunAsync(doneAsk, doneClient, Console.Out)", program);
        Assert.Contains("quest done <id> [--note \\\"…\\\"]", program[questUsage..]);
    }

    /// <summary>
    /// HIST1d (D153 point 7, the history-clearing design §6.2, D50): the history verbs are doors the usage names, routed by the
    /// host to its console inside the one catch, with this host's log, where a clear's line says the terminal's door. <c>quest
    /// clear</c> and <c>ask --clear</c> are asked for before the quest's other words and an ask's words, which would take them as
    /// a usage mistake and as the words of a new ask. The words are the library's, which <c>HistoryCommandTests</c> holds.
    /// </summary>
    [Fact]
    public void The_usage_names_the_history_verbs_and_the_host_routes_them_to_the_librarys_words()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  history [--workspace <name>] [--json]\n", usage);
        Assert.Contains("\n  history clear --workspace <name> [--yes]\n", usage);
        Assert.Contains("\n  quest clear <id> [--failed] [--yes]  ·  ask --clear <id> [--yes]\n", usage);

        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var history = program.IndexOf("if (args is [\"history\", .. var historyArgs])", StringComparison.Ordinal);
        var log = program.IndexOf("using var log = MachineLog.Open(", StringComparison.Ordinal);
        Assert.True(history > log, $"the history verbs routed at {history}, inside the catch after the machine log opened at {log}");
        Assert.Contains("HistoryConsole.RunAsync(historyArgs, log)", program);
        var askClear = program.IndexOf("HistoryCommand.Asks(WorkScope.Ask, clearArgs)", StringComparison.Ordinal);
        var askWords = program.IndexOf("AskConsole.RunAsync(askArgs)", StringComparison.Ordinal);
        Assert.True(askClear > 0 && askWords > askClear, $"`ask --clear` asked for at {askClear}, an ask's words at {askWords}");
        Assert.Contains("HistoryConsole.RunAsync(WorkScope.Ask, clearArgs, log)", program);
        var questClear = program.IndexOf("HistoryCommand.Asks(WorkScope.Quest, questArgs)", StringComparison.Ordinal);
        var questUsage = program.IndexOf("usage: daoris-driver quest delete <id>", StringComparison.Ordinal);
        Assert.True(questClear > 0 && questUsage > questClear, $"`quest clear` asked for at {questClear}, the quest's usage at {questUsage}");
        Assert.Contains("HistoryConsole.RunAsync(WorkScope.Quest, questArgs, log)", program);
        Assert.Contains("quest clear <id> [--failed] [--yes]", program[questUsage..]);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "HistoryConsole.cs"));
        Assert.Contains("HistoryCommand.Read(args, out var problem)", console);
        Assert.Contains("HistoryCommand.Read(scope, args, out var problem)", console);
        Assert.Contains("Console.Error.WriteLine(HistoryCommand.Usage);", console);
        Assert.Contains("new HistoryWorld(service, home, configPath, new SessionProcesses(Path.Combine(home, \"sessions\"))) { Log = log }", console);
        Assert.Contains("HistoryCommand.RunAsync(ask, world, Console.Out)", console);
    }

    /// <summary>
    /// TRACE1 (D143, D50): one read back to the ask is a door the usage names, routed by the host to its console before the
    /// machine log opens, since that open prunes old files and the trace writes nothing anywhere. The words are the library's,
    /// which <c>TraceTests</c> holds.
    /// </summary>
    [Fact]
    public void The_usage_names_the_trace_and_the_host_routes_it_before_anything_is_written()
    {
        Assert.Contains("\n  trace <commit|session|quest>  ·  trace commit|session|quest <id>\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.StartsWith("usage: daoris-driver trace <commit|session|quest>  ·  trace commit|session|quest <id>", TraceCommand.Usage);

        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var trace = program.IndexOf("if (args is [\"trace\", .. var traceArgs])", StringComparison.Ordinal);
        var log = program.IndexOf("using var log = MachineLog.Open(", StringComparison.Ordinal);
        Assert.True(trace > 0 && log > trace, $"the trace routed at {trace}, the machine log opened at {log}");
        Assert.Contains("TraceConsole.RunAsync(traceArgs)", program);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TraceConsole.cs"));
        Assert.Contains("TraceCommand.Read(args, out var problem)", console);
        Assert.Contains("TraceCommand.RunAsync(", console);
        Assert.DoesNotContain("MachineLog", console);
    }

    /// <summary>
    /// GIT1c (D147 §3.3, D50): the branch list is a door the usage names, routed by the host to its console before the machine
    /// log opens, since that open prunes old files and the list writes nothing anywhere. The words are the library's, which
    /// <c>GitBranchesCommandTests</c> holds.
    /// </summary>
    [Fact]
    public void The_usage_names_the_branch_list_and_the_host_routes_it_before_anything_is_written()
    {
        Assert.Contains("\n  git branches [--repository <name>] [--all] [--json]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.StartsWith("usage: daoris-driver git branches [--repository <name>] [--all] [--json]", GitBranchesCommand.Usage);

        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var git = program.IndexOf("if (args is [\"git\", .. var gitArgs])", StringComparison.Ordinal);
        var log = program.IndexOf("using var log = MachineLog.Open(", StringComparison.Ordinal);
        Assert.True(git > 0 && log > git, $"the branch list routed at {git}, the machine log opened at {log}");
        Assert.Contains("GitConsole.RunAsync(gitArgs)", program);

        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "GitConsole.cs"));
        Assert.Contains("GitBranchesCommand.Read(args, out var problem)", console);
        Assert.Contains("GitBranchesCommand.RunAsync(", console);
        Assert.DoesNotContain("MachineLog", console);
    }

    /// <summary>
    /// PLUGHOOK1c (D148 point 2, design §2.1 occasion 4, D50): <i>Ask again</i> from a terminal is a <c>trees</c> verb the usage
    /// names, the host reads and routes to the library's ask, whose words the library says (<c>PullRequestWordsTests</c>). The
    /// plugin's lines are said under its name, and its frames written to the machine log, as a landing's are. The clean-up's and
    /// bringing up to date's looks, which ask too, say and write theirs the same way.
    /// </summary>
    [Fact]
    public void The_usage_names_asking_a_landed_branchs_plugin_again_and_the_host_routes_it()
    {
        Assert.Contains("| state <session|branch> [--repository <name>]", DriverCommand.Usage);
        var console = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "TreesConsole.cs"));
        Assert.Contains("case [\"state\", var named, ..]", console);
        Assert.Contains("| state <session|branch> [--repository <name>]", console);
        Assert.Contains("asking.AskAgainAsync(root, entry)", console);
        Assert.Contains("PullRequestWords.AskedAgain(again)", console);
        Assert.Contains("asking.CleanPlanAsync(repositories, inUse)", console);
        Assert.Contains("asking.SyncPlanAsync(repositories, inUse, fetch: true, scope: scope)", console);
        Assert.Contains("new LandingPlugins(home, say: (id, line) => Console.WriteLine($\"  plugin:{id}  {line}\"), log: log)", console);
    }

    // ——— DEV3b (D115's DEV3a note): a loop the host runs ends on a cancellation two ways, and only the person's close is a close.

    /// <summary>
    /// 🔴 The host read every cancellation as Ctrl+C, so a look whose own request met the client's timeout printed
    /// <i>driver: stopped.</i> and exited 0. A close is the person's: the timeout is a failure that names itself, exit 2, and a
    /// cancellation nothing asked for is one too.
    /// </summary>
    [Fact]
    public void A_cancellation_is_a_close_only_where_the_person_closed_the_run()
    {
        var timeout = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", new TimeoutException());
        var other = new OperationCanceledException("The operation was canceled.");

        Assert.Equal(new LoopEnded(0, "driver: stopped.", Failed: false), DriverCommand.Cancelled(timeout, closed: true));
        Assert.Equal(new LoopEnded(0, "driver: stopped.", Failed: false), DriverCommand.Cancelled(other, closed: true));
        Assert.Equal(
            new LoopEnded(2, "driver: the service did not answer in time — The request was canceled due to the configured "
                + "HttpClient.Timeout of 100 seconds elapsing.", Failed: true),
            DriverCommand.Cancelled(timeout, closed: false));
        Assert.Equal(
            new LoopEnded(2, "driver: a request was cancelled though nothing closed the run — The operation was canceled.", Failed: true),
            DriverCommand.Cancelled(other, closed: false));
    }

    /// <summary>The host hands every cancellation to that reading, with whether its own close was asked for, and prints its answer.</summary>
    [Fact]
    public void The_host_reads_a_cancellation_by_whether_its_close_was_asked_for()
    {
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));

        Assert.Contains("catch (OperationCanceledException cancelled)", program);
        Assert.Contains("DriverCommand.Cancelled(cancelled, closing.IsCancellationRequested)", program);
        Assert.Contains("(ending.Failed ? Console.Error : Console.Out).WriteLine(ending.Said);", program);
        Assert.Contains("return ending.Exit;", program);
        Assert.DoesNotContain("Console.WriteLine(\"driver: stopped.\");", program);
    }

    /// <summary>
    /// DEV3c: the host's watch is handed its console as the door for a part of a look, so a look that stopped a session and then
    /// failed prints the stop before the failure ends the loop. Without it the watch carries the part to a next look that a
    /// failure here never reaches.
    /// </summary>
    [Fact]
    public void The_hosts_watch_prints_what_a_failed_look_had_said()
    {
        var program = File.ReadAllText(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host", "Program.cs"));
        var watch = program.IndexOf("await watching.RunAsync(", StringComparison.Ordinal);

        Assert.True(watch > 0, "the host runs its watch");
        Assert.Contains(
            "onError: null,\n            closing.Token,\n            said: report => Print(report));",
            program[watch..].ReplaceLineEndings("\n"));
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
