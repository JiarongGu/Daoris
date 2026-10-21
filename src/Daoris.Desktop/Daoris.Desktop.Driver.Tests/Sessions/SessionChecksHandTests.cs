using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1b (D192 point 5; the pipeline design §3.2's first and third layers): what a driven session is told and handed under its
/// run's checks. Under <c>light</c> its words name the light gates and the heavy gates as the verification's; where its agent takes
/// Daoris's rules (Claude Code) each heavy gate's declared command is denied exactly, never as a prefix, and auto mode's classifier is told
/// one sentence; where it takes none (Codex, dsh) nothing is handed and its words say so. Under <c>heavy-at-end</c> its words are the
/// heavy gates once, at its end, as the pipeline's; with nothing set, today's. Reads and writes files only, so the fast half's.
/// </summary>
public sealed class SessionChecksHandTests : IDisposable
{
    private const string Light =
        "This repository's development pipeline is light. While you work, run its light gates (`lint`, `unit`) and the checks your change "
        + "reaches, filtered: a test class, a test file, a census. Its heavy gates (`build`, `e2e`) are not this session's: they run in "
        + "the repository's verification after the work lands. Never run them, whole or another way: `daoris-driver run-heavy` refuses "
        + "this tree, and your harness refuses each one's declared command.";

    private const string LightSaid =
        "This repository's development pipeline is light. While you work, run its light gates (`lint`, `unit`) and the checks your change "
        + "reaches, filtered: a test class, a test file, a census. Its heavy gates (`build`, `e2e`) are not this session's: they run in "
        + "the repository's verification after the work lands. Never run them, whole or another way: `daoris-driver run-heavy` refuses "
        + "this tree. Your agent takes no rules from Daoris, so nothing refuses their commands in your shell: they are said to you instead.";

    private const string AtEnd =
        "This repository's development pipeline runs its heavy gates once, at a session's end. While you work, run the checks that cover "
        + "what you change. Once, when the work is done, run its heavy gates (`build`, `e2e`) through `daoris-driver run-heavy`, from this "
        + "tree's root: it waits its turn behind another session's run of this repository rather than running beside it. Never run those "
        + "gates whole another way.";

    private readonly HeavyFixture _fixture = new("session-checks-hand");

    public SessionChecksHandTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    public void Dispose() => _fixture.Dispose();

    private string Install => _fixture.At("install");

    private string Home => Path.Combine(Install, DaorisHome.InstallData);

    private void Launcher()
    {
        var bin = Path.Combine([Install, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "daoris-driver.cmd"), "");
    }

    private void Marked() =>
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("build", "dotnet build", true), ("unit", "npm test", false), ("e2e", "npm run e2e", true));

    /// <summary>
    /// Under light a session is told its light gates and its heavy gates as the verification's, whether or not its PATH finds the verb,
    /// and where its agent takes rules it is handed each heavy gate's command denied exactly and the classifier's sentence.
    /// </summary>
    [Fact]
    public void Under_light_the_words_name_the_light_gates_and_the_heavy_ones_as_the_verification_s_and_deny_each_command()
    {
        Marked();

        var handed = HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.Light, takesRules: true);

        Assert.Equal(Light, handed.Words);
        Assert.Equal(["Bash(dotnet build)", "Bash(npm run e2e)"], handed.Deny);
        Assert.Equal(
            "Running this repository's heavy gates (`build`, `e2e`) whole, in this session: under its light development pipeline they run in "
            + "its verification after the work lands. A check filtered to a test class, a test file or a census is not one of them.",
            handed.HardDeny);
    }

    /// <summary>Where the agent takes no rules, the words say the gates are said rather than refused, and nothing is handed to refuse them.</summary>
    [Fact]
    public void Under_light_an_agent_that_takes_no_rules_is_told_so_and_handed_nothing()
    {
        Marked();

        var handed = HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.Light, takesRules: false);

        Assert.Equal(LightSaid, handed.Words);
        Assert.Empty(handed.Deny);
        Assert.Null(handed.HardDeny);
    }

    /// <summary>A repository that marks no heavy gate, and has no mapping here, gives nothing to the verification: nothing said or refused.</summary>
    [Fact]
    public void Under_light_a_repository_that_marks_no_heavy_gate_is_told_nothing_and_refused_nothing()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null));

        var handed = HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.Light, takesRules: true);

        Assert.Null(handed.Words);
        Assert.Empty(handed.Deny);
        Assert.Null(handed.HardDeny);
    }

    /// <summary>
    /// 🔴 Only declared commands, exactly, never as a prefix (the pipeline design §3.2's third layer): a filtered check that begins with a
    /// heavy gate's command passes; a command a light gate also runs is never refused; one a rule cannot name exactly (a wildcard, a line
    /// break) is left to the words and the classifier.
    /// </summary>
    [Fact]
    public void Each_heavy_command_is_denied_exactly_and_a_filtered_check_passes()
    {
        _fixture.Gates(
            _fixture.Checkout,
            ("universal", "node tools/check.mjs", null), ("service", "dotnet test src/Service", true), ("again", "node tools/check.mjs", true),
            ("glob", "npm test -- src/*", true), ("lines", "npm test\nnpm run e2e", true));

        var deny = HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.Light, takesRules: true).Deny;

        Assert.Equal(["Bash(dotnet test src/Service)"], deny);
        Assert.Contains(deny, rule => BashRule.Matches(rule, "dotnet test src/Service"));
        Assert.DoesNotContain(deny, rule => BashRule.Matches(rule, "dotnet test src/Service --filter FullyQualifiedName~Landing"));
        Assert.DoesNotContain(deny, rule => BashRule.Matches(rule, "node tools/check.mjs"));
    }

    /// <summary>A repository that marks none takes this machine's mapping as its heavy gates, and every gate its file declares is light.</summary>
    [Fact]
    public void Under_light_this_machine_s_mapping_is_the_heavy_gates_and_the_file_s_are_light()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("unit", "npm test", null));
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.DriverConfig), """{"heavyGates":{"engine":[{"name":"build","run":"dotnet build"},{"name":"e2e","run":"npm run e2e"}]}}""");

        var handed = HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.Light, takesRules: true);

        Assert.Equal(Light, handed.Words);
        Assert.Equal(["Bash(dotnet build)", "Bash(npm run e2e)"], handed.Deny);
    }

    /// <summary>Under heavy-at-end the heavy gates are said once, at the end, as the pipeline's, where the verb is found; nothing is denied.</summary>
    [Fact]
    public void Under_heavy_at_end_the_heavy_gates_are_said_once_at_the_end_as_the_pipeline_s()
    {
        Marked();
        Assert.Null(HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.HeavyAtEnd, takesRules: true).Words);

        Launcher();
        var handed = HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.HeavyAtEnd, takesRules: true);

        Assert.Equal(AtEnd, handed.Words);
        Assert.Empty(handed.Deny);
        Assert.Null(handed.HardDeny);
    }

    /// <summary>Nothing set is today's: the words HEAVYRUN1b1 says, and nothing denied.</summary>
    [Fact]
    public void Nothing_set_is_today_s_words_and_nothing_denied()
    {
        Marked();
        Launcher();

        var handed = HeavyTold.Hand(_fixture.Checkout, Home, "engine", checks: null, takesRules: true);

        Assert.Equal(HeavyTold.For(_fixture.Checkout, Home, "engine"), handed.Words);
        Assert.Equal(HeavyTold.Words(["build", "e2e"]), handed.Words);
        Assert.Empty(handed.Deny);
        Assert.Null(handed.HardDeny);
    }

    /// <summary>
    /// The spawned settings file of a Claude Code session under light names each heavy gate's command in its deny list and tells the
    /// classifier, beside <c>no-push</c>; a Codex session's start writes no file at all (the pipeline design §7's 1b proof).
    /// </summary>
    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void A_claude_session_under_light_is_handed_each_heavy_command_denied_and_the_classifier_told(string name)
    {
        Marked();
        var adapters = AdapterSet.Built();
        // The roster reads this test's own harness file, never the one DAORIS_HOME names (the live install's, where it is set).
        var driver = new Daoris.Driver.Driver(new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty, adapters, Home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)));
        var adapter = adapters.Resolve(name);
        var handed = HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.Light, adapter.TakesSettings);
        var info = adapter.Prepare(Target(_fixture.Checkout), ["claude"]);

        var (file, _, _) = driver.HandRules(adapter, info, "s-light", null, "engine", _fixture.Checkout, kept: null, heavy: handed);

        var settings = JsonNode.Parse(File.ReadAllText(file!))!;
        var deny = settings["permissions"]!["deny"]!.AsArray().Select(rule => rule!.GetValue<string>()).ToList();
        Assert.Contains("Bash(dotnet build)", deny);
        Assert.Contains("Bash(npm run e2e)", deny);
        Assert.Contains("Bash(git push:*)", deny);
        var told = settings["autoMode"]!["hard_deny"]!.AsArray().Select(entry => entry!.GetValue<string>()).ToList();
        Assert.Equal("$defaults", told[0]);
        Assert.Contains(handed.HardDeny!, told);
        Assert.Contains(told, entry => entry.Contains("git -C <dir> push", StringComparison.Ordinal));
    }

    [Fact]
    public void A_codex_session_under_light_is_handed_no_rules_file()
    {
        Marked();
        var adapters = AdapterSet.Built();
        // The roster reads this test's own harness file, never the one DAORIS_HOME names (the live install's, where it is set).
        var driver = new Daoris.Driver.Driver(new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty, adapters, Home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)));
        var adapter = adapters.Resolve("codex-acp");
        var handed = HeavyTold.Hand(_fixture.Checkout, Home, "engine", SessionChecks.Light, adapter.TakesSettings);
        var info = adapter.Prepare(Target(_fixture.Checkout), ["codex"]);

        var (file, meta, _) = driver.HandRules(adapter, info, "s-codex", null, "engine", _fixture.Checkout, kept: null, heavy: handed);

        Assert.Null(file);
        Assert.Null(meta);
        Assert.Equal(LightSaid, handed.Words);
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.Spawn)) && Directory.EnumerateFiles(DaorisHome.Entry(Home, HomeEntry.Spawn)).Any());
    }

    /// <summary>
    /// The checks a session's run takes are kept where <c>run-heavy</c> reads them by the session its tree's lock names, beside the
    /// session's other spawn files, and go with them; nothing set keeps nothing.
    /// </summary>
    [Fact]
    public void A_session_s_checks_are_kept_by_its_id_and_let_go()
    {
        Assert.Null(SessionChecks.Hold(Home, "s-1", null));
        Assert.Null(SessionChecks.Held(Home, "s-1"));

        var kept = SessionChecks.Hold(Home, "s-1", SessionChecks.Light);

        Assert.Equal(DaorisHome.Entry(Home, HomeEntry.Spawn, "s-1" + SessionChecks.Suffix), kept);
        Assert.Equal(SessionChecks.Light, SessionChecks.Held(Home, "s-1"));
        Assert.Null(SessionChecks.Held(Home, "s-2"));
        Assert.Contains(SessionChecks.Suffix, SessionHomeFiles.SpawnSuffixes);

        SessionChecks.LetGo(kept);
        Assert.Null(SessionChecks.Held(Home, "s-1"));
        SessionChecks.LetGo(kept);
    }

    /// <summary>
    /// The run's checks: under Current the repository's rule; a named workflow's run its bound version's <c>work</c> step's
    /// (FABRIC-PIPELINE1b2), never the rule, so a version that sets none is today's and one that sets them holds where the rule is none.
    /// </summary>
    [Fact]
    public void A_run_takes_the_repository_s_rule_under_current_and_its_version_s_under_a_named_workflow()
    {
        var config = DriverConfig.Empty.WithSessionChecks("engine", SessionChecks.Light);
        var current = WorkflowProcess.Current(config, "engine", null);
        var named = current with { Binding = new WorkflowRunBinding("q1", "engine", "default", "docs", "repository", DateTimeOffset.UnixEpoch) { Version = 1 } };
        NamedStep Work(string? checks) => new("work", WorkflowKinds.Work, [new("checks", checks)]);

        Assert.Equal(SessionChecks.Light, SessionChecks.Of(config, "engine", null));
        Assert.Equal(SessionChecks.Light, SessionChecks.Of(config, "engine", current));
        Assert.Null(SessionChecks.Of(config, "engine", named));
        Assert.Null(SessionChecks.Of(config, "engine", named with { Steps = [Work(null)] }));
        Assert.Equal(SessionChecks.HeavyAtEnd, SessionChecks.Of(config, "engine", named with { Steps = [Work(SessionChecks.HeavyAtEnd)] }));
        Assert.Equal(SessionChecks.Light, SessionChecks.Of(DriverConfig.Empty, "engine", named with { Steps = [Work(SessionChecks.Light)] }));
        Assert.Null(SessionChecks.Of(config, "shop", current));
    }

    /// <summary>
    /// FABRIC-PIPELINE1b3 (D192's FABRIC-PIPELINE1b2 note, point 5): a run bound to a graph version takes its <c>claimed</c> node's
    /// <c>checks</c>, read from the version as its file holds it, never the repository's rule, and its <c>callMinutes</c> beside them. Read
    /// as written: a value no choice, or a bound that is no whole number of minutes from one, is none; and a version that is not the one its
    /// run bound gives nothing.
    /// </summary>
    [Fact]
    public void A_graph_run_takes_its_claimed_checks_and_call_minutes_as_written()
    {
        var rule = DriverConfig.Empty.WithSessionChecks("engine", SessionChecks.Light);
        WorkflowProcess Bound(string run, string? options, string? digest = null)
        {
            var saved = RunHome.SaveGraph(Home, ClaimedGraph(options), workflow: run);
            RunHome.Bind(Home, run, digest ?? saved, DateTimeOffset.UnixEpoch, workflow: run);
            return WorkflowProcesses.Read(Home, rule, "engine", null, run);
        }

        var light = Bound("light", """{"checks":"light","callMinutes":30}""");
        Assert.True(light.Named);
        Assert.Equal(SessionChecks.Light, SessionChecks.Of(rule, "engine", light));
        Assert.Equal(30, LongCalls.MinutesOf(light));

        var atEnd = Bound("at-end", """{"checks":"heavy-at-end"}""");
        Assert.Equal(SessionChecks.HeavyAtEnd, SessionChecks.Of(rule, "engine", atEnd));
        Assert.Null(LongCalls.MinutesOf(atEnd));

        // A graph that sets none is today's, never Current's rule.
        Assert.Null(SessionChecks.Of(rule, "engine", Bound("none", options: null)));

        var spelled = Bound("spelled", """{"checks":"Light","callMinutes":"30"}""");
        Assert.Null(SessionChecks.Of(rule, "engine", spelled));
        Assert.Null(LongCalls.MinutesOf(spelled));
        Assert.Null(LongCalls.MinutesOf(Bound("zero", """{"checks":"light","callMinutes":0}""")));
        Assert.Null(LongCalls.MinutesOf(Bound("part", """{"checks":"light","callMinutes":2.5}""")));

        // Bound at a digest the file's version is not: not the version its run started on, so nothing of it is taken.
        var other = Bound("other", """{"checks":"light","callMinutes":30}""", digest: "gph-00000000000000000000000000000000");
        Assert.Null(SessionChecks.Of(rule, "engine", other));
        Assert.Null(LongCalls.MinutesOf(other));
    }

    /// <summary>The smallest development graph: <c>claimed</c>, with <paramref name="options"/> where set, then <c>handed</c>.</summary>
    private static string ClaimedGraph(string? options) =>
        $$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"{{(options is null ? "" : $",\"options\":{options}")}}},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    /// <summary>
    /// A run's <c>callMinutes</c> is kept beside its checks, by its session's id, where the long-call watch reads it; only where set, so the
    /// file holds what it held before for every run that sets none.
    /// </summary>
    [Fact]
    public void A_session_s_call_minutes_are_kept_beside_its_checks_only_where_set()
    {
        var kept = SessionChecks.Hold(Home, "s-1", SessionChecks.Light, callMinutes: 30);

        Assert.Equal("""{"checks":"light","callMinutes":30}""", JsonNode.Parse(File.ReadAllText(kept!))!.ToJsonString());
        Assert.Equal((SessionChecks.Light, (int?)30), (SessionChecks.Held(Home, "s-1"), SessionChecks.HeldMinutes(Home, "s-1")));

        SessionChecks.Hold(Home, "s-2", SessionChecks.Light);
        Assert.Null(SessionChecks.HeldMinutes(Home, "s-2"));
        Assert.Null(SessionChecks.Hold(Home, "s-3", null, callMinutes: 30));
        Assert.Null(SessionChecks.HeldMinutes(Home, "s-3"));
    }

    private static SessionTarget Target(string root) => new(
        QuestId: "q1", Title: "Serve the report", Body: "The report needs a port.", Asker: "ask #a1", Repository: "engine", Root: root,
        ServiceUrl: "http://127.0.0.1:9");
}
