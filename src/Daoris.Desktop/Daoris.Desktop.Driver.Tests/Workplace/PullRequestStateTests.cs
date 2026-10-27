using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGHOOK1a (D148, the plugin hooks design §2.1–§2.5): a squash-merged pull request's branches go on its plugin's word,
/// where git confirms it. Real git, a bare <c>origin</c> beside the checkout and a "platform" that squash-merges in a clone of
/// it; the plugin is faked on its channel, answering by branch as its platform would, so nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
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
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins,Plugin);
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
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));

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
    /// 🔴 FABRIC-END4a, the fourth review's P1 (TreeTidies.cs:133 and :136, PullRequestAsks.cs:279): the carried tidy asks about the tip it
    /// read, never the branch's live name. A session branch at A, one commit past what the pull request carried, moved to B, which it carried,
    /// as it was asked, and back to A before its delete, keeps A: B's proof is not A's.
    /// </summary>
    [Fact]
    public async Task A_carried_tidy_whose_branch_moved_away_and_back_keeps_it()
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
        // The answer kept while the session branch is still what the pull request carried, so the carried tidy has a carrier to ask.
        await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Equal(PullRequestStates.Completed, trees.Recorded.Landing("s1")!.PullRequestState!.State);
        // A: a commit the pull request never carried, its tree removed by hand so the branch stands alone.
        await CommitAsync(first.Path, "more.txt", "more work\n", "more work");
        var a = (await GitAsync(root, "rev-parse", first.Branch)).Trim();
        await GitAsync(root, "worktree", "remove", first.Path);

        Rule(tidy: true);
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(second.Path, "second.txt", "other work\n", "other work");
        // The carried tidy reads the branch, then asks the carrier about it: the branch is at B while it is asked, and at A from the next
        // git start on. (The asks before it ask the same of the branch by name, after reading the pull request's source commit.)
        var (read, moved, back) = (false, false, false);
        WorkingTree.Started.Value = arguments =>
        {
            if (moved && !back)
            {
                back = true;
                GitNow(root, "update-ref", $"refs/heads/{first.Branch}", a);
            }
            else if (read && !moved && arguments is ["merge-base", "--is-ancestor", var revision, var over] && over == source
                     && (revision == a || revision == $"refs/heads/{first.Branch}"))
            {
                moved = true;
                GitNow(root, "update-ref", $"refs/heads/{first.Branch}", source);
            }

            read = arguments is ["rev-parse", "--verify", "--quiet", var named] && named == $"refs/heads/{first.Branch}";
        };
        try
        {
            var next = await trees.LandAsync(second.Path, new LandingSubject("s2", "q2", "Second"), inUse: NoneRunning);

            Assert.True(next.Landed, next.Message);
            Assert.True(back, "the branch moved to what was carried and back as it was asked");
            Assert.Equal(a, (await GitAsync(root, "rev-parse", first.Branch)).Trim());
            Assert.DoesNotContain(next.Tidied!, each => each.Branch == first.Branch && each.Removed);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }
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
    /// PUSH1c4a (D182, D148 point 2): the machine's facts judge one repository's list by the answer the record keeps and ask no plugin.
    /// A squash-merged branch D102 cannot see stays kept until the person's look asks; once its answer is kept, the part judges it as
    /// the look did, and still asks nothing.
    /// </summary>
    [Fact]
    public async Task The_facts_part_of_the_list_judges_by_the_kept_answer_and_asks_no_plugin()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var merged = await LandAsync(trees, root, "merged.txt", "merged\n", new LandingSubject("s1", "q1", "Merged"));
        var tip = trees.Recorded.Of("engine", merged.Branch!)!.Tip;
        var mergedAt = await SquashOnPlatformAsync(origin, merged.Branch!, then: ("merged.txt", "changed on the line since\n"));
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        _answers[merged.Branch!] = () => Completed(mergedAt, tip);

        var before = Landed(await trees.SweepPartAsync("engine", "aurora", root, new HashSet<string>()), merged.Branch!);

        Assert.Equal(0, _asked);
        Assert.False(before.Removable);
        Assert.Null(trees.Recorded.Landing("s1")!.PullRequestState);

        var look = Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), merged.Branch!);
        var after = Landed(await trees.SweepPartAsync("engine", "aurora", root, new HashSet<string>()), merged.Branch!);

        Assert.Equal(1, _asked);
        Assert.Equal((LandedKind.PullRequest, "origin/main", true), (after.Kind, after.Where, after.Removable));
        Assert.Equal((look.Kind, look.Where, look.Commits, look.State, look.StateCode), (after.Kind, after.Where, after.Commits, after.State, after.StateCode));
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
        // The line changes its file again after the squash, so its work is not on the line by content either (SQUASHTIDY1):
        // only the plugin's word could carry it, and that word is not confirmed.
        var otherMerge = await SquashOnPlatformAsync(origin, other.Branch!, then: ("unknown.txt", "changed on the line since\n"));
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

        var carriedTip = (await GitAsync(root, "rev-parse", carried.Branch)).Trim();
        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal("", (await GitAsync(root, "branch", "--list", carried.Branch)).Trim());
        // KEEPSIZE1b (the keep-size design §3.1, Opus finding 21): the press's path keeps a carried branch's exact tip on a ref of its own.
        Assert.Equal(carriedTip, (await GitAsync(root, "rev-parse", $"refs/daoris/discarded/{carried.Branch}")).Trim());
        Assert.NotEqual("", (await GitAsync(root, "branch", "--list", unknown.Branch)).Trim());
        var entry = trees.Recorded.Landing("s1")!;
        Assert.Equal((carried.Branch, CarriedBy.CleanUp), (Assert.Single(entry.Carried).Branch, entry.Carried[0].By));
    }

    /// <summary>
    /// AUTOTIDY2a: the watch's look takes a carried session branch by itself, as the press does, on the answer a list kept: its
    /// tree's folder moved aside and its removal kept on the landing's entry. The look asks no plugin; it reads the kept answers.
    /// </summary>
    [Fact]
    public async Task The_look_takes_a_session_branch_a_kept_answer_carried()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var carried = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(carried.Path, "carried.txt", "carried\n", "carried");
        var landing = await trees.LandAsync(carried.Path, new LandingSubject("s1", "q1", "Carried"));
        var source = (await GitAsync(root, "rev-parse", landing.Branch!)).Trim();
        var merge = await SquashOnPlatformAsync(origin, landing.Branch!);
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        await GitAsync(root, "branch", "-D", landing.Branch!);
        _answers[landing.Branch!] = () => Completed(merge, source);
        await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        var asked = _asked;
        // FABRIC-END4a: the look's tidy takes what the keeper or its fallback proves, both in the lease's process alone, as the loop's.
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        var carriedTip = (await GitAsync(root, "rev-parse", carried.Branch)).Trim();
        var pass = await trees.TidyEmptyAsync(root, "engine", "aurora", NoneRunning);

        Assert.Equal(asked, _asked);
        var removed = Assert.Single(pass.Results, result => result.Removed);
        Assert.Equal((carried.Branch, SweepKind.Carried, landing.Branch), (removed.Item.Branch, removed.Item.Kind, removed.Item.Where));
        Assert.NotNull(removed.MovedTo);
        // KEEPSIZE1b (the keep-size design §3.1, Opus finding 21): the look's path keeps it too, named in the folder's manifest as carried.
        Assert.Equal(carriedTip, (await GitAsync(root, "rev-parse", $"refs/daoris/discarded/{carried.Branch}")).Trim());
        var manifest = SlimManifest.Read(SlimManifest.PathFor(removed.MovedTo)).Manifest!;
        Assert.Equal(("carried", $"refs/daoris/discarded/{carried.Branch}"), (manifest.Finished!.Hold.Kind, manifest.Finished.Ref));
        Assert.False(Directory.Exists(carried.Path));
        Assert.Equal("", (await GitAsync(root, "branch", "--list", carried.Branch)).Trim());
        var entry = trees.Recorded.Landing("s1")!;
        Assert.Equal((carried.Branch, CarriedBy.CleanUp), (Assert.Single(entry.Carried).Branch, entry.Carried[0].By));
    }

    /// <summary>
    /// 🔴 FABRIC-END2d, after the merge: the keeper keeps a carried session branch's end once the platform and the person deleted the
    /// carrier's branch, resting on the form of the line its merge commit is on (<see cref="PullRequestCarrier.Rests"/>), as the carried
    /// tidy verifies it. It named the gone branch before, whose commit cannot be read, so no fact was kept and only END4a's fallback took it.
    /// </summary>
    [Fact]
    public async Task The_keeper_keeps_a_carried_session_branchs_end_on_the_form_its_merge_commit_is_on()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var carried = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(carried.Path, "carried.txt", "carried\n", "carried");
        var landing = await trees.LandAsync(carried.Path, new LandingSubject("s1", "q1", "Carried"));
        var source = (await GitAsync(root, "rev-parse", landing.Branch!)).Trim();
        var merge = await SquashOnPlatformAsync(origin, landing.Branch!);
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        await GitAsync(root, "branch", "-D", landing.Branch!);
        _answers[landing.Branch!] = () => Completed(merge, source);
        await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        await trees.Ends.KeepAsync(root, "engine", "aurora", "main", CancellationToken.None);

        var held = new SessionBranches(Home).Of("engine", carried.Branch)?.Held;
        Assert.NotNull(held);
        Assert.Equal((EndBy.Carried, "origin/main", (await GitAsync(root, "rev-parse", "origin/main")).Trim()), (held.By, held.On, held.OnAt));
    }

    /// <summary>
    /// AUTOTIDY2b: the watch's look takes a landing's branch whose pull request completed by squash, on the answer a list kept,
    /// though the line changed its file since, so D102's proof by content cannot see it: its session's branch first, whose work
    /// is on the line through it, its tree's folder moved aside; then the landing's branch, its commits kept on a ref of their
    /// own, and the record keeping what proved it. The look asks no plugin.
    /// </summary>
    [Fact]
    public async Task The_look_takes_a_landing_s_branch_a_kept_answer_completed()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "merged.txt", "merged\n", "merged");
        var landing = await trees.LandAsync(tree.Path, new LandingSubject("s1", "q1", "Merged"));
        Assert.True(landing.Landed, landing.Message);
        var source = (await GitAsync(root, "rev-parse", landing.Branch!)).Trim();
        var merge = await SquashOnPlatformAsync(origin, landing.Branch!, then: ("merged.txt", "changed on the line since\n"));
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        _answers[landing.Branch!] = () => Completed(merge, source);
        await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        var asked = _asked;
        // FABRIC-END4a: the look's tidy takes what the keeper or its fallback proves, both in the lease's process alone, as the loop's.
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        var pass = await trees.TidyEmptyAsync(root, "engine", "aurora", NoneRunning);

        Assert.Equal(asked, _asked);
        var session = Assert.Single(pass.Results, result => result.Removed);
        Assert.Equal((tree.Branch, SweepKind.Landed, "origin/main"), (session.Item.Branch, session.Item.Kind, session.Item.Where));
        Assert.NotNull(session.MovedTo);
        var removed = Assert.Single(pass.Landed, result => result.Removed);
        Assert.Equal((landing.Branch, LandedKind.PullRequest, "s1"), (removed.Item.Branch, removed.Item.Kind, removed.Session));
        Assert.Equal($"refs/daoris/discarded/{landing.Branch}", removed.KeptRef);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", landing.Branch!, tree.Branch)).Trim());
        var trace = trees.Recorded.Landing("s1")!;
        Assert.Equal((LandedKind.PullRequest, "origin/main"), (trace.RemovedAs, trace.RemovedOn));
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
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
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

    /// <summary>
    /// FABRIC-END6c, finding 2: two occasions at once share D148's minute. The first look's ask is in flight when the second looks, so
    /// the second asks nothing, and the pull request is asked once.
    /// </summary>
    [Fact]
    public async Task Two_looks_at_once_ask_a_pull_request_once()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = Trees();
        var landed = await LandAsync(trees, root, "work.txt", "work\n", new LandingSubject("s1", "q1", "Work"));
        _answers[landed.Branch!] = () => new PullRequestState(PullRequestStates.Open);
        _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        var second = trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        // The look that did not ask ends while the other's ask is held; asked twice, both would wait here.
        await Task.WhenAny(Task.WhenAny(first, second), Task.Delay(TimeSpan.FromSeconds(10)));
        _held.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, _asked);
        Assert.Equal(PullRequestStates.Open, trees.Recorded.Landing("s1")!.PullRequestState!.State);
    }

    /// <summary>
    /// FABRIC-END6c, finding 3: frames go one at a time, so each pull request's answer is kept with the time its own request was sent,
    /// not the occasion's start: the minute counts from when it was actually asked.
    /// </summary>
    [Fact]
    public async Task Each_pull_request_is_kept_with_its_own_request_s_time()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var clock = _now;
        _onAsk = () => clock = clock.AddSeconds(25);
        var trees = new SessionTrees(Home, new LandingPlugins(Home, start: (_, _, _) => Task.FromResult<IHookChannel>(new Platform(this)), clock: () => clock));
        var a = await LandAsync(trees, root, "a.txt", "a\n", new LandingSubject("s1", "q1", "First"));
        var b = await LandAsync(trees, root, "b.txt", "b\n", new LandingSubject("s2", "q2", "Second"));
        _answers[a.Branch!] = () => new PullRequestState(PullRequestStates.Open);
        _answers[b.Branch!] = () => new PullRequestState(PullRequestStates.Open);
        var start = clock;

        await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(2, _asked);
        Assert.Equal(
            [start, start.AddSeconds(25)],
            new[] { trees.Recorded.Landing("s1")!, trees.Recorded.Landing("s2")! }.Select(entry => entry.PullRequestState!.AskedAt).Order());
    }

    /// <summary>A plugin's ask held until a test lets it go; null answers at once.</summary>
    private TaskCompletionSource? _held;

    /// <summary>What each ask does before it answers, as a slow platform's time passing; null does nothing.</summary>
    private Action? _onAsk;

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

        public async Task<PullRequestState> StateAsync(object payload, CancellationToken ct)
        {
            Interlocked.Increment(ref test._asked);
            if (test._held is { } held) await held.Task.WaitAsync(ct);
            test._onAsk?.Invoke();
            var branch = JsonSerializer.SerializeToElement(payload).GetProperty("branch").GetString()!;
            return test._answers.TryGetValue(branch, out var answer) ? answer() : new PullRequestState(PullRequestStates.Unknown);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
