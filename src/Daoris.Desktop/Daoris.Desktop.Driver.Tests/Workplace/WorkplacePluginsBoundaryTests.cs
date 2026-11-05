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
            ?? JsonSerializer.Serialize(new { id, version = "1.0.0", hooks = new { command = new[] { "node", "hooks.mjs" }, points = points ?? [HookPoints.Perform, HookPoints.State] } }));
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
        Install("acme.lands", points: [HookPoints.Perform]);
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

        Assert.Null(LandingPlugins.ProblemOf("acme.ready", catalog));
        Assert.Null(LandingPlugins.ProblemOf("ACME.Ready", catalog));
        Assert.Null(LandingPlugins.ProblemOf("acme.lands", catalog));
        Assert.Equal(
            "the landing names plugin `acme.absent`, which is not installed on this machine — "
            + "`daoris plugin add <folder>` installs one, and `daoris plugin list` shows what there is.",
            LandingPlugins.ProblemOf("acme.absent", catalog));
        Assert.Equal(
            "plugin `acme.off` is switched off on this machine — `daoris plugin enable acme.off` switches it on.",
            LandingPlugins.ProblemOf("acme.off", catalog));
        Assert.Equal("plugin `acme.broken` contributes nothing: " + broken.Problem, LandingPlugins.ProblemOf("acme.broken", catalog));
        Assert.Equal(
            "plugin `acme.mute` does not open pull requests — its manifest speaks on no `work/perform` point.",
            LandingPlugins.ProblemOf("acme.mute", catalog));
        Assert.Equal(
            "plugin `acme.gate` does not open pull requests — its manifest speaks on no `work/perform` point.",
            LandingPlugins.ProblemOf("acme.gate", catalog));
        Assert.Equal(
            "plugin `acme.states` does not open pull requests — its manifest speaks on no `work/perform` point.",
            LandingPlugins.ProblemOf("acme.states", catalog));
        // The sentence names the id as the rule wrote it, and the switch the id the catalogue holds.
        Assert.Equal(
            "plugin `ACME.OFF` is switched off on this machine — `daoris plugin enable acme.off` switches it on.",
            LandingPlugins.ProblemOf("ACME.OFF", catalog));
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
    public void The_wire_asks_the_homes_catalogue_at_each_call_and_refuses_a_plugin_without_starting_it()
    {
        var started = 0;
        var plugins = new LandingPlugins(_home, start: (_, _, _) => { started++; throw new InvalidOperationException("never started"); }, log: _log);

        Assert.StartsWith("the landing names plugin `acme.ready`, which is not installed", plugins.Problem("acme.ready"));
        Install("acme.ready");
        Assert.Null(plugins.Problem("acme.ready"));
        PluginState.Disable(_home, "acme.ready");
        Assert.Equal("plugin `acme.ready` is switched off on this machine — `daoris plugin enable acme.ready` switches it on.", plugins.Problem("acme.ready"));

        Assert.Equal(0, started);
        // Refused before it is started, a plugin was never spoken to and nothing was written about it.
        Assert.Empty(Lines());
    }

    // ——— The wire, through the trees' context

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
        Assert.StartsWith("the landing names plugin `acme.ready`, which is not installed", plugins.Problem("acme.ready"));
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
    public void The_landing_line_carries_codes_and_counts_alone()
    {
        Assert.Equal(
            ["session", "repository", "workspace", "code", "commits", "uncommitted"],
            LandingLine.Auto("s1", "engine", "aurora", "landed", 1, 0).Data.Select(each => each.Key));
    }

    [Fact]
    public void The_landings_patience_the_states_bounds_and_the_hold_they_bound_keep_their_values()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), LandingPlugins.DefaultPatience);
        Assert.Equal(TimeSpan.FromSeconds(30), LandingPlugins.StatePatience);
        Assert.Equal(TimeSpan.FromSeconds(60), LandingPlugins.StateBound);
        Assert.Equal(TimeSpan.FromMinutes(5), LandingHolds.Wait);
        Assert.Equal(LandingPlugins.StatePatience, PluginKit.Find(HookPoints.State)!.Patience);
    }

    // ——— What a workflow reads of the plugins

    [Fact]
    public void A_workflow_reads_the_plugins_switched_on_and_sound_in_the_catalogues_order_with_their_points()
    {
        var plugins = WorkflowSteps.PluginsOf(Standing());

        Assert.Equal(
            [("acme.gate", [HookPoints.QuestConsider]), ("acme.lands", [HookPoints.Perform]), ("acme.mute", []), ("acme.ready", [HookPoints.Perform, HookPoints.State]), ("acme.states", [HookPoints.State])],
            plugins.Select(plugin => (plugin.Id, (IReadOnlyList<string>)plugin.Points)));
        Assert.Empty(WorkflowSteps.PluginsOf(PluginCatalog.None));
    }

    // ——— Helpers

    private PluginCatalog Catalog() => PluginCatalog.Load(_home, AdapterSet.Built().Names);

    private static LandedBranch Entry(string branch) =>
        new("engine", "aurora", branch, "main", new string('b', 40), "s1", "q1", "Fix", At.AddDays(-1));

    private static StateAsk Ask(LandedBranch entry, string plugin) =>
        new(entry, plugin, new StateFrame(entry.Repository, entry.Workspace, "D:/fam/engine", entry.Branch, "main", null, null));

    private List<LogLine> Lines() =>
        [.. MachineLogReader.Read(DaorisHome.Entry(_home, HomeEntry.Logs), new LogFilter()).Lines];

    private static string? Text(LogLine line, string key) =>
        line.Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? By(LogLine line) => Text(line, "by");

    /// <summary>A plugin's channel, faked: it answers a state ask as handed, and says it was let go.</summary>
    private sealed class Stand(Func<object, PullRequestState>? state = null, Action? disposed = null) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.State];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PullRequestState> StateAsync(object payload, CancellationToken ct) => Task.FromResult(state!(payload));

        public ValueTask DisposeAsync()
        {
            disposed?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
