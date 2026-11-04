using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACCEPTALL1 (D167's 2026-10-15 amendment points 1 to 3): an entry's Accept lands its parts' work on one branch, in ledger order.
/// On the install one ask's entry held parts that ran alongside, each in a tree of its own, and each offered its own Accept, so the
/// person pressed once per part and the earlier attempt at the same fix became a branch of its own. Now the entry's one press lands
/// the first part on the branch named for its first quest and replays each later part's commits onto it; a part a later part redid
/// is named in the plan and left out only where the person chose; a conflict with the entry's earlier work is refused naming both
/// parts. Real git in a scratch repository, so this is the suite's Process half; nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class EntryLandingTests : LandedFixture
{
    private const string Branch = "feature/0fda18-fix-the-gap";

    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    /// <summary>The row's proof: two parts that ran alongside, in trees of their own from the line, land on one branch in order.</summary>
    [Fact]
    public async Task Two_alongside_parts_land_on_one_branch_in_ledger_order()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "a.txt", "a\n", "the first part");
        await CommitAsync(second.Path, "b.txt", "b\n", "the second part");
        EntryLandingPart[] parts = [Part("s1a2b3c4", first.Path), Part("s5e6f7a8", second.Path)];

        var plan = await trees.PlanEntryAsync(parts, Subject);

        Assert.Equal((LandingForm.Branch, Branch), (plan.Form, plan.Target));
        Assert.Equal([Branch, Branch], plan.Parts.Select(part => part.Plan!.Target));
        Assert.All(plan.Parts, part => Assert.Null(part.Superseded));

    }

    /// <summary>The install's case: an earlier part's attempt at a fix a later part redid conflicts after it, and the plan says so.</summary>
    [Fact]
    public async Task A_superseded_part_is_named_and_left_out_only_by_the_persons_choice()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "app.txt", "one\n", "the app");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "app.txt", "the first attempt\n", "an attempt at the fix");
        await CommitAsync(second.Path, "app.txt", "the fix\n", "the fix");
        EntryLandingPart[] parts = [Part("s1a2b3c4", first.Path), Part("s5e6f7a8", second.Path)];

        var plan = await trees.PlanEntryAsync(parts, Subject);

        var superseded = plan.Parts[0].Superseded!;
        Assert.Equal(("s5e6f7a8", true), (superseded.By, superseded.Conflicts));
        Assert.Contains("app.txt", superseded.Says);
        Assert.Null(plan.Parts[1].Superseded);
    }

    /// <summary>
    /// ACCEPTCONFLICT1, the install's case: two parts add files of their own and edit one line of a shared file. They meet only in that
    /// line, so the earlier part has work the line would keep: said as a clash naming the file, never superseded.
    /// </summary>
    [Fact]
    public async Task Parts_that_add_their_own_files_and_meet_in_one_line_are_a_clash_and_not_superseded()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "brief.txt", "line\n", "the brief");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "records.txt", "records\n", "the records");
        await CommitAsync(first.Path, "brief.txt", "line, as the records say\n", "the brief, for the records");
        await CommitAsync(second.Path, "quest.txt", "quest\n", "the quest");
        await CommitAsync(second.Path, "brief.txt", "line, as the quest says\n", "the brief, for the quest");
        EntryLandingPart[] parts = [Part("s1a2b3c4", first.Path), Part("s5e6f7a8", second.Path)];

        var plan = await trees.PlanEntryAsync(parts, Subject);

        Assert.Null(plan.Parts[0].Superseded);
        var clash = plan.Parts[0].Clash!;
        Assert.Equal(("s5e6f7a8", "brief.txt"), (clash.By, clash.Says));
        Assert.Null(plan.Parts[1].Clash);
    }

    /// <summary>A part whose work a later part already holds adds nothing after it: named superseded, with its files.</summary>
    [Fact]
    public async Task A_part_that_adds_nothing_after_a_later_part_is_named_with_its_files()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "fix.txt", "the fix\n", "the fix, first");
        await CommitAsync(second.Path, "fix.txt", "the fix\n", "the fix, again");
        await CommitAsync(second.Path, "notes.txt", "notes\n", "the notes");
        EntryLandingPart[] parts = [Part("s1a2b3c4", first.Path), Part("s5e6f7a8", second.Path)];

        var plan = await trees.PlanEntryAsync(parts, Subject);

        Assert.Equal(new PartSuperseded("s5e6f7a8", "fix.txt", Conflicts: false), plan.Parts[0].Superseded);
    }

    /// <summary>After the entry's branch was squash-merged, its next work makes a new branch, still named for its first quest.</summary>
    [Fact]
    public async Task After_the_entrys_branch_merged_its_next_work_lands_on_a_new_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "a.txt", "a\n", "the first part");
        var once = await trees.LandAsync(first.Path, await trees.JoinEntryAsync(first.Path, "s1a2b3c4", Subject), joins: true, gate: Lands);
        Assert.True(once.Landed, once.Message);
        await SquashAsync(root, Branch);
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(second.Path, "b.txt", "b\n", "the second part");
        EntryLandingPart[] parts = [Part("s5e6f7a8", second.Path)];

        var plan = await trees.PlanEntryAsync(parts, Subject);

        Assert.Equal($"{Branch}-2", plan.Target);
    }

    /// <summary>
    /// ACCEPTALL1b (D167's amendment point 2): a part's own Accept, in its review, is its entry's Accept for that part alone. Two parts
    /// that ran alongside, each accepted from its own review, land on the entry's one branch: the second joins it as the entry's press would.
    /// </summary>
    [Fact]
    public async Task An_alongside_parts_own_accept_advances_the_entrys_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "a.txt", "a\n", "the first part");
        await CommitAsync(second.Path, "b.txt", "b\n", "the second part");

        var lead = await trees.JoinEntryAsync(first.Path, "s1a2b3c4", Subject);
        var once = await trees.LandAsync(first.Path, lead, joins: true, gate: Lands);
        Assert.True(once.Landed, once.Message);
        Assert.Equal(Branch, once.Branch);

        var joined = await trees.JoinEntryAsync(second.Path, "s5e6f7a8", Subject);
        Assert.Equal(("s5e6f7a8", Branch), (joined.Session, joined.EntryBranch));
        Assert.Equal(Branch, (await trees.PlanAsync(second.Path, joined)).Target);

        var landed = await trees.LandAsync(second.Path, joined, joins: true, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(Branch, landed.Branch);
        Assert.NotNull(landed.AdvancedFrom);
        Assert.Contains("since its work ran alongside its entry's earlier parts", landed.Message);
        Assert.Equal("the second part\nthe first part", (await GitAsync(root, "log", "--format=%s", $"main..{Branch}")).Trim());
        var tip = (await GitAsync(root, "rev-parse", Branch)).Trim();
        Assert.Equal(tip, (await GitAsync(second.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal(tip, trees.Recorded.Of("engine", Branch)!.Tip);
    }

    /// <summary>
    /// ACCEPTALL1f (D167's amendment point 2): every door that lands one session (the review's, the terminal's <c>trees land</c>, the look's
    /// at done) names it by <see cref="SessionTrees.OwnSubjectAsync"/> over the service's records. Pressed in either order, an entry's
    /// parts land on its one branch: the later part, pressed first, makes it under the first part's name, and the first part's own
    /// landing joins it; each landing records its own session.
    /// </summary>
    [Fact]
    public async Task Each_parts_own_landing_by_the_doors_one_call_lands_on_the_entrys_branch_in_either_order()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "a.txt", "a\n", "the first part");
        await CommitAsync(second.Path, "b.txt", "b\n", "the second part");
        IReadOnlyList<SessionRecord> records =
        [
            new("s5e6f7a8", "engine", "completed") { Entry = "ask:a3/engine", Quest = "1b2c3d", Tree = second.Path, Created = At(2) },
            new("s1a2b3c4", "engine", "completed") { Entry = "ask:a3/engine", Quest = "0fda18", Tree = first.Path, Created = At(1) },
        ];
        static Task<QuestView?> Find(string id) => Task.FromResult<QuestView?>(id switch
        {
            "0fda18" => new QuestView("0fda18", "game", "engine", "Fix the gap", "Fix it.", "Done"),
            "1b2c3d" => new QuestView("1b2c3d", "game", "engine", "Check the gap", "Check it.", "Done"),
            _ => null,
        });
        Task<LandingSubject> OwnAsync(string tree, string session, string quest) =>
            trees.OwnSubjectAsync(tree, session, quest, records, Find, _ => null, process: null);

        var later = await OwnAsync(second.Path, "s5e6f7a8", "1b2c3d");
        var once = await trees.LandAsync(second.Path, later, joins: later.Joins, gate: Lands);
        Assert.True(once.Landed, once.Message);
        Assert.Equal(Branch, once.Branch);

        var lead = await OwnAsync(first.Path, "s1a2b3c4", "0fda18");
        var landed = await trees.LandAsync(first.Path, lead, joins: lead.Joins, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(Branch, landed.Branch);
        Assert.NotNull(landed.AdvancedFrom);
        Assert.Equal("the first part\nthe second part", (await GitAsync(root, "log", "--format=%s", $"main..{Branch}")).Trim());
        var record = trees.Recorded.Of("engine", Branch)!;
        Assert.Equal("s5e6f7a8", record.Session);
        Assert.Equal("s1a2b3c4", Assert.Single(record.Advances).Session);
    }

    /// <summary>
    /// ACCEPTFOOT2 (D164's 2026-10-16 note): a conversation's entry is named for its first part's work, never for the tree a part's own
    /// Accept is pressed in, so parts whose oldest commits differ still land on the entry's one branch, in either order.
    /// </summary>
    [Fact]
    public async Task A_conversations_parts_land_on_the_branch_named_for_its_first_parts_work()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "a.txt", "a\n", "Fix parser");
        await CommitAsync(second.Path, "b.txt", "b\n", "Fix renderer");
        IReadOnlyList<SessionRecord> records =
        [
            new("s5e6f7a8", "engine", "working") { Entry = "chat:c1/engine", Kind = "chat", Tree = second.Path, Created = At(2) },
            new("s1a2b3c4", "engine", "working") { Entry = "chat:c1/engine", Kind = "chat", Tree = first.Path, Created = At(1) },
        ];
        Task<LandingSubject> OwnAsync(string tree, string session) => trees.OwnSubjectAsync(
            tree, session, null, records, _ => Task.FromResult<QuestView?>(null), _ => "ok so that is merged now", process: null);
        const string named = "feature/s1a2b3c4-fix-parser";

        var later = await OwnAsync(second.Path, "s5e6f7a8");
        Assert.Equal(named, (await trees.PlanAsync(second.Path, later)).Target);
        var once = await trees.LandAsync(second.Path, later, joins: later.Joins, gate: Lands);
        Assert.True(once.Landed, once.Message);
        Assert.Equal(named, once.Branch);

        var lead = await OwnAsync(first.Path, "s1a2b3c4");
        var landed = await trees.LandAsync(first.Path, lead, joins: lead.Joins, gate: Lands);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(named, landed.Branch);
    }

    private static DateTimeOffset At(int minute) => new(2026, 10, 16, 9, minute, 0, TimeSpan.Zero);

    /// <summary>
    /// ACCEPTALL1g (D167's amendment point 2, D145 §3): the plan says whether the branch Accept lands on already stands. Before the
    /// entry's first landing its branch is one the press makes; after it, the next part's plan names the same branch, standing, which
    /// its press moves on, so the review stops promising a new branch there.
    /// </summary>
    [Fact]
    public async Task The_plan_says_the_entrys_branch_already_stands_once_a_part_landed_on_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "a.txt", "a\n", "the first part");
        await CommitAsync(second.Path, "b.txt", "b\n", "the second part");

        var lead = await trees.JoinEntryAsync(first.Path, "s1a2b3c4", Subject);
        Assert.False((await trees.PlanAsync(first.Path, lead)).Stands);
        var once = await trees.LandAsync(first.Path, lead, joins: true, gate: Lands);
        Assert.True(once.Landed, once.Message);

        var plan = await trees.PlanAsync(second.Path, await trees.JoinEntryAsync(second.Path, "s5e6f7a8", Subject));

        Assert.Equal((Branch, true), (plan.Target, plan.Stands));
    }

    /// <summary>
    /// ACCEPTALL1d, with ACCEPTALL1b's join: under a squash rule a part's own later work, grown from the tip its first landing squashed,
    /// still advances by its own tip, with nothing replayed, as a chain's later step under a squash does.
    /// </summary>
    [Fact]
    public async Task Under_a_squash_rule_a_parts_later_work_advances_by_its_own_tip()
    {
        Squashing();
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "a.txt", "a\n", "the first part");
        var lead = await trees.JoinEntryAsync(first.Path, "s1a2b3c4", Subject);
        var once = await trees.LandAsync(first.Path, lead, joins: true, gate: Lands);
        Assert.True(once.Landed, once.Message);
        await CommitAsync(first.Path, "a2.txt", "a2\n", "more of the first part");
        var tip = (await GitAsync(first.Path, "rev-parse", "HEAD")).Trim();

        var again = await trees.LandAsync(first.Path, await trees.JoinEntryAsync(first.Path, "s1a2b3c4", Subject), joins: true, gate: Lands);

        Assert.True(again.Landed, again.Message);
        Assert.Contains("1 more commit(s), squashed into one", again.Message);
        Assert.DoesNotContain("replayed", again.Message);
        Assert.Equal(tip, trees.Recorded.Of("engine", Branch)!.Squashed);
        Assert.Equal(tip, (await GitAsync(first.Path, "rev-parse", "HEAD")).Trim());
    }

    /// <summary>Each landing squashed into one commit (PROCESS1b-ii), its branch named as the plain landing names it.</summary>
    private void Squashing() => Lands = GraphLand.Gate(Home, GraphLand.Branch("feature/{quest}-{slug}", squash: true));

    /// <summary>An entry's part that lands by the run's bound <c>land</c>.</summary>
    private EntryLandingPart Part(string session, string tree) => new(session, tree) { Gate = Lands };
}
