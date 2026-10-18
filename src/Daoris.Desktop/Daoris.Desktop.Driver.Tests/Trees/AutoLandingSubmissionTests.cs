using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1h: the landing on an unheld submission (the fabric design §E5, D173's note), split from the one class by PROCSPLIT1:
/// a bound quest's submission is due at the agent's <c>ready</c>, a hold or a reopen racing the landing stops it before anything is
/// made, and work that goes on after a landing moves its branch on.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AutoLandingSubmissionTests : AutoLandingFixture
{
    /// <summary>
    /// The row's proof. In a closure graph Daoris writes no Done before the graph's end, so the landing starts at the agent's
    /// <c>ready</c>: a bound quest's unheld submission is due at its conclusion and lands its branch at the look, the quest still taken,
    /// and a held one lands nothing.
    /// </summary>
    [Fact]
    public async Task A_bound_quests_unheld_submission_lands_its_branch_and_a_held_one_does_not()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1", submitted: true);
        await DoneSessionAsync(root, "s2", "q2", held: true, submitted: true);

        var said = await LookAsync();

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"), string.Join("\n", said));
        Assert.False(await BranchAsync(root, "feature/q2-fix-the-gap"), "a held submission lands nothing");
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(AutoLandingCode.Held, new AutoLandings(_home).Of("s2")!.Last!.Code);
        Assert.Equal(AcceptedBy.Auto, new LandedBranches(_home).Landing("s1")!.AcceptedBy);
    }

    /// <summary>
    /// Each hold the service computes on a submission holds its landing as it holds a done's (§E5: departed, evidence unread or missing),
    /// said once; its release lets the work go on, and it lands with no Done written.
    /// </summary>
    [Theory]
    [InlineData(EvidenceCodes.Departed)]
    [InlineData(EvidenceCodes.Unread)]
    [InlineData(EvidenceCodes.MissingHold)]
    public async Task A_held_submission_waits_and_lands_at_the_look_after_its_release_with_no_done(string hold)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await DoneSessionAsync(root, "s1", "q1", held: true, submitted: true);
        _world.Quests["q1"] = _world.Quests["q1"] with { Hold = hold };

        Assert.Single(await LookAsync());
        Assert.Empty(await LookAsync());
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(("Taken", 1L), (_world.Quests["q1"].Status, _world.Quests["q1"].Submission!.Sequence));
    }

    /// <summary>
    /// A hold racing the landing (the pre-mortem's first trap): the landing is judged again once it holds its tree, since LANDRACE1's wait
    /// for another landing of the tree may be long. The person's hold, a reopen of the submission, or a later <c>ready</c> that came while
    /// it waited stops it before anything is made: no branch, no push, no record. A hold on a plain done that waited is judged again too.
    /// A later <c>ready</c> keeps no try, so the next look judges the new submission from the start, and lands it.
    /// </summary>
    [Theory]
    [InlineData("held-done")]
    [InlineData("held")]
    [InlineData("reopened")]
    [InlineData("replaced")]
    public async Task A_hold_or_reopen_while_the_landing_waits_for_its_tree_stops_it_before_anything_is_made(string change)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: change != "held-done");
        var lander = Lander();
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Single(chosen);

        Task<IReadOnlyList<string>> landing;
        using (var holding = await LandingHolds.TakeAsync(_home, tree))
        {
            Assert.NotNull(holding);
            using (ExecutionContext.SuppressFlow())
            {
                landing = Task.Run(() => lander.LandAsync(chosen));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, tree) > 0, Poll.Patience), "the landing waits for its tree");
            var quest = _world.Quests["q1"];
            _world.Quests["q1"] = change switch
            {
                "reopened" => quest with { Submission = null },
                "replaced" => quest with { Submission = Made(2) },
                _ => quest with { Held = true, Hold = EvidenceCodes.Departed },
            };
        }

        var said = await landing;

        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), string.Join("\n", said));
        Assert.Empty(_frames);
        Assert.Null(new LandedBranches(_home).Landing("s1"));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(change switch { "reopened" => AutoLandingCode.Undone, "replaced" => null, _ => AutoLandingCode.Held }, entry.Last?.Code);
        if (change != "replaced") return;

        Assert.Null(entry.Closed);
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Single(_frames);
    }

    /// <summary>
    /// The reverse order, against the landing's commitment point (the parent's default, D173's note): a hold that comes once the landing
    /// was judged again under its tree's hold finds it going on. The branch is made and pushed, once; the hold applies to what follows,
    /// and nothing already pushed is taken back or pushed again.
    /// </summary>
    [Fact]
    public async Task A_hold_after_the_landing_was_judged_under_its_hold_applies_to_what_follows_and_retracts_nothing()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1", submitted: true);
        _landing = () =>
        {
            _landing = null;
            _world.Quests["q1"] = _world.Quests["q1"] with { Held = true, Hold = EvidenceCodes.Departed };
        };

        await LookAsync();

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Single(_frames);
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.Landed, entry.Last!.Code);
        Assert.NotNull(entry.Closed);
        Assert.Empty((await Lander().ChooseAsync(Config())).Chosen);
        Assert.Single(_frames);
    }

    /// <summary>
    /// A landing never implies completion (the pre-mortem's fourth trap): the branch is made and its push fails. The quest stays taken at
    /// its submission, since a landing writes no quest state (D46), the push's way on stays the person's hand-off, and a restart's look
    /// lands nothing twice and keeps the landing's record.
    /// </summary>
    [Fact]
    public async Task A_submissions_landing_whose_push_fails_leaves_the_quest_taken_and_a_restart_lands_nothing_twice()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1", submitted: true);

        var line = Assert.Single(await LookAsync(pushes: false));

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"), line);
        Assert.Equal(AutoLandingCode.PluginFailed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(("Taken", 1L), (_world.Quests["q1"].Status, _world.Quests["q1"].Submission!.Sequence));
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);

        // A restart: a new lander over the same home.
        Assert.Empty((await Lander().ChooseAsync(Config())).Chosen);
        Assert.Single(_frames);
        Assert.Equal("feature/q1-fix-the-gap", new LandedBranches(_home).Landing("s1")!.Branch);
    }

    /// <summary>
    /// FABRIC-CLOSE1h6 (D173's FABRIC-CLOSE1h note, *Enrolled and kept due*): a reopen takes the submission back and the look closes the
    /// entry <c>undone</c>; the agent's next <c>ready</c> makes its session due again, and the next look judges it afresh and lands it,
    /// the earlier try kept.
    /// </summary>
    [Fact]
    public async Task A_submission_reopened_and_readied_again_lands_at_the_next_look()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: true);
        _world.Quests["q1"] = _world.Quests["q1"] with { Submission = null };
        Assert.Single(await LookAsync());
        Assert.Equal(AutoLandingCode.Undone, new AutoLandings(_home).Of("s1")!.Last!.Code);

        await CommitAsync(tree, "more.txt", "the work after the reopen", "the work after the reopen");
        _world.Quests["q1"] = _world.Quests["q1"] with { Submission = Made(2) };
        ConcludeAgain("s1", "q1", tree);
        var said = await LookAsync();

        Assert.True(await BranchAsync(root, PushBranch), string.Join("\n", said));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal([AutoLandingCode.Undone, AutoLandingCode.Landed], entry.Tries.Select(each => each.Code));
        Assert.Equal(2, entry.Last!.Commits);
        Assert.NotNull(entry.Closed);
        Assert.Equal(("Taken", 2L), (_world.Quests["q1"].Status, _world.Quests["q1"].Submission!.Sequence));
    }

    /// <summary>
    /// FABRIC-CLOSE1h6 (D137, LAND2c): the ordinary automatic landing, no closure graph. A session landed, words went on with it, and it
    /// concluded on its done again with more work: the next look moves the branch its landing made on, its plugin told once at the
    /// tree's tip, kept as <c>advanced</c> beside the first try. Concluded once more with nothing new, it lands nothing twice.
    /// </summary>
    [Fact]
    public async Task A_done_after_words_went_on_past_its_landing_moves_the_made_branch_on_with_one_push()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Assert.Single(await LookAsync());
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Single(_frames);

        await CommitAsync(tree, "more.txt", "the work after its landing", "the work after its landing");
        var head = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        ConcludeAgain("s1", "q1", tree);
        var said = await LookAsync();

        Assert.True(_frames.Count == 2, string.Join("\n", said));
        Assert.Equal(head, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Equal(head, Json(_frames[1]).GetProperty("tip").GetString());
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal([AutoLandingCode.Landed, AutoLandingCode.Advanced], entry.Tries.Select(each => each.Code));
        Assert.NotNull(entry.Closed);
        Assert.Equal("s1", Assert.Single(new LandedBranches(_home).Landing("s1")!.Advances).Session);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));

        // Concluded again with nothing new: judged afresh, its work is on the branch already, so nothing lands twice.
        ConcludeAgain("s1", "q1", tree);
        Assert.Single(await LookAsync());
        Assert.Equal(2, _frames.Count);
        Assert.Equal(AutoLandingCode.Already, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(head, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
    }
}
