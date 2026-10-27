using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Plugins cut: the behaviour the cut keeps, written against the surface that exists on both
/// sides of it (the landing's and the state ask's refusal sentences for a plugin that is unknown, switched off, unsound or silent at the
/// point, the plugin wire's land, hand and state ask through the trees' context with a stand-in channel and the machine log's
/// <c>plugin.*</c> lines, the door words, the plugin id's shape and the landing line's use of it, the patience and the hold it bounds,
/// the default wire a session's trees take, and the plugins a workflow reads), so the same cases pass on the base and after it. The
/// seams the cut adds are <c>WorkplacePluginsPortsTests</c>'.
/// </summary>
public sealed class WorkplacePluginsBoundaryTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-plugins", Guid.NewGuid().ToString("N")[..8]);
    private readonly MachineLog _log;

    public WorkplacePluginsBoundaryTests()
    {
        Directory.CreateDirectory(_home);
        _log = new MachineLog(_home, "desktop", () => At);
    }

    public void Dispose()
    {
        _log.Dispose();
        GitSeed.Remove(_home);
    }

    private void Install(string id, string? manifest = null, string[]? points = null)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest
            ?? JsonSerializer.Serialize(new { id, version = "1.0.0", hooks = new { command = new[] { "node", "hooks.mjs" }, points = points ?? [HookPoints.Land, HookPoints.State] } }));
    }

    /// <summary>One of each way a plugin stands: ready at both points, off, unsound, with no hooks and speaking on neither point.</summary>
    private PluginCatalog Standing()
    {
        Install("acme.ready");
        Install("acme.off");
        PluginState.Disable(_home, "acme.off");
        Install("acme.broken", manifest: "{ not json");
        Install("acme.mute", manifest: """{ "id": "acme.mute", "version": "1.0.0" }""");
        Install("acme.gate", points: [HookPoints.QuestConsider]);
        Install("acme.lands", points: [HookPoints.Land]);
        Install("acme.states", points: [HookPoints.State]);
        return PluginCatalog.Load(_home, AdapterSet.Built().Names);
    }

    // ——— The refusal sentences

    [Fact]
    public void A_plugin_that_cannot_land_work_is_refused_in_the_landings_own_sentences()
    {
        var catalog = Standing();
        var broken = Assert.Single(catalog.Plugins, entry => entry.Manifest.Id == "acme.broken");
        Assert.NotNull(broken.Problem);

        Assert.Null(LandingRules.PluginProblem("acme.ready", catalog));
        Assert.Null(LandingRules.PluginProblem("ACME.Ready", catalog));
        Assert.Null(LandingRules.PluginProblem("acme.lands", catalog));
        Assert.Equal(
            "the landing rule names plugin `acme.absent`, which is not installed on this machine — "
            + "`daoris plugin add <folder>` installs one, and `daoris plugin list` shows what there is.",
            LandingRules.PluginProblem("acme.absent", catalog));
        Assert.Equal(
            "plugin `acme.off` is switched off on this machine — `daoris plugin enable acme.off` switches it on.",
            LandingRules.PluginProblem("acme.off", catalog));
        Assert.Equal("plugin `acme.broken` contributes nothing: " + broken.Problem, LandingRules.PluginProblem("acme.broken", catalog));
        Assert.Equal(
            "plugin `acme.mute` does not land work — its manifest speaks on no `work/land` point.",
            LandingRules.PluginProblem("acme.mute", catalog));
        Assert.Equal(
            "plugin `acme.gate` does not land work — its manifest speaks on no `work/land` point.",
            LandingRules.PluginProblem("acme.gate", catalog));
        Assert.Equal(
            "plugin `acme.states` does not land work — its manifest speaks on no `work/land` point.",
            LandingRules.PluginProblem("acme.states", catalog));
        // The sentence names the id as the rule wrote it, and the switch the id the catalogue holds.
        Assert.Equal(
            "plugin `ACME.OFF` is switched off on this machine — `daoris plugin enable acme.off` switches it on.",
            LandingRules.PluginProblem("ACME.OFF", catalog));
    }

    [Fact]
    public void A_plugin_that_cannot_be_asked_for_a_pull_request_is_refused_in_the_state_asks_own_sentences()
    {
        var catalog = Standing();
        var broken = Assert.Single(catalog.Plugins, entry => entry.Manifest.Id == "acme.broken");

        Assert.Null(PullRequestAsking.Problem("acme.ready", catalog));
        Assert.Null(PullRequestAsking.Problem("ACME.STATES", catalog));
        Assert.Equal(
            "plugin `acme.absent`, which pushed the branch, is not installed on this machine, so its pull request was not asked about.",
            PullRequestAsking.Problem("acme.absent", catalog));
        Assert.Equal(
            "plugin `acme.off` is switched off on this machine, so its pull request was not asked about.",
            PullRequestAsking.Problem("acme.off", catalog));
        Assert.Equal("plugin `acme.broken` contributes nothing: " + broken.Problem, PullRequestAsking.Problem("acme.broken", catalog));
        Assert.Equal(
            "plugin `acme.lands` does not answer for a pull request — its manifest speaks on no `work/state` point.",
            PullRequestAsking.Problem("acme.lands", catalog));
        Assert.Equal(
            "plugin `acme.mute` does not answer for a pull request — its manifest speaks on no `work/state` point.",
            PullRequestAsking.Problem("acme.mute", catalog));
    }

    [Fact]
    public void Setting_a_landing_rule_refuses_a_plugin_that_cannot_land_and_writes_nothing()
    {
        var catalog = Standing();
        var config = DriverConfig.Empty;

        var refused = Assert.Throws<DriverException>(() =>
            LandingRules.Put(config, "engine", null, new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "acme.off"), catalog));
        Assert.Equal(
            "plugin `acme.off` is switched off on this machine — `daoris plugin enable acme.off` switches it on. Nothing was written.",
            refused.Message);
        Assert.Contains("which is not installed", Assert.Throws<DriverException>(() =>
            LandingRules.Put(config, null, "aurora", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "acme.absent"), catalog)).Message);
        Assert.Equal("a landing rule is set for a `repository` or a `workspace` — name one of them.",
            Assert.Throws<DriverException>(() => LandingRules.Put(config, "engine", "aurora", null, catalog)).Message);

        var rule = new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "acme.ready");
        Assert.Equal(rule, LandingRules.Put(config, "engine", null, rule, catalog).Landings["engine"]);
        // A rule's shape is judged first, and the catalogue is not asked of a rule that names no plugin.
        Assert.Empty(LandingRules.Put(config, "engine", null, null, catalog).Landings);
    }

    [Fact]
    public void The_wire_asks_the_homes_catalogue_at_each_call_and_refuses_a_plugin_without_starting_it()
    {
        var started = 0;
        var plugins = new LandingPlugins(_home, start: (_, _, _) => { started++; throw new InvalidOperationException("never started"); }, log: _log);

        Assert.StartsWith("the landing rule names plugin `acme.ready`, which is not installed", plugins.Problem("acme.ready"));
        Install("acme.ready");
        Assert.Null(plugins.Problem("acme.ready"));
        PluginState.Disable(_home, "acme.ready");
        Assert.Equal("plugin `acme.ready` is switched off on this machine — `daoris plugin enable acme.ready` switches it on.", plugins.Problem("acme.ready"));

        var landed = plugins.LandAsync("acme.ready", Frame()).GetAwaiter().GetResult();
        var handed = plugins.HandAsync("acme.absent", Frame()).GetAwaiter().GetResult();

        Assert.Equal(new PluginLanding("acme.ready", false, null, plugins.Problem("acme.ready")!, true), landed);
        Assert.Equal(new PluginLanding("acme.absent", false, null, plugins.Problem("acme.absent")!, true), handed);
        Assert.Equal(0, started);
        // Refused before it is started, a plugin was never spoken to and nothing was written about it.
        Assert.Empty(Lines());
    }

    // ——— The wire, through the trees' context

    [Fact]
    public async Task A_land_and_a_hand_through_the_trees_speak_one_frame_say_it_and_log_the_door_each_came_by()
    {
        Install("acme.ready");
        var said = new List<(string Plugin, string Line)>();
        var told = new List<string>();
        var trees = new SessionTrees(_home, new LandingPlugins(_home,
            start: (_, say, _) =>
            {
                say("stderr line");
                return Task.FromResult<IHookChannel>(new Stand(land: payload =>
                {
                    told.Add(JsonSerializer.SerializeToElement(payload).GetProperty("branch").GetString()!);
                    return new PluginLanding("ignored", true, "https://example.test/pull/7", "pushed it.");
                }));
            },
            say: (plugin, line) => said.Add((plugin, line)),
            log: _log));

        var landed = await trees.Context.Plugins.LandAsync("acme.ready", Frame());
        var handed = await trees.Context.Plugins.HandAsync("ACME.READY", Frame());

        Assert.Equal(new PluginLanding("acme.ready", true, "https://example.test/pull/7", "pushed it."), landed);
        Assert.Equal(new PluginLanding("acme.ready", true, "https://example.test/pull/7", "pushed it."), handed);
        Assert.Equal(["feature/q1-fix", "feature/q1-fix"], told);
        Assert.Equal(
            [
                ("acme.ready", "stderr line"),
                ("acme.ready", "landed `feature/q1-fix`: pushed, https://example.test/pull/7 — pushed it."),
                ("acme.ready", "stderr line"),
                ("acme.ready", "landed `feature/q1-fix`: pushed, https://example.test/pull/7 — pushed it."),
            ],
            said);
        var lines = Lines();
        Assert.Equal(
            ["plugin.started", "plugin.called", "plugin.stopped", "plugin.started", "plugin.called", "plugin.stopped"],
            lines.Select(line => line.Event));
        // The call's line names the point and its answer, and no door: the frame's start and stop carry it.
        Assert.Equal(["landing", null, "landing", "hand", null, "hand"], lines.Select(line => By(line)));
        Assert.Equal(("work/land", "pushed"), (Text(lines[1], "point"), Text(lines[1], "answer")));
        Assert.Equal(["plugin", "point", "answer", "ms"], lines[1].Data.EnumerateObject().Select(each => each.Name));
        Assert.Equal(("ended", "landing"), (Text(lines[2], "why"), Text(lines[2], "by")));
    }

    [Fact]
    public async Task A_plugin_that_fails_a_land_comes_back_failed_with_its_sentence_and_is_stopped_through_the_trees()
    {
        Install("acme.ready");
        var disposed = 0;
        var trees = new SessionTrees(_home, new LandingPlugins(_home,
            start: (_, _, _) => Task.FromResult<IHookChannel>(new Stand(
                land: _ => throw PluginFailures.Mark(new DriverException("the platform said no"), PluginEvents.Errored), disposed: () => disposed++)),
            log: _log));

        var landed = await trees.Context.Plugins.LandAsync("acme.ready", Frame());

        Assert.Equal(new PluginLanding("acme.ready", false, null, "the platform said no", true), landed);
        Assert.Equal(1, disposed);
        var failed = Assert.Single(Lines(), line => line.Event == "plugin.failed");
        Assert.Equal(("work/land", "errored", "landing"), (Text(failed, "where"), Text(failed, "kind"), Text(failed, "by")));
        Assert.Equal("plugin.stopped", Lines()[^1].Event);
    }

    [Fact]
    public async Task A_state_ask_through_the_trees_answers_in_the_order_asked_stamps_the_clock_and_logs_the_state_door()
    {
        Install("acme.ready");
        var now = At;
        var trees = new SessionTrees(_home, new LandingPlugins(_home,
            start: (_, _, _) => Task.FromResult<IHookChannel>(new Stand(state: _ => new PullRequestState(PullRequestStates.Open) { Plugin = "ignored" })),
            clock: () => now,
            log: _log));

        Assert.Equal(At, trees.Context.Plugins.Now);
        now = At.AddMinutes(7);
        Assert.Equal(At.AddMinutes(7), trees.Context.Plugins.Now);
        Assert.Empty(await trees.Context.Plugins.AskStatesAsync([]));

        var answers = await trees.Context.Plugins.AskStatesAsync(
        [
            Ask(Entry("feature/a"), "acme.ready"),
            Ask(Entry("feature/b"), "acme.absent"),
            Ask(Entry("feature/c"), "acme.ready"),
        ]);

        Assert.Equal(["feature/a", "feature/b", "feature/c"], answers.Select(answer => answer.Ask.Entry.Branch));
        Assert.Equal(PullRequestStates.Open, answers[0].Answer!.State);
        Assert.Equal("acme.ready", answers[0].Answer!.Plugin);
        Assert.Equal(now, answers[0].AskedAt);
        Assert.Null(answers[1].Answer);
        Assert.Equal(PullRequestCodes.Unready, answers[1].Failure);
        Assert.Equal(PullRequestAsking.Problem("acme.absent", Catalog()), answers[1].Sentence);
        Assert.Equal(PullRequestStates.Open, answers[2].Answer!.State);
        var lines = Lines();
        // One process for the occasion's two entries, started once and stopped after them, by the state door.
        Assert.Equal(["plugin.started", "plugin.called", "plugin.called", "plugin.stopped"], lines.Select(line => line.Event));
        Assert.Equal(["state", null, null, "state"], lines.Select(line => By(line)));
        Assert.Equal(("work/state", "open"), (Text(lines[1], "point"), Text(lines[1], "answer")));
    }

    [Fact]
    public void The_trees_default_wire_is_the_landing_plugins_of_their_home_and_a_supplied_one_is_the_one_they_use()
    {
        var made = new SessionTrees(_home);
        var plugins = Assert.IsType<LandingPlugins>(made.Context.Plugins);
        Assert.StartsWith("the landing rule names plugin `acme.ready`, which is not installed", plugins.Problem("acme.ready"));
        // It reads the home it was made over, at each call.
        Install("acme.ready");
        Assert.Null(made.Context.Plugins.Problem("acme.ready"));
        Assert.Equal(DateTimeOffset.UtcNow, made.Context.Plugins.Now, TimeSpan.FromMinutes(1));
        Assert.NotSame(made.Context.Plugins, new SessionTrees(_home).Context.Plugins);

        var supplied = new LandingPlugins(_home, clock: () => At);
        Assert.Same(supplied, new SessionTrees(_home, supplied).Context.Plugins);
        Assert.Equal(At, new SessionTrees(_home, supplied).Context.Plugins.Now);
    }

    // ——— The words and the numbers

    [Fact]
    public void The_door_words_are_the_literal_bytes_the_history_commands_pass()
    {
        Assert.Equal("screen", PluginEvents.Screen);
        Assert.Equal("terminal", PluginEvents.Terminal);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("acme.lands", true)]
    [InlineData("a-b.c9", true)]
    [InlineData("9a", true)]
    [InlineData("", false)]
    [InlineData("A", false)]
    [InlineData("acme.Lands", false)]
    [InlineData(".a", false)]
    [InlineData("-a", false)]
    [InlineData("a b", false)]
    [InlineData("a_b", false)]
    [InlineData("a\n", false)]
    [InlineData("\na", false)]
    [InlineData("é", false)]
    [InlineData("a/b", false)]
    public void A_plugin_ids_shape_is_one_rule_wherever_it_is_asked(string id, bool is_id)
    {
        Assert.Equal(is_id, PluginCatalog.IsId(id));
        Assert.Equal(is_id, PluginIds.IsId(id));
    }

    [Fact]
    public void The_landing_line_keeps_a_plugin_only_where_it_is_an_id_within_its_bound()
    {
        static object? Plugin(string? id) =>
            LandingLine.Auto("s1", "engine", "aurora", "landed", 1, 0, id, true).Data.Single(each => each.Key == "plugin").Value;

        Assert.Equal("acme.lands", Plugin("acme.lands"));
        Assert.Equal(new string('a', 120), Plugin(new string('a', 120)));
        Assert.Null(Plugin(new string('a', 121)));
        Assert.Null(Plugin("Acme.Lands"));
        Assert.Null(Plugin(".lands"));
        Assert.Null(Plugin("lands\n"));
        Assert.Null(Plugin(""));
        Assert.Null(Plugin(null));
        Assert.Equal(
            ["session", "repository", "workspace", "code", "commits", "uncommitted", "plugin", "pushed"],
            LandingLine.Auto("s1", "engine", "aurora", "landed", 1, 0, "acme.lands", true).Data.Select(each => each.Key));
    }

    [Fact]
    public void The_landings_patience_the_states_bounds_and_the_hold_they_bound_keep_their_values()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), LandingPlugins.DefaultPatience);
        Assert.Equal(TimeSpan.FromSeconds(30), LandingPlugins.StatePatience);
        Assert.Equal(TimeSpan.FromSeconds(60), LandingPlugins.StateBound);
        Assert.Equal(TimeSpan.FromMinutes(5), LandingHolds.Wait);
        Assert.Equal(LandingPlugins.DefaultPatience, PluginKit.Find(HookPoints.Land)!.Patience);
        Assert.Equal(LandingPlugins.StatePatience, PluginKit.Find(HookPoints.State)!.Patience);
    }

    // ——— What a workflow reads of the plugins

    [Fact]
    public void A_workflow_reads_the_plugins_switched_on_and_sound_in_the_catalogues_order_with_their_points()
    {
        var plugins = WorkflowCurrent.PluginsOf(Standing());

        Assert.Equal(
            [("acme.gate", [HookPoints.QuestConsider]), ("acme.lands", [HookPoints.Land]), ("acme.mute", []), ("acme.ready", [HookPoints.Land, HookPoints.State]), ("acme.states", [HookPoints.State])],
            plugins.Select(plugin => (plugin.Id, (IReadOnlyList<string>)plugin.Points)));
        Assert.Empty(WorkflowCurrent.PluginsOf(PluginCatalog.None));
    }

    // ——— Helpers

    private PluginCatalog Catalog() => PluginCatalog.Load(_home, AdapterSet.Built().Names);

    private static LandingFrame Frame() => new("engine", "default", Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-plugins", "absent"), "feature/q1-fix", "main", "Fix", "q1", "s1", []);

    private static LandedBranch Entry(string branch) =>
        new("engine", "aurora", branch, "main", new string('b', 40), "s1", "q1", "Fix", At.AddDays(-1));

    private static StateAsk Ask(LandedBranch entry, string plugin) =>
        new(entry, plugin, new StateFrame(entry.Repository, entry.Workspace, "D:/fam/engine", entry.Branch, "main", null, null));

    private List<LogLine> Lines() =>
        [.. MachineLogReader.Read(DaorisHome.Entry(_home, HomeEntry.Logs), new LogFilter()).Lines];

    private static string? Text(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? By(LogLine line) => Text(line, "by");

    /// <summary>A plugin's channel, faked: it answers a land and a state ask as handed, and says it was let go.</summary>
    private sealed class Stand(Func<object, PluginLanding>? land = null, Func<object, PullRequestState>? state = null, Action? disposed = null) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land, HookPoints.State];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(land!(payload));
        public Task<PullRequestState> StateAsync(object payload, CancellationToken ct) => Task.FromResult(state!(payload));

        public ValueTask DisposeAsync()
        {
            disposed?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
