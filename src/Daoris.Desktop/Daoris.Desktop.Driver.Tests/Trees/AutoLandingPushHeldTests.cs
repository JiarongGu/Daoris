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
                Poll.Patience), "the hand-off waits for its branch");
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

    // ——— FABRIC-CLOSE1h12: the landing's gates judged again at the push and in its recovery (D173's note)

    /// <summary>
    /// The row's proof at the push. A gate that starts holding after the look chose a landing, while its push waits behind another push
    /// of its branch, holds the push back as a hold does: a second opinion the repository's rule now reads before landing, or a set-up
    /// step showing the work for the person's review. The branch is made, no plugin is told, the entry stays <c>push-held</c> and a look
    /// while the gate holds says nothing more; once it lets go, the first look hands the branch on, one push.
    /// </summary>
    [Theory]
    [InlineData("opinion")]
    [InlineData("review")]
    public async Task A_gate_that_starts_holding_while_the_push_waits_holds_it_back_until_it_lets_go(string gate)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tip = await HeadAsync(await DoneSessionAsync(root, "s1", "q1"));
        var said = await HeldAtThePushAsync(root, () => Holds(gate, tip));

        Assert.True(await BranchAsync(root, PushBranch), string.Join("\n", said));
        Assert.Empty(_frames);
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.PushHeld, entry.Last!.Code);
        Assert.Null(entry.Closed);
        var line = Assert.Single(said);
        Assert.Contains("nothing was pushed", line);
        Assert.Contains("its landing's gate held it before its plugin was told, so its push was not tried", line);
        Assert.Contains(gate == "opinion" ? "Waits for a second opinion" : "Waits for your review in `local`", line);
        Assert.Single(_lines, each => each.Event == (gate == "opinion" ? "opinion.held" : "review.held"));

        // While the gate holds, a look hands nothing on and says nothing more.
        Assert.Empty(await LookAsync());
        Assert.Empty(_frames);

        LetsGo(gate, tip);
        Assert.Single(await LookAsync());

        Assert.Equal(PushBranch, Json(Assert.Single(_frames)).GetProperty("branch").GetString());
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed);
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal([$"refs/heads/{PushBranch}"], await FeatureBranchesAsync(root));
    }

    /// <summary>
    /// The row's proof in recovery. A push held back by its quest's hold is handed on by the first look once the quest stands (CLOSE1h2),
    /// after a restart as at any look, since a look keeps nothing but the due list. A gate that started holding before that look holds
    /// the hand-on too, read before it is chosen: the look waits, says nothing and tells no plugin, and the first look once the gate
    /// lets go pushes once.
    /// </summary>
    [Theory]
    [InlineData("opinion")]
    [InlineData("review")]
    public async Task A_gate_that_starts_holding_before_a_push_held_back_is_recovered_holds_its_hand_on_until_it_lets_go(string gate)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tip = await HeadAsync(await DoneSessionAsync(root, "s1", "q1"));
        await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s1")!.Last!.Code);

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        Holds(gate, tip);
        Assert.Empty(await LookAsync());

        Assert.Empty(_frames);
        Assert.False(new LandedBranches(_home).Landing("s1")!.Pushed);
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.PushHeld, entry.Last!.Code);
        Assert.Null(entry.Closed);
        // FABRIC-CLOSE1h15: the opinion holding it is owed there, for the loop's look to ask; a review that holds it owes none.
        Assert.Equal(gate == "opinion" ? 1 : 0, new OpinionDues(_home).Open().Count);

        LetsGo(gate, tip);
        Assert.Single(await LookAsync());

        Assert.Single(_frames);
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed);
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
    }

    // ——— FABRIC-CLOSE1h15: an opinion that holds a push held back is asked by the loop (D173's note)

    /// <summary>
    /// The row's proof. A push held back whose recovery finds a second opinion holding it that nobody asked (a rule that started reading
    /// after the done, so none was owed) makes one owed where it reads the gate, at the landing's occasion, as the person's ask would.
    /// The loop's opinion look asks it as any, and does not close it as landed while the push is held back; a look while it holds owes it
    /// once, not again. Once it settles, the first look pushes once, and the opinion look then closes it as landed.
    /// </summary>
    [Fact]
    public async Task A_push_held_back_by_an_opinion_nobody_asked_is_owed_where_its_gate_is_read_and_pushes_once_it_settles()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);
        await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });
        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        Holds("opinion", tip);
        Assert.Empty(new OpinionDues(_home).All());

        Assert.Empty(await LookAsync());

        Assert.Empty(_frames);
        var due = Assert.Single(new OpinionDues(_home).Open());
        Assert.Equal(("s1", "q1", "engine", "aurora", tree, OpinionRules.Landing),
            (due.Session, due.Quest, due.Repository, due.Workspace, due.Tree, due.Occasion));
        Assert.Null(await ClosesAsync(due));

        Assert.Empty(await LookAsync());
        Assert.Equal(due.At, Assert.Single(new OpinionDues(_home).All()).At);

        Settled("o1", tip);
        Assert.Single(await LookAsync());

        Assert.Single(_frames);
        Assert.True(new LandedBranches(_home).Landing("s1")!.Pushed);
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(OpinionDueClosed.Landed, await ClosesAsync(due));
    }

    /// <summary>
    /// The parent's decision 6: a set-up step's <c>unreviewed</c> hold is its review gate's own, so the quest's hold alone never holds the
    /// push back, and the gate read there does. q1 becomes a set-up step showing its work at the tip while the push waits: reviewed
    /// there, its quest still held, it pushes at once; not reviewed, the push is held back until the person's reviewed lets it go.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_set_up_steps_unreviewed_hold_at_the_push_is_its_review_gates_to_judge(bool reviewed)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var tip = await HeadAsync(await DoneSessionAsync(root, "s1", "q1"));
        var said = await HeldAtThePushAsync(root, quest => quest with
        {
            SetUpIn = "local", Held = true, Hold = EvidenceCodes.Unreviewed, SetUps = [Shown(tip, 41)],
            Verdicts = reviewed ? [Reviewed(tip, 41)] : [],
        });

        if (reviewed)
        {
            Assert.Single(_frames);
            Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
            return;
        }

        Assert.Empty(_frames);
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Contains("Waits for your review in `local`", Assert.Single(said));
        Assert.Empty(await LookAsync());
        Assert.Empty(_frames);

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null, Verdicts = [Reviewed(tip, 41)] };
        Assert.Single(await LookAsync());

        Assert.Single(_frames);
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
    }

    /// <summary>
    /// The parent's decision 4: where a later session's done moved the branch on (CLOSE1h10), the gate is read for that session's work,
    /// its quest and its tree, as its quest is. s1's push and s2's advance's were held back; both quests stand again, and q3, a set-up
    /// step after them, was reviewed at s1's tip, which does not hold s2's work: no look hands anything on, said or not, until the person
    /// reviews what q3 shows at s2's tip, and then the branch is pushed once, at its tip.
    /// </summary>
    [Fact]
    public async Task The_looks_hand_on_reads_the_gate_for_the_later_session_that_moved_its_branch()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        var first = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(first);
        await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed });
        var later = await HeadAsync(await DoneSessionAsync(root, "s2", "q2", parent: "q1", from: $"daoris/{Path.GetFileName(first)}"));
        await HeldAtThePushAsync(root, quest => quest with { Held = true, Hold = EvidenceCodes.Departed }, quest: "q2");
        var recorded = new LandedBranches(_home).Of("engine", PushBranch)!;
        Assert.Equal(("s1", "s2", false), (recorded.Session, recorded.LastSession(), recorded.Pushed));

        _world.Quests["q1"] = _world.Quests["q1"] with { Held = false, Hold = null };
        _world.Quests["q2"] = _world.Quests["q2"] with { Held = false, Hold = null };
        _world.Quests["q3"] = new QuestView("q3", "game", "engine", "Show #q2 in `local` for review", "", "Done")
        {
            Parent = "q2", SetUpIn = "local", SetUps = [Shown(tip, 41)], Verdicts = [Reviewed(tip, 41)],
        };
        Assert.Empty(await LookAsync());

        Assert.Empty(_frames);
        Assert.False(new LandedBranches(_home).Of("engine", PushBranch)!.Pushed);
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(AutoLandingCode.PushHeld, new AutoLandings(_home).Of("s2")!.Last!.Code);

        _world.Quests["q3"] = _world.Quests["q3"] with { SetUps = [Shown(tip, 41), Shown(later, 42)], Verdicts = [Reviewed(tip, 41), Reviewed(later, 42)] };
        await LookAsync();

        var frame = Json(Assert.Single(_frames));
        Assert.Equal((await GitAsync(root, "rev-parse", PushBranch)).Trim(), frame.GetProperty("tip").GetString());
        Assert.True(new LandedBranches(_home).Of("engine", PushBranch)!.Pushed);
    }

    /// <summary>
    /// The owner's install (the parent's decision 2): an uncontended landing of a plain done reads the gate no more at its push, since
    /// the gate is read again only where the quest is (LANDGIT1's counts). Its landing reads the quest list as often as the look's
    /// judgement did, which is the judgement just before the landing, and pushes once.
    /// </summary>
    [Fact]
    public async Task An_uncontended_plain_done_reads_no_gate_at_its_push()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Plugin, AutoAccept: true));
        InstallManifest();
        await DoneSessionAsync(root, "s1", "q1");
        var reads = 0;
        _world.ReadingQuests = () => Interlocked.Increment(ref reads);
        var lander = Lander();

        var (chosen, _) = await lander.ChooseAsync(Config());
        var looked = Interlocked.Exchange(ref reads, 0);
        await lander.LandAsync(chosen);

        Assert.True(looked > 0, "the look reads the quest list for its gate");
        Assert.Equal(looked, reads);
        Assert.Single(_frames);
    }

    /// <summary>
    /// The gate named starts holding: the repository's rule now reads a second opinion before landing, which nobody has asked yet; or q2,
    /// a set-up step after q1, shows its work at <paramref name="tip"/> for the person's review.
    /// </summary>
    private void Holds(string gate, string tip)
    {
        if (gate == "opinion")
        {
            (Config() with
            {
                Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
                {
                    ["engine"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], true),
                },
            }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
            return;
        }

        _world.Quests["q2"] = new QuestView("q2", "game", "engine", "Show #q1 in `local` for review", "", "Done")
        {
            Parent = "q1", SetUpIn = "local", Held = true, Hold = EvidenceCodes.Unreviewed, SetUps = [Shown(tip, 41)],
        };
    }

    /// <summary>The gate named lets go: the rule reads no opinion any more, or the person reviewed what q2 showed.</summary>
    private void LetsGo(string gate, string tip)
    {
        if (gate == "opinion")
        {
            (Config() with { Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase) })
                .Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
            return;
        }

        _world.Quests["q2"] = _world.Quests["q2"] with { Held = false, Hold = null, Verdicts = [Reviewed(tip, 41)] };
    }

    /// <summary>The reviewer read s1's work at <paramref name="tip"/> and raised nothing: the opinion, kept at the gate as asked, settles.</summary>
    private void Settled(string id, string tip)
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
    private Task<string?> ClosesAsync(OpinionDue due) =>
        OpinionLook.ClosesAsync(due, _world.Quests[due.Quest], [.. _world.Quests.Values], Config(), new LandedBranches(_home), CancellationToken.None,
            pushHeld: OpinionLook.PushHeld(_home));

    /// <summary>A set-up this machine posted at <paramref name="commit"/>, as its <paramref name="sequence"/>th.</summary>
    private static QuestSetUpView Shown(string commit, long sequence) =>
        new(commit) { Shows = "the work", Session = "s9", Local = true, Machine = "desk", Sequence = sequence };

    /// <summary>The person's reviewed of that set-up.</summary>
    private static QuestReviewVerdictView Reviewed(string commit, long sequence) =>
        new(ReviewVerdicts.Reviewed) { SetUpMachine = "desk", SetUpSequence = sequence, Commit = commit, At = DateTimeOffset.UtcNow };
}
