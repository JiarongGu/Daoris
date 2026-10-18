using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The push held back and its recovery, split from the one class by PROCSPLIT1 (FABRIC-CLOSE1h2, h8 and h10, D173's note): a hold
/// between a landing's branch and its push leaves the branch made and the entry <c>push-held</c>, and the hand-off or the look's
/// hand-on that pushes it later is judged on the session that last moved the branch, under the branch's hold.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AutoLandingPushHeldTests : AutoLandingFixture
{
    // ——— FABRIC-CLOSE1h2: a hold between a landing's branch and its push (D173's note)

    /// <summary>
    /// The row's proof. The automatic landing is judged once more just before its plugin is told, since the owner's default (D173's
    /// FABRIC-CLOSE1h note) is that a hold stops a landing up to the moment its push begins. A hold that comes while the push waits
    /// behind another push of its branch leaves the branch made and recorded and tells the plugin nothing: the entry stays open as
    /// <c>push-held</c>, says nothing more while the hold stands, and the first look after the release hands the branch on, one push,
    /// never a second branch. A plain done is judged there only where its push waited; a submission every time.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hold_while_the_push_waits_for_its_branch_leaves_the_branch_made_and_pushes_it_once_after_the_release(bool submitted)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1", submitted: submitted);
        var said = await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });

        Assert.True(await BranchAsync(root, PushBranch), string.Join("\n", said));
        Assert.Empty(_frames);
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.PushHeld, entry.Last!.Code);
        Assert.Null(entry.Closed);
        Assert.Contains("nothing was pushed", Assert.Single(said));

        // While the hold stands, a look hands nothing on and says nothing more.
        Assert.Empty(await LookAsync());
        Assert.Empty(_frames);

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        var handed = Assert.Single(await LookAsync());

        var frame = Json(Assert.Single(_frames));
        Assert.Equal(PushBranch, frame.GetProperty("branch").GetString());
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed, handed);
        entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.Landed, entry.Last!.Code);
        Assert.NotNull(entry.Closed);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));
        Assert.Empty((await Lander().ChooseAsync(Config())).Chosen);
        Assert.Single(_frames);
    }

    /// <summary>
    /// A reopen that comes as the push waits holds it the same way, and the work that goes on after it is not lost to a hand-off of the
    /// branch as it was made: once the quest stands at a later submission and its tree has moved past the commit landed, the look lands
    /// it as any later done of the session lands, moving the made branch on, and its plugin is told once, at the tree's tip.
    /// </summary>
    [Fact]
    public async Task A_reopen_while_the_push_waits_holds_it_and_the_work_that_went_on_moves_the_made_branch_with_one_push()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1", submitted: true);
        await HeldAtThePushAsync(root, quest => quest with { Submission = null });

        Assert.True(await BranchAsync(root, PushBranch));
        Assert.Empty(_frames);
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s1")!.Last!.Code);

        // Taken back: a look waits for its next submission, and hands nothing on.
        Assert.Empty(await LookAsync());
        Assert.Empty(_frames);

        await CommitAsync(tree, "more.txt", "the work after the reopen", "the work after the reopen");
        var head = (await GitAsync(tree, "rev-parse", "HEAD")).Trim();
        _world.Quests["q1"] = _world.Quests["q1"] with { Submission = Made(2) };
        var said = await LookAsync();

        var frame = Json(Assert.Single(_frames));
        Assert.Equal(head, frame.GetProperty("tip").GetString());
        Assert.Equal(head, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Equal(AutoLandingCode.Advanced, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed, string.Join("\n", said));
    }

    /// <summary>
    /// FABRIC-CLOSE1h8 (D173's note): a person's hand-off is judged again under its branch's hold, just before its plugin is told, against
    /// the quest of the session that last moved the branch (the parent's decision 1). s2's done moved s1's branch on; the hand-off waits
    /// behind another push of it while s2's submission is reopened, replaced, readied again at the same commit or readied on another
    /// machine: nothing is pushed, no plugin is told, nothing is recorded, and the answer names `trees hand s2`; the verb exits 1. A hold
    /// on s2's quest does not stop it, since the hand-off is the person's own word (decision 2), nor does s1's result moving, which is
    /// not the branch's last.
    /// </summary>
    [Theory]
    [InlineData("reopened", false)]
    [InlineData("replaced", false)]
    [InlineData("same-commit", false)]
    [InlineData("elsewhere", false)]
    [InlineData("held", true)]
    [InlineData("first-reopened", true)]
    public async Task A_hand_off_waiting_for_its_push_tells_no_plugin_where_the_last_result_on_its_branch_moved(string change, bool pushes)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var first = await DoneSessionAsync(root, "s1", "q1", submitted: true);
        Assert.Single(await LookAsync(pushes: false));
        await DoneSessionAsync(root, "s2", "q2", parent: "q1", from: $"daoris/{Path.GetFileName(first)}", submitted: true);
        Assert.Single(await LookAsync(pushes: false));
        var recorded = new LandedBranches(_home).Of("engine", PushBranch)!;
        Assert.Equal(("s1", "s2"), (recorded.Session, recorded.LastSession()));
        Assert.False(recorded.Pushed);
        lock (_frames) _frames.Clear();
        _pushes = true;

        // As the doors make it: the branch's last session's quest, read where the door names the branch.
        var pressed = await PressQuests.HandedAsync(recorded, SessionQuestAsync, _world.QuestAsync);
        Task<TreeHand> hand;
        using (var pushing = await LandingHolds.BranchAsync(_home, root, PushBranch))
        {
            Assert.NotNull(pushing);
            using (ExecutionContext.SuppressFlow())
            {
                hand = Task.Run(() => new SessionTrees(_home, Plugins()).HandAsync(root, recorded, beforePush: pressed.BeforePush));
            }

            Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, PushBranch) > 0 || hand.IsCompleted,
                TimeSpan.FromSeconds(60)), "the hand-off waits for its branch");
            var quest = change == "first-reopened" ? "q1" : "q2";
            var was = _world.Quests[quest];
            _world.Quests[quest] = change switch
            {
                "reopened" or "first-reopened" => was with { Submission = null },
                "replaced" => was with { Submission = Made(2) with { Commit = "a later commit" } },
                "same-commit" => was with { Submission = Made(2) },
                "elsewhere" => was with { Submission = new QuestSubmissionView("laptop", 1, DateTimeOffset.UtcNow, "ready") },
                _ => was with { Held = true, Hold = EvidenceCodes.Departed },
            };
        }

        var handed = await hand;

        Assert.Equal(pushes, handed.Handed);
        Assert.Equal(pushes, new LandedBranches(_home).Of("engine", PushBranch)!.Pushed);
        Assert.True(await BranchAsync(root, PushBranch));
        if (pushes)
        {
            Assert.Single(_frames);
            Assert.Null(handed.Withheld);
            return;
        }

        Assert.Empty(_frames);
        Assert.Null(handed.Plugin);
        Assert.NotNull(handed.Withheld);
        Assert.Contains("its push was not tried", handed.Message);
        Assert.Contains("`daoris-driver trees hand s2`", handed.Message);
        Assert.Equal(1, TreesWords.Hand(handed, []).Code);
    }

    /// <summary>
    /// FABRIC-CLOSE1h8: the look's own hand-off of a push held back keeps the look's rule (the parent's decision 3). Released, the look
    /// hands the made branch on; that hand-off waits behind another push of the branch while the quest is held again: nothing is pushed,
    /// no plugin is told, and the entry stays <c>push-held</c>, not refused, so the first look after the release hands it on, once.
    /// </summary>
    [Fact]
    public async Task The_looks_hand_off_held_again_while_it_waits_for_its_push_stays_push_held_and_pushes_once_after_the_release()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1", submitted: true);
        await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s1")!.Last!.Code);

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        var said = await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });

        Assert.Empty(_frames);
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.PushHeld, entry.Last!.Code);
        Assert.Null(entry.Closed);
        Assert.Contains("nothing was pushed", Assert.Single(said));

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        Assert.Single(await LookAsync());

        Assert.Single(_frames);
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed);
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));
    }

    // ——— FABRIC-CLOSE1h10: a hand-off judged on the session that moved its branch while it waited (D173's note)

    /// <summary>
    /// The row's proofs 2 and 3: a person's hand-off whose door read the branch with s1 last, and whose record s2's done moved on before
    /// it held the branch, is judged on s2's quest as the record reads under the branch's hold. s2's result taken back holds the push
    /// back: no plugin is told, nothing is recorded, the answer names `trees hand s2` and the verb exits 1. s2's result standing, held or
    /// not (a hold does not stop the person's own word), pushes the branch once, at its tip.
    /// </summary>
    [Theory]
    [InlineData("reopened", false)]
    [InlineData("held", true)]
    [InlineData("standing", true)]
    public async Task A_hand_off_is_judged_on_the_session_that_moved_its_branch_while_it_waited(string change, bool pushes)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var first = await DoneSessionAsync(root, "s1", "q1", submitted: true);
        Assert.Single(await LookAsync(pushes: false));
        // The door reads the branch with s1 last; s2's done moves it on before the hand-off holds it, its own push not made.
        var door = new LandedBranches(_home).Of("engine", PushBranch)!;
        Assert.Equal("s1", door.LastSession());
        var pressed = await PressQuests.HandedAsync(door, SessionQuestAsync, _world.QuestAsync);
        await DoneSessionAsync(root, "s2", "q2", parent: "q1", from: $"daoris/{Path.GetFileName(first)}", submitted: true);
        Assert.Single(await LookAsync(pushes: false));
        Assert.Equal("s2", new LandedBranches(_home).Of("engine", PushBranch)!.LastSession());
        var was = _world.Quests["q2"];
        _world.Quests["q2"] = change switch
        {
            "reopened" => was with { Submission = null },
            "held" => was with { Held = true, Hold = EvidenceCodes.Departed },
            _ => was,
        };
        lock (_frames) _frames.Clear();
        _pushes = true;

        var handed = await new SessionTrees(_home, Plugins()).HandAsync(root, door, beforePush: pressed.BeforePush);

        Assert.Equal(pushes, handed.Handed);
        Assert.Equal(pushes, new LandedBranches(_home).Of("engine", PushBranch)!.Pushed);
        Assert.True(await BranchAsync(root, PushBranch));
        if (pushes)
        {
            var frame = Json(Assert.Single(_frames));
            Assert.Equal((await GitAsync(root, "rev-parse", PushBranch)).Trim(), frame.GetProperty("tip").GetString());
            Assert.Null(handed.Withheld);
            return;
        }

        Assert.Empty(_frames);
        Assert.Null(handed.Plugin);
        Assert.NotNull(handed.Withheld);
        Assert.Contains("its push was not tried", handed.Message);
        Assert.Contains("`daoris-driver trees hand s2`", handed.Message);
        Assert.Equal(1, TreesWords.Hand(handed, []).Code);
    }

    /// <summary>
    /// The row's proof 1: the look's hand-on of a push held back is judged on the session that last moved the branch, by the look's own
    /// rule. s1's push was held back; s2's done moved the branch on and its own push was held back too. Released, s1's quest stands again
    /// while s2's was taken back, before the look chose the hand-on, or while that hand-on waited for the branch: no plugin is told of
    /// s2's work, nothing is pushed, and s1 stays <c>push-held</c>.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_looks_hand_on_never_pushes_a_later_sessions_work_whose_quest_was_taken_back(bool whileItWaits)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var first = await DoneSessionAsync(root, "s1", "q1", submitted: true);
        await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });
        await DoneSessionAsync(root, "s2", "q2", parent: "q1", from: $"daoris/{Path.GetFileName(first)}", submitted: true);
        await HeldAtThePushAsync(root, quest => whileItWaits
            ? quest with { Held = true, Hold = EvidenceCodes.Departed }
            : quest with { Submission = null }, quest: "q2");
        var recorded = new LandedBranches(_home).Of("engine", PushBranch)!;
        Assert.Equal(("s1", "s2", false), (recorded.Session, recorded.LastSession(), recorded.Pushed));
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s2")!.Last!.Code);
        Assert.Empty(_frames);

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        if (whileItWaits)
        {
            // Both stand as the look chooses; s2's is taken back as the hand-on waits for the branch.
            _world.Quests["q2"] = _world.Quests["q2"] with { Held = false, Hold = null };
            var said = await HeldAtThePushAsync(root, quest => quest with { Submission = null }, quest: "q2", choices: 2);
            Assert.Contains("`daoris-driver trees hand s2`", Assert.Single(said));
        }
        else
        {
            Assert.Empty(await LookAsync());
        }

        Assert.Empty(_frames);
        Assert.False(new LandedBranches(_home).Of("engine", PushBranch)!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.PushHeld, entry.Last!.Code);
        Assert.Null(entry.Closed);
    }
}
