using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The fixture the automatic landing's tests share (PROCSPLIT1): one scratch repository home, a stand-in for the service's quests and
/// records, and a plugin faked on the wire's channel, built per test instance. It was the whole of one class, which xUnit ran one test
/// after another; the classes that derive from it hold the tests by concern, so the git half's runner runs them beside each other.
/// </summary>
/// <remarks>
/// Nothing here is shared between instances: the scratch folder carries a fresh id, and <c>LandingHolds</c>' gates are keyed by the
/// home inside it, so two classes running together never meet at a hold or a folder.
/// </remarks>
public abstract class AutoLandingFixture : IDisposable
{
    protected const string Plugin = "example.lands";

    protected readonly string _scratch;
    protected readonly string _home;
    protected readonly World _world = new();
    protected readonly List<object> _frames = [];
    protected readonly List<LandingLine> _lines = [];

    protected AutoLandingFixture()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "auto-landing", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// A named workflow `docs-to-pr` saved here with one version and chosen as `engine`'s default (or, with a kind, the workspace's for
    /// it), and run q1 bound to it as its first start binds it (WORKFLOW1e), over the rule <see cref="Rule"/> wrote.
    /// </summary>
    protected void Follow(string steps, string? kind = null)
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(
            "docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", System.Text.Json.Nodes.JsonNode.Parse(steps)), kept: []);
        var use = kind is null
            ? new WorkflowUse("docs-to-pr", "engine", null, null)
            : new WorkflowUse("docs-to-pr", null, "aurora", kind);
        WorkflowSelection.Apply(Config(), use).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var task = kind is null ? null : new WorkflowTaskChoice(kind, null);
        Assert.True(WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(Config(), _home, "q1", "engine", "aurora", null, task, [], DateTimeOffset.UtcNow)));
    }

    /// <summary>A done session in its own tree, concluded as the driver concludes one: its run's process read, then the switch.</summary>
    protected async Task<string> NamedDoneAsync(string root, string session, string quest)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        _world.Quests[quest] = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", "Done");
        _world.Records.Add(new SessionRecord(session, "engine", "completed") { Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        var process = await AutoLander.ProcessAsync(_home, Config(), _world, _world.Quests[quest], opened.Path, "aurora");
        Assert.True(process!.Named);
        Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session,
            _world.Quests[quest], "Done", "completed", opened.Path, "aurora", process: process));
        return opened.Path;
    }

    /// <summary>The quest a session serves, as the service answers it from its records (<c>ServiceClient.SessionQuestAsync</c>).</summary>
    protected Task<string?> SessionQuestAsync(string id, CancellationToken ct) =>
        Task.FromResult(_world.Records.FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase))?.Quest);

    /// <summary>
    /// The person's press as its doors make it (FABRIC-CLOSE1h3): the quest read where the door names its landing, then the landing
    /// judged again under its holds on that read.
    /// </summary>
    protected async Task<TreeLanding> PressAsync(string tree, string? quest = "q1")
    {
        var reads = new PressQuests(_world.QuestAsync);
        if (quest is not null) await reads.FindAsync(quest);
        var pressed = await reads.PressedAsync("s1", quest);
        return await new SessionTrees(_home, Plugins()).LandAsync(
            tree, new LandingSubject("s1", quest, "Fix the gap"), judged: pressed?.Judged, beforePush: pressed?.BeforePush);
    }

    protected const string PushBranch = "feature/q1-fix-the-gap";

    /// <summary>
    /// One look's landing of the due entries it chose (one, unless <paramref name="choices"/> says more), whose push waits behind another
    /// push of its branch, held here, while <paramref name="quest"/> moves as <paramref name="change"/> says; then the branch's hold is let
    /// go and the landing ends. What the landing said comes back.
    /// </summary>
    protected Task<IReadOnlyList<string>> HeldAtThePushAsync(
        string root, Func<QuestView, QuestView> change, string quest = "q1", int choices = 1) =>
        HeldAtThePushAsync(root, () => _world.Quests[quest] = change(_world.Quests[quest]), choices);

    /// <summary>
    /// The same, while <paramref name="change"/> moves anything the push is judged on (FABRIC-CLOSE1h12): a quest, the configuration's
    /// rules, a set-up step the review reads.
    /// </summary>
    protected async Task<IReadOnlyList<string>> HeldAtThePushAsync(string root, Action change, int choices = 1)
    {
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Equal(choices, chosen.Count);
        Task<IReadOnlyList<string>> landing;
        using (var pushing = await LandingHolds.BranchAsync(_home, root, PushBranch))
        {
            Assert.NotNull(pushing);
            using (ExecutionContext.SuppressFlow())
            {
                landing = Task.Run(() => lander.LandAsync(chosen));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, PushBranch) > 0 || landing.IsCompleted,
                // TESTWAIT8: the hold's own signal, or the landing ending; the bound only elapses on a real failure, so it is minutes.
                TimeSpan.FromMinutes(5)), "the push waits for its branch");
            change();
        }

        return await landing;
    }

    protected static async Task<IReadOnlyList<string>> FeatureBranchesAsync(string root) =>
        (await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/heads/feature/"))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private const string Graph = "sha256:closure";

    /// <summary>A submission made on this machine at <paramref name="sequence"/> (FABRIC-CLOSE1a).</summary>
    protected static QuestSubmissionView Made(long sequence) => new("desk", sequence, DateTimeOffset.UtcNow, "ready");

    /// <summary>The agent moves its tree onto a branch of its own and commits its work there (TREEFOLLOW1, D164 point 4).</summary>
    protected static async Task OwnBranchAsync(string tree)
    {
        await GitAsync(tree, "checkout", "--quiet", "-b", "feature/own");
        await CommitAsync(tree, "s1.txt", "the work of s1", "the work of s1");
    }

    // ——— the world, the plugin and the fixtures

    /// <summary>The service's quests and records, standing in.</summary>
    protected sealed class World : IAutoLandingWorld
    {
        public Action? ReadingQuests { get; set; }
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlySet<string>>(Records.Where(r => r.Live && r.Tree is not null).Select(r => r.Tree!).ToHashSet());

        // The review's gate reads the chain and its ask (REVIEWENV1c): no rule names a review here, so these decide nothing.
        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct)
        {
            ReadingQuests?.Invoke();
            return Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);
        }

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        // The second opinion's gate reads the host's opinions (XAGENT1f): none, unless a test settles one (FABRIC-CLOSE1h15).
        public Dictionary<string, OpinionView> Opinions { get; } = new(StringComparer.Ordinal);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult(Opinions.GetValueOrDefault(id));
    }

    private sealed class FakeLander(Func<object, PluginLanding> land) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(land(payload));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    protected bool _pushes = true;

    // What happens as the plugin's process starts, before its frame (LANDPIN2), and as it lands, before it answers (LANDRACE1).
    protected Action? _starting;
    protected Action? _landing;

    // The plugin's next answer, once, where a test names one (LANDRACE1's review).
    protected Func<PluginLanding>? _answer;

    protected AutoLander Lander() => new(
        _home, _world, new SessionTrees(_home, Plugins()), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add);

    /// <summary>The rule's plugin, faked on its channel: every frame it is told is kept, whichever door told it.</summary>
    protected LandingPlugins Plugins() => new(_home, start: (_, _, _) =>
    {
        _starting?.Invoke();
        return Task.FromResult<IHookChannel>(new FakeLander(frame =>
        {
            lock (_frames) _frames.Add(frame);
            _landing?.Invoke();
            if (_answer is { } answer)
            {
                _answer = null;
                return answer();
            }

            return _pushes
                ? new PluginLanding(Plugin, true, "https://example.test/pull/7", "pushed it.")
                : new PluginLanding(Plugin, false, null, "gh is not signed in — run `gh auth login`.");
        }));
    });

    protected static JsonElement Json(object frame) => JsonDocument.Parse(JsonSerializer.Serialize(frame)).RootElement.Clone();

    /// <summary>One look: what it chose, then what landing those beside it said.</summary>
    protected async Task<IReadOnlyList<string>> LookAsync(bool pushes = true)
    {
        _pushes = pushes;
        var lander = Lander();
        var (chosen, said) = await lander.ChooseAsync(Config());
        return [.. said, .. await lander.LandAsync(chosen)];
    }

    protected DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>A session that worked in its own tree and concluded on its quest's done, made due as the driver's conclusion makes it.</summary>
    /// <param name="parent">The quest it follows, where it is a chain's later step.</param>
    /// <param name="from">The session branch its tree grows from, as a chain's step grows (CHAIN2).</param>
    /// <param name="entry">The entry its record names (SESSONE1b), shared by an entry's parts; none is its own line of work.</param>
    /// <param name="submitted">Bound to a closure graph and at its submission, taken, rather than done (FABRIC-CLOSE1h, §E5).</param>
    protected async Task<string> DoneSessionAsync(
        string root, string session, string quest, bool held = false, bool commit = true, string? parent = null, string? from = null,
        string? entry = null, bool submitted = false)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora", from: from);
        if (commit) await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        var view = new QuestView(quest, "game", "engine", "Fix the gap", "Fix it.", submitted ? "Taken" : "Done") { Held = held, Parent = parent };
        // FABRIC-CLOSE1h: a closure graph's quest stays taken at its agent's `ready`, its submission current (§E5).
        _world.Quests[quest] = submitted ? view with { Closure = Graph, Submission = Made(1) } : view;
        _world.Records.Add(new SessionRecord(session, "engine", "completed")
        {
            Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow, Entry = entry,
        });
        // The status the driver's conclusion hands on, as it reads the quest after the run (Driver.ConcludedForLandingAsync's callers).
        var verdict = AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session, _world.Quests[quest],
            QuestResults.Status(_world.Quests[quest]), "completed", opened.Path, "aurora");
        // The rule as it lands, the access setup's landing composed in (AUTONOMY1c2).
        Assert.Equal(LandingRules.Effective(Config(), "engine", "aurora").Rule.AutoAccept ? AutoConcluded.Due : null, verdict);
        return opened.Path;
    }

    /// <summary>
    /// The session's record concluding again on its quest as it stands now, as the driver's conclusion of a carried-on run hands it on
    /// (D137's words that went on, or the agent's next <c>ready</c>): due again.
    /// </summary>
    protected void ConcludeAgain(string session, string quest, string tree) =>
        Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session,
            _world.Quests[quest], QuestResults.Status(_world.Quests[quest]), "completed", tree, "aurora"));

    /// <summary>What a session in this repository's tree would be told of its landing, as a start composes it.</summary>
    protected string Instruction(string root)
    {
        var trees = new SessionTrees(_home);
        var tree = trees.OpenAsync(root, "engine", "aurora").GetAwaiter().GetResult();
        var plan = trees.PlanAsync(tree.Path, new LandingSubject("s9", "q9", "Fix the gap")).GetAwaiter().GetResult();
        var quest = new QuestView("q9", "game", "engine", "Fix the gap", "Fix it.", "Open");
        return TargetPrompt.Compose(SessionTarget.ForQuest(quest, tree.Path, "http://stand-in") with { LandsOn = plan });
    }

    protected IReadOnlyList<SessionEvent> Notes(string session) =>
        [.. new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note)];

    protected static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;

    protected void Rule(LandingRule rule) =>
        DriverConfig.Empty.WithLanding("engine", rule).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>
    /// The repository's rule reads a second opinion before landing from now on (FABRIC-CLOSE1h15, h17), which nobody has asked yet: made
    /// after a done, it is a rule that started reading after it, so the done owed none.
    /// </summary>
    protected void ReadsAnOpinion() =>
        (Config() with
        {
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["engine"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], true),
            },
        }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>The reviewer read s1's work at <paramref name="tip"/> and raised nothing: the opinion, kept at the gate as asked, settles.</summary>
    protected void Settled(string id, string tip)
    {
        _world.Opinions[id] = new OpinionView(id, OpinionRules.Landing, "first", "s1", "r-" + id, "engine", tip, tip, "codex-acp",
            ReviewerLabels.AnotherMaker, "given")
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20, Findings = [], Read = "the whole change",
        };
        new OpinionGates(_home).Asked("q1", "engine", new OpinionAskKept(DateTimeOffset.UtcNow, OpinionRules.Landing, "s1", tip)
        {
            Opinion = id, Reviewer = "codex-acp",
        });
    }

    /// <summary>Why the loop's opinion look would close <paramref name="due"/> now, as it reads it (FABRIC-CLOSE1h15), or null where it is still owed.</summary>
    protected Task<string?> ClosesAsync(OpinionDue due) =>
        OpinionLook.ClosesAsync(due, _world.Quests[due.Quest], [.. _world.Quests.Values], Config(), new LandedBranches(_home), CancellationToken.None,
            pushHeld: OpinionLook.PushHeld(_home));

    protected void InstallManifest()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins,Plugin);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "example.lands", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");
    }

    protected static async Task<string> HeadAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    protected static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    protected async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
        return root;
    }

    protected static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    protected static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        // 🔴 Both streams are read at once, before the wait: read one after the other, a git that writes more than a pipe's
        // worth of warnings to the second blocks on it while the first is still being read (FIX-LOG 2026-10-04).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
