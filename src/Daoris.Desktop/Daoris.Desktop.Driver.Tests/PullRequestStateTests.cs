using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGHOOK1a (D148, the plugin hooks design §2.1–§2.5): a squash-merged pull request's branches go on its plugin's word,
/// where git confirms it. Real git, a bare <c>origin</c> beside the checkout and a "platform" that squash-merges in a clone of
/// it; the plugin is faked on its channel, answering by branch as its platform would, so nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class PullRequestStateTests : LandedFixture
{
    private const string Plugin = "acme.asks";

    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>What the fake platform answers for each branch, and how many times it was asked.</summary>
    private readonly Dictionary<string, Func<PullRequestState>> _answers = new(StringComparer.Ordinal);

    private int _asked;

    /// <summary>
    /// The asks' clock, held for the whole test: an answer from the last minute is reused (PLUGHOOK1a), and on the wall
    /// clock a first plan slower than a minute under load made the second plan ask again (merging integrate-ak, 2026-10-04).
    /// </summary>
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public PullRequestStateTests()
    {
        var folder = Path.Combine(Home, "plugins", Plugin);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new
        {
            id = Plugin,
            hooks = new { command = new[] { "node", "plugin.mjs" }, points = new[] { HookPoints.Land, HookPoints.State } },
        }));
        Rule(tidy: false);
    }

    private void Rule(bool tidy) =>
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: tidy, Plugin: Plugin))
            .Save(Path.Combine(Home, "driver.json"));

    private SessionTrees Trees() => new(Home, new LandingPlugins(
        Home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Platform(this)), clock: () => _now));

    private static PullRequestState Completed(string merge, string source, string target = "main") => new(PullRequestStates.Completed)
    {
        PullRequest = "https://example.test/org/project/_git/engine/pullrequest/7",
        MergeCommit = merge,
        SourceCommit = source,
        Target = target,
        How = MergeHow.Squash,
    };

    /// <summary>
    /// LAND3's tidy, after the landing's plugin step (design §2.1 occasion 1): an earlier landing's pull request completed by
    /// squash, the platform deleted its branch and so did the person, and its session branch held commits no branch of theirs
    /// holds. The next landing in the repository asks, and that session branch goes with its tree, kept on the entry.
    /// </summary>
    [Fact]
    public async Task The_tidy_takes_a_session_branch_a_squash_merged_pull_request_carried()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "the first work\n", "the first work");
        var landed = await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "First"));
        Assert.True(landed.Landed, landed.Message);
        var source = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        var merge = await SquashOnPlatformAsync(origin, landed.Branch!);
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        await GitAsync(root, "branch", "-D", landed.Branch!);
        _answers[landed.Branch!] = () => Completed(merge, source);

        Rule(tidy: true);
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(second.Path, "second.txt", "other work\n", "other work");
        var next = await trees.LandAsync(second.Path, new LandingSubject("s2", "q2", "Second"), inUse: NoneRunning);

        Assert.True(next.Landed, next.Message);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", first.Branch)).Trim());
        Assert.False(Directory.Exists(first.Path), "its tree went with it");
        Assert.Contains(new TidiedBranch(first.Branch, Removed: true, Tree: true) { CarriedBy = landed.Branch }, next.Tidied!);
        Assert.Contains($"whose work `{landed.Branch}`'s completed pull request carried", next.Message);
        var entry = trees.Recorded.Landing("s1")!;
        Assert.Equal(PullRequestStates.Completed, entry.PullRequestState!.State);
        Assert.Equal(Plugin, entry.PullRequestState.Plugin);
        var carried = Assert.Single(entry.Carried);
        Assert.Equal((first.Branch, CarriedBy.Tidy), (carried.Branch, carried.By));
    }

    /// <summary>
    /// The clean-up's look asks; its press acts on what was kept (design §2.1 occasion 2, §2.3). A landed branch squash-merged
    /// after which the line changed its file again (D102's proof cannot see it) goes as <c>pull-request</c>; each other code
    /// keeps its branch and names why; a second look within the minute asks nothing.
    /// </summary>
    [Fact]
    public async Task The_clean_up_removes_a_squash_merged_branch_on_a_confirmed_answer_and_names_each_code_it_keeps()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var merged = await LandAsync(trees, root, "merged.txt", "merged\n", new LandingSubject("s1", "q1", "Merged"));
        var open = await LandAsync(trees, root, "open.txt", "open\n", new LandingSubject("s2", "q2", "Open"));
        var elsewhere = await LandAsync(trees, root, "elsewhere.txt", "elsewhere\n", new LandingSubject("s3", "q3", "Elsewhere"));
        var missing = await LandAsync(trees, root, "missing.txt", "missing\n", new LandingSubject("s4", "q4", "Missing"));
        var beyond = await LandAsync(trees, root, "beyond.txt", "beyond\n", new LandingSubject("s5", "q5", "Beyond"));
        string Tip(TreeLanding landing) => trees.Recorded.Of("engine", landing.Branch!)!.Tip;

        // The platform squash-merges two of them; after the first the line changes its file again, so D102 cannot see it.
        var mergedAt = await SquashOnPlatformAsync(origin, merged.Branch!, then: ("merged.txt", "changed on the line since\n"));
        var beyondAt = await SquashOnPlatformAsync(origin, beyond.Branch!);
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        // A later commit on one of them that its pull request never carried.
        await CommitOnAsync(root, beyond.Branch!, "after.txt", "after the pull request\n", "after the pull request");

        _answers[merged.Branch!] = () => Completed(mergedAt, Tip(merged));
        _answers[open.Branch!] = () => new PullRequestState(PullRequestStates.Open);
        _answers[elsewhere.Branch!] = () => Completed(mergedAt, Tip(elsewhere), target: "release");
        _answers[missing.Branch!] = () => Completed(new string('c', 40), Tip(missing));
        _answers[beyond.Branch!] = () => Completed(beyondAt, Tip(beyond));

        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        var asked = _asked;
        await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(5, asked);
        Assert.Equal(asked, _asked);
        var item = Landed(plan, merged.Branch!);
        Assert.Equal((LandedKind.PullRequest, "origin/main"), (item.Kind, item.Where));
        Assert.True(item.Removable);
        Assert.Contains("its pull request completed by squash, as `acme.asks` answered,", LandedWords.Describe(item));
        Assert.Equal(PullRequestStates.Open, Landed(plan, open.Branch!).StateCode);
        Assert.Equal(PullRequestCodes.OtherTarget, Landed(plan, elsewhere.Branch!).StateCode);
        Assert.Equal(PullRequestCodes.MergeNotHere, Landed(plan, missing.Branch!).StateCode);
        Assert.Equal(PullRequestCodes.Beyond, Landed(plan, beyond.Branch!).StateCode);
        Assert.All(new[] { open, elsewhere, missing, beyond }, each => Assert.False(Landed(plan, each.Branch!).Removable));

        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(5, _asked);
        Assert.True(done.Landed.Single(result => result.Item.Branch == merged.Branch).Removed);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", merged.Branch!)).Trim());
        var trace = trees.Recorded.Landing("s1")!;
        Assert.Equal((LandedKind.PullRequest, "origin/main"), (trace.RemovedAs, trace.RemovedOn));
        foreach (var kept in new[] { open, elsewhere, missing, beyond })
        {
            Assert.NotEqual("", (await GitAsync(root, "branch", "--list", kept.Branch!)).Trim());
        }
    }

    /// <summary>
    /// A session branch whose landed branch the person deleted after its pull request completed by squash: D88 can no longer see
    /// its commits on any branch of theirs, the clean-up's look asks, and the press removes it as carried, kept on the entry.
    /// A source commit this machine never had keeps everything (<c>source-not-here</c>).
    /// </summary>
    [Fact]
    public async Task The_clean_up_takes_a_session_branch_a_completed_pull_request_carried_and_keeps_one_it_cannot_confirm()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var carried = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(carried.Path, "carried.txt", "carried\n", "carried");
        var landing = await trees.LandAsync(carried.Path, new LandingSubject("s1", "q1", "Carried"));
        var unknown = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(unknown.Path, "unknown.txt", "unknown\n", "unknown");
        var other = await trees.LandAsync(unknown.Path, new LandingSubject("s2", "q2", "Unconfirmed"));
        var source = (await GitAsync(root, "rev-parse", landing.Branch!)).Trim();
        var merge = await SquashOnPlatformAsync(origin, landing.Branch!);
        var otherMerge = await SquashOnPlatformAsync(origin, other.Branch!);
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        await GitAsync(root, "branch", "-D", landing.Branch!);
        await GitAsync(root, "branch", "-D", other.Branch!);
        _answers[landing.Branch!] = () => Completed(merge, source);
        _answers[other.Branch!] = () => Completed(otherMerge, new string('d', 40));

        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        var row = plan.Sessions.Single(item => item.Branch == carried.Branch);
        Assert.Equal((SweepKind.Carried, landing.Branch), (row.Kind, row.Where));
        Assert.True(row.Removable);
        Assert.Equal(SweepKind.Unlanded, plan.Sessions.Single(item => item.Branch == unknown.Branch).Kind);

        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal("", (await GitAsync(root, "branch", "--list", carried.Branch)).Trim());
        Assert.NotEqual("", (await GitAsync(root, "branch", "--list", unknown.Branch)).Trim());
        var entry = trees.Recorded.Landing("s1")!;
        Assert.Equal((carried.Branch, CarriedBy.CleanUp), (Assert.Single(entry.Carried).Branch, entry.Carried[0].By));
    }

    /// <summary>
    /// Without an answer nothing is removed, and absent is never zero (design §2.4): a plugin that fails keeps its code beside
    /// the kept answer, which it never overwrites, and the branch stays.
    /// </summary>
    [Fact]
    public async Task A_failed_ask_removes_nothing_and_never_overwrites_the_kept_answer()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var landed = await LandAsync(trees, root, "work.txt", "work\n", new LandingSubject("s1", "q1", "Work"));
        trees.Recorded.Answered(trees.Recorded.Landing("s1")!, new PullRequestState(PullRequestStates.Open)
        {
            Plugin = Plugin,
            AskedAt = _now.AddHours(-1),
        });
        _answers[landed.Branch!] = () => throw PluginFailures.Mark(new DriverException("az: not signed in"), PluginEvents.Errored);

        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        var item = Landed(plan, landed.Branch!);
        Assert.False(item.Removable);
        Assert.Equal(PullRequestStates.Open, item.StateCode);
        var entry = trees.Recorded.Landing("s1")!;
        Assert.Equal(PullRequestStates.Open, entry.PullRequestState!.State);
        Assert.Equal(PluginEvents.Errored, entry.PullRequestAskFailed!.Code);
    }

    /// <summary>
    /// PLUGHOOK1c (design §2.1 occasion 3): bringing up to date's look asks after its fetch, which is where the platform's squash
    /// first reaches this machine, so the answer confirms there and the landed branch is listed to go as <c>pull-request</c>. A look
    /// that fetched nothing asks nothing, and the press acts on what was kept without asking again.
    /// </summary>
    [Fact]
    public async Task Bringing_up_to_date_asks_after_its_fetch_and_its_press_removes_on_the_kept_answer()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "merged.txt", "merged\n", "Merged");
        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "q1", "Merged"));
        Assert.True(landed.Landed, landed.Message);
        // Its session branch is gone, as a tidy leaves it, so no session branch leans on the landed one.
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);
        await GitAsync(root, "branch", "-D", tree.Branch);
        var tip = trees.Recorded.Of("engine", landed.Branch!)!.Tip;
        // The platform squash-merges it and the line changes its file again; nothing of that is fetched here yet.
        var merge = await SquashOnPlatformAsync(origin, landed.Branch!, then: ("merged.txt", "changed on the line since\n"));
        _answers[landed.Branch!] = () => Completed(merge, tip);

        var unfetched = await trees.SyncPlanAsync([("engine", "aurora", root)], new HashSet<string>(), fetch: false);
        Assert.Equal(0, _asked);
        Assert.DoesNotContain(unfetched.Deletes, item => item.Branch == landed.Branch && item.Removable);

        var plan = await trees.SyncPlanAsync([("engine", "aurora", root)], new HashSet<string>(), fetch: true);

        Assert.Equal(1, _asked);
        var item = plan.Deletes.Single(each => each.Branch == landed.Branch);
        Assert.Equal((LandedKind.PullRequest, "origin/main"), (item.Kind, item.Where));
        Assert.True(item.Removable);
        Assert.Equal(PullRequestStates.Completed, trees.Recorded.Landing("s1")!.PullRequestState!.State);

        var done = await trees.SyncAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(1, _asked);
        Assert.True(done.Deletes.Single(result => result.Item.Branch == landed.Branch).Removed);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", landed.Branch!)).Trim());
        Assert.Equal(LandedKind.PullRequest, trees.Recorded.Landing("s1")!.RemovedAs);
    }

    /// <summary>
    /// PLUGHOOK1c (design §2.1): a row whose pull request was not asked about says why — the plugin that pushed it is not installed
    /// here, or no plugin pushed it and the rule names none — at the clean-up's look and at bringing up to date's. Nothing goes.
    /// </summary>
    [Fact]
    public async Task The_rows_say_why_a_pull_request_was_not_asked_about()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var gone = await LandAsync(trees, root, "gone.txt", "gone\n", new LandingSubject("s1", "q1", "Gone"));
        // The plugin that pushed it is no longer on this machine.
        trees.Recorded.Pushed("engine", gone.Branch!, new PluginLanding("acme.gone", Pushed: true, null, "pushed"), trees.Recorded.Of("engine", gone.Branch!)!.Tip);
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
            .Save(Path.Combine(Home, "driver.json"));
        var unpushed = await LandAsync(trees, root, "unpushed.txt", "unpushed\n", new LandingSubject("s2", "q2", "Unpushed"));

        var plan = await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(0, _asked);
        var unready = Landed(plan, gone.Branch!);
        Assert.Equal(PullRequestCodes.Unready, unready.NotAsked!.Code);
        Assert.EndsWith("`acme.gone`, which pushed the branch, is not installed on this machine, so its pull request was not asked about",
            LandedWords.Describe(unready));
        var nobody = Landed(plan, unpushed.Branch!);
        Assert.Equal(new PullRequestNotAsked(PullRequestCodes.NoPlugin, PullRequestWords.NoPlugin), nobody.NotAsked);
        Assert.All(new[] { unready, nobody }, item => Assert.False(item.Removable));

        var sync = await trees.SyncPlanAsync([("engine", "aurora", root)], new HashSet<string>(), fetch: true);

        Assert.Equal(0, _asked);
        var row = sync.Deletes.Select(item => (item.Branch, item.NotAsked)).Concat(sync.Rebases.Select(item => (item.Branch, item.NotAsked)))
            .Single(each => each.Branch == gone.Branch);
        Assert.Equal(PullRequestCodes.Unready, row.NotAsked!.Code);
    }

    /// <summary>
    /// PLUGHOOK1c (design §2.1 occasion 4): <i>Ask again</i> asks whatever the kept answer's age, keeps a failure beside the answer
    /// and never over it, says what a completed answer proves here once its merge commit is fetched, and asks a final answer no more.
    /// </summary>
    [Fact]
    public async Task Ask_again_asks_whatever_the_kept_answers_age_and_a_completed_one_no_more()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var landed = await LandAsync(trees, root, "work.txt", "work\n", new LandingSubject("s1", "q1", "Work"));
        LandedBranch Entry() => trees.Recorded.FindAll(landed.Branch!, "engine")[0];
        _answers[landed.Branch!] = () => new PullRequestState(PullRequestStates.Open);

        var first = await trees.AskAgainAsync(root, Entry());
        var second = await trees.AskAgainAsync(root, Entry());

        Assert.Equal(2, _asked);
        Assert.True(first.Answered && second.Answered);
        Assert.Equal(PullRequestStates.Open, second.Verdict!.Code);
        Assert.Equal("nothing goes on its word: its pull request has not completed.", PullRequestWords.AskedAgain(second)[^1]);

        _answers[landed.Branch!] = () => throw PluginFailures.Mark(new DriverException("az: not signed in"), PluginEvents.Errored);
        var failed = await trees.AskAgainAsync(root, Entry());

        Assert.False(failed.Answered);
        Assert.Equal(PluginEvents.Errored, failed.Code);
        Assert.Equal(PullRequestStates.Open, failed.Entry.PullRequestState!.State);
        Assert.Equal(PluginEvents.Errored, failed.Entry.PullRequestAskFailed!.Code);

        var tip = Entry().Tip;
        var merge = await SquashOnPlatformAsync(origin, landed.Branch!, deleteBranch: false);
        await GitAsync(root, "fetch", "--quiet", "origin");
        _answers[landed.Branch!] = () => Completed(merge, tip);
        var completed = await trees.AskAgainAsync(root, Entry());

        Assert.True(completed.Answered);
        Assert.Null(completed.Entry.PullRequestAskFailed);
        Assert.Equal(new PullRequestVerdict(null, "origin/main", Carries: true, Clears: true), completed.Verdict);
        var asked = _asked;

        var final = await trees.AskAgainAsync(root, Entry());

        Assert.Equal(asked, _asked);
        Assert.True(final.Final);
        Assert.Equal(completed.Verdict, final.Verdict);
    }

    /// <summary>The fake platform: a landing's push is a real one into the bare origin; a state is what the test says for the branch.</summary>
    private sealed class Platform(PullRequestStateTests test) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land, HookPoints.State];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public async Task<PluginLanding> LandAsync(object payload, CancellationToken ct)
        {
            var frame = JsonSerializer.SerializeToElement(payload);
            var branch = frame.GetProperty("branch").GetString()!;
            await GitAsync(frame.GetProperty("root").GetString()!, "push", "--quiet", "origin", branch);
            return new PluginLanding(Plugin, Pushed: true, $"https://example.test/org/project/_git/engine/pullrequest/{branch.Length}", "pushed");
        }

        public Task<PullRequestState> StateAsync(object payload, CancellationToken ct)
        {
            test._asked++;
            var branch = JsonSerializer.SerializeToElement(payload).GetProperty("branch").GetString()!;
            return Task.FromResult(test._answers.TryGetValue(branch, out var answer) ? answer() : new PullRequestState(PullRequestStates.Unknown));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
