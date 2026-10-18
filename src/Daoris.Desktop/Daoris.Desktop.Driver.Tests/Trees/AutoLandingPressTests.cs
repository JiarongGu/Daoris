using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's doors beside the automatic landing, split from the one class by PROCSPLIT1: a press or a hand-off that waits behind a
/// landing of its tree or its branch (LANDRACE1, LANDPIN2), and a press or an entry's Accept judged again under its holds at its tree
/// and at its push (FABRIC-CLOSE1h3, D173's note).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AutoLandingPressTests : AutoLandingFixture
{
    /// <summary>
    /// LANDRACE1 (D145 point 2, D173's FABRIC-END7 note): the person presses Accept while the automatic landing of the same due entry
    /// is still in its plugin. The press waits for the entry's hold, finds the landing recorded while it waited, and says so: the plugin
    /// is spoken to once, and one landing is recorded. A tree on a branch of its own (TREEFOLLOW1) is the case git's ref writes never
    /// kept apart, since no ref is made for it: both doors landed it, and both pushed.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_press_beside_the_automatic_landing_of_one_entry_waits_for_it_and_lands_nothing_more(bool ownBranch)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", commit: !ownBranch);
        if (ownBranch) await OwnBranchAsync(tree);

        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Single(chosen);
        Task<TreeLanding>? press = null;
        var waits = new List<bool>();
        // The plugin's first frame: the person presses while the automatic landing's plugin is still pushing. The press is its own
        // request, so nothing of the landing's flow (its look, its table of git's answers) flows into it.
        _landing = () =>
        {
            _landing = null;
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() =>
                {
                    // LANDHOLD2: the press says it waits behind the landing, and that it has stopped.
                    using var told = LandingHoldNotice.Told(waiting => { lock (waits) waits.Add(waiting); });
                    return new SessionTrees(_home, Plugins()).LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap"));
                });
            }

            SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0 || press.IsCompleted, TimeSpan.FromSeconds(30));
        };

        await lander.LandAsync(chosen);
        var pressed = await press!;

        lock (waits) Assert.Equal([true, false], waits);
        Assert.Single(_frames);
        Assert.Single(new LandedBranches(_home).Entries(), entry => entry.Names("s1"));
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.False(pressed.Landed, pressed.Message);
        Assert.Equal(AutoLandingCode.Already, pressed.Refusal);
        Assert.Contains($"`{(ownBranch ? "feature/own" : "feature/q1-fix-the-gap")}`", pressed.Message);
        Assert.Contains("accepted automatically", pressed.Message);
    }

    /// <summary>
    /// LANDRACE1's review, finding 1: a press that waited is `already` only where the commit it would land is the one landed. The agent
    /// commits again on its own branch while the automatic landing's plugin pushes the first commit; the person presses for the new
    /// one, waits, and lands it: the plugin is told twice, each time its own commit.
    /// </summary>
    [Fact]
    public async Task A_press_for_a_later_commit_lands_it_after_the_landing_it_waited_behind()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", commit: false);
        await OwnBranchAsync(tree);
        var first = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Task<TreeLanding>? press = null;
        _landing = () =>
        {
            _landing = null;
            CommitAsync(tree, "more.txt", "made while the plugin pushed", "more work").GetAwaiter().GetResult();
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() => new SessionTrees(_home, Plugins()).LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap")));
            }

            SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0 || press.IsCompleted, TimeSpan.FromSeconds(30));
        };

        await lander.LandAsync(chosen);
        var pressed = await press!;

        var second = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        Assert.True(pressed.Landed, pressed.Message);
        Assert.Equal([first, second], _frames.Select(frame => Json(frame).GetProperty("tip").GetString()));
        Assert.Equal(second, new LandedBranches(_home).Landing("s1")!.Tip);
    }

    /// <summary>
    /// LANDRACE1's review, finding 1: a press that waited behind a landing of the same commit is told what that landing's plugin answered,
    /// its failure included, rather than a bare `already`: the branch stands, and the sentence says how it is pushed.
    /// </summary>
    [Fact]
    public async Task A_press_behind_a_landing_whose_plugin_failed_is_told_the_failure()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Task<TreeLanding>? press = null;
        _answer = () => new PluginLanding(Plugin, false, null, "gh is not signed in — run `gh auth login`", Failed: true);
        _landing = () =>
        {
            _landing = null;
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() => new SessionTrees(_home, Plugins()).LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap")));
            }

            SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0 || press.IsCompleted, TimeSpan.FromSeconds(30));
        };

        await lander.LandAsync(chosen);
        var pressed = await press!;

        Assert.Single(_frames);
        Assert.False(pressed.Landed, pressed.Message);
        Assert.Equal(AutoLandingCode.Already, pressed.Refusal);
        Assert.True(pressed.Plugin is { Failed: true }, pressed.Message);
        Assert.Contains("gh is not signed in", pressed.Message);
        Assert.Contains("`feature/q1-fix-the-gap`", pressed.Message);
    }

    /// <summary>
    /// LANDRACE1's review, finding 4: a hand-off of the branch a landing just recorded, while that landing's plugin still pushes, waits for
    /// the push and hands nothing more on: the plugin is told once, and the hand-off says the branch was pushed at this commit. Its
    /// re-check: a terminal's hand-off from a linked worktree of the repository (a registry may name one) takes the same hold as the
    /// landing, which names the repository by its main checkout.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hand_off_beside_a_landings_push_waits_for_it_and_hands_nothing_more_on(bool fromLinked)
    {
        var main = await RepositoryAsync("engine");
        var root = main;
        if (fromLinked)
        {
            root = Path.Combine(_scratch, "engine-linked");
            await GitAsync(main, "worktree", "add", "--quiet", "--detach", root);
        }

        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(main, "s1", "q1");
        const string branch = "feature/q1-fix-the-gap";
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Task<TreeHand>? hand = null;
        _landing = () =>
        {
            _landing = null;
            var recorded = new LandedBranches(_home).Of("engine", branch)!;
            using (ExecutionContext.SuppressFlow())
            {
                hand = Task.Run(() => new SessionTrees(_home, Plugins()).HandAsync(root, recorded));
            }

            SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, branch) > 0 || hand.IsCompleted, TimeSpan.FromSeconds(30));
        };

        await lander.LandAsync(chosen);
        var handed = await hand!;

        Assert.Single(_frames);
        Assert.False(handed.Handed, handed.Message);
        Assert.Contains("pushed", handed.Message);
        Assert.True(new LandedBranches(_home).Of("engine", branch)!.Pushed);
    }

    /// <summary>
    /// LANDPIN2 (D164 point 4, D173's FABRIC-END7 note): a tree on a branch of its own has no ref made for it, so its plugin is handed the
    /// commit judged to push, not the branch's name alone. Its agent commits again after the judgement, as the plugin starts: the frame
    /// still names the commit judged, which the record keeps as the one landed.
    /// </summary>
    [Fact]
    public async Task A_tree_on_its_own_branch_hands_its_plugin_the_commit_judged_not_the_branch_as_it_stands()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", commit: false);
        await OwnBranchAsync(tree);
        var judged = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        // The plugin's process starting: the agent commits on its branch after the landing judged it, before its plugin pushes.
        _starting = () =>
        {
            _starting = null;
            CommitAsync(tree, "late.txt", "made after the judgement", "a late change").GetAwaiter().GetResult();
        };

        await lander.LandAsync(chosen);

        var frame = Json(Assert.Single(_frames));
        Assert.Equal("feature/own", frame.GetProperty("branch").GetString());
        Assert.Equal(judged, frame.GetProperty("tip").GetString());
        Assert.NotEqual(judged, (await GitAsync(tree, "rev-parse", "HEAD")).Trim());
        Assert.Equal(judged, new LandedBranches(_home).Landing("s1")!.Tip);
    }

    // ——— FABRIC-CLOSE1h3: the person's press and an entry's Accept judged again under their holds (D173's note)

    /// <summary>
    /// The row's proof. A press waits behind another landing of its tree (LANDRACE1, bounded only at minutes), and the result it was
    /// made on moves meanwhile: a reopen, a later <c>ready</c> at the same commit, one on another machine, or a plain done turned down.
    /// It makes nothing: no branch, no record, no plugin told, and says why. A hold on the same result does not stop it (the parent's
    /// decision 1: the press is the person's own word), and a press on an unfinished quest or on no quest holds to nothing and lands as
    /// before (decision 3).
    /// </summary>
    [Theory]
    [InlineData("reopened", false)]
    [InlineData("again", false)]
    [InlineData("elsewhere", false)]
    [InlineData("turned-down", false)]
    [InlineData("held", true)]
    [InlineData("unfinished", true)]
    [InlineData("conversation", true)]
    public async Task A_press_waiting_for_its_tree_makes_nothing_where_the_result_it_was_made_on_moved(string change, bool lands)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: change is "reopened" or "again" or "elsewhere" or "held");
        if (change == "unfinished") _world.Quests["q1"] = _world.Quests["q1"] with { Status = "Taken" };

        Task<TreeLanding> press;
        using (var holding = await LandingHolds.TakeAsync(_home, tree))
        {
            Assert.NotNull(holding);
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() => PressAsync(tree, change == "conversation" ? null : "q1"));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0, TimeSpan.FromSeconds(30)), "the press waits for its tree");
            var quest = _world.Quests["q1"];
            _world.Quests["q1"] = change switch
            {
                "reopened" => quest with { Submission = null },
                "again" => quest with { Submission = Made(2) },
                "elsewhere" => quest with { Submission = quest.Submission! with { Machine = "laptop" } },
                "turned-down" => quest with { Status = "Taken" },
                _ => quest with { Held = true, Hold = EvidenceCodes.Departed },
            };
        }

        var pressed = await press;

        Assert.Equal(lands, pressed.Landed);
        if (lands)
        {
            Assert.Single(_frames);
            return;
        }

        Assert.False(await BranchAsync(root, PushBranch), pressed.Message);
        Assert.Empty(_frames);
        Assert.Null(new LandedBranches(_home).Landing("s1"));
        Assert.Contains(change is "reopened" or "turned-down" ? "its quest was taken back" : "its quest's submission was made again", pressed.Message);
    }

    /// <summary>
    /// The same at the push (the parent's decision 4): a press whose push waits behind another push of its branch, its result moved
    /// meanwhile, keeps the branch made and recorded, tells no plugin, and says why and that the hand-off pushes it; the verb exits 1. A
    /// hold on the same result there pushes, once.
    /// </summary>
    [Theory]
    [InlineData("reopened", false)]
    [InlineData("again", false)]
    [InlineData("held", true)]
    public async Task A_press_waiting_for_its_push_keeps_its_branch_and_tells_no_plugin_where_its_result_moved(string change, bool pushes)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: true);

        Task<TreeLanding> press;
        using (var pushing = await LandingHolds.BranchAsync(_home, root, PushBranch))
        {
            Assert.NotNull(pushing);
            using (ExecutionContext.SuppressFlow())
            {
                press = Task.Run(() => PressAsync(tree));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, PushBranch) > 0 || press.IsCompleted,
                TimeSpan.FromSeconds(60)), "the push waits for its branch");
            var quest = _world.Quests["q1"];
            _world.Quests["q1"] = change switch
            {
                "reopened" => quest with { Submission = null },
                "again" => quest with { Submission = Made(2) },
                _ => quest with { Held = true, Hold = EvidenceCodes.Departed },
            };
        }

        var pressed = await press;

        Assert.True(pressed.Landed, pressed.Message);
        Assert.True(await BranchAsync(root, PushBranch));
        Assert.Equal(pushes, new LandedBranches(_home).Landing("s1")!.Pushed);
        if (pushes)
        {
            Assert.Single(_frames);
            Assert.Null(pressed.Withheld);
            return;
        }

        Assert.Empty(_frames);
        Assert.NotNull(pressed.Withheld);
        Assert.Contains("its push was not tried", pressed.Message);
        Assert.Contains("`daoris-driver trees hand s1`", pressed.Message);
        Assert.Equal(1, TreesWords.Land(pressed, []).Code);
    }

    /// <summary>
    /// An entry's Accept at the push: its first part's result moves while that part's push waits for its branch. Its branch stays made
    /// with no plugin told, and the part after it is not tried, since its push would carry the first part's work on the entry's one
    /// branch; the press says it did not land all it was asked to.
    /// </summary>
    [Fact]
    public async Task An_entrys_part_whose_result_moved_at_its_push_keeps_its_branch_and_stops_the_parts_after_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin));
        InstallManifest();
        var first = await DoneSessionAsync(root, "s1", "q1", entry: "ask:a3/engine", submitted: true);
        var second = await DoneSessionAsync(root, "s2", "q2", entry: "ask:a3/engine", submitted: true);
        var reads = new PressQuests(_world.QuestAsync);
        EntryLandingPart[] parts =
        [
            new("s1", first) { Pressed = await reads.PressedAsync("s1", "q1", late: true) },
            new("s2", second) { Pressed = await reads.PressedAsync("s2", "q2", late: true) },
        ];

        Task<EntryLanding> landing;
        using (var pushing = await LandingHolds.BranchAsync(_home, root, PushBranch))
        {
            Assert.NotNull(pushing);
            using (ExecutionContext.SuppressFlow())
            {
                landing = Task.Run(() => new SessionTrees(_home, Plugins()).LandEntryAsync(
                    parts, new LandingSubject("s1", "q1", "Fix the gap"), new HashSet<string>(StringComparer.Ordinal)));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, PushBranch) > 0 || landing.IsCompleted,
                TimeSpan.FromSeconds(60)), "the first part's push waits for its branch");
            _world.Quests["q1"] = _world.Quests["q1"] with { Submission = null };
        }

        var landed = await landing;

        Assert.False(landed.Landed, landed.Message);
        Assert.Equal([EntryPartOutcome.Landed, EntryPartOutcome.NotTried], landed.Parts.Select(part => part.Outcome));
        Assert.NotNull(landed.Parts[0].Landing!.Withheld);
        Assert.Empty(_frames);
        Assert.Equal("1", (await GitAsync(root, "rev-list", "--count", $"main..{PushBranch}")).Trim());
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);
        Assert.Contains("`s1`'s push was held back", landed.Message);
        Assert.Contains("`daoris-driver trees hand s1`", landed.Parts[0].Landing!.Message);
    }
}
