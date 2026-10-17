using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1p (the fabric events design §E5 step 1; D173's CLOSE1b note, "After review (FABRIC-CLOSE1l)", finding 5): a closing quest's
/// revision digests the tip git answers for each branch its chain worked, read with one snapshot per repository for one judgement, and a tip
/// git did not answer leaves the close waiting. The fast half: each snapshot's read is the test's, so no git starts.
/// </summary>
public sealed class CloseTipsTests
{
    private const string C1 = "1111111111111111111111111111111111111111";
    private const string C2 = "2222222222222222222222222222222222222222";
    private const string C3 = "3333333333333333333333333333333333333333";

    private static readonly CancellationToken None = CancellationToken.None;

    private static QuestView Taken(string id, string? closure = "gph-close") =>
        new(id, "game", "engine", "Ship " + id, "", "Taken") { Closure = closure, Submission = new QuestSubmissionView("desk", 41, null, "ready") };

    private static WorkUnit QuestUnit(QuestView quest) => new("quest:" + quest.Id, WorkUnitKind.Quest,
        new WorkOwner(quest.To, "default", WorkRecord.Quests, WorkRecord.ServiceWriter), new WorkSubject("#" + quest.Id, null),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []) { Records = new WorkUnitRecords(null, [], quest) };

    /// <summary>A session branch unit the quest's sessions worked, its recorded revision the kept end fact's (none while it is not ended).</summary>
    private static WorkUnit Branch(string branch, string quest, string repository = "engine", string? recorded = null) => new(
        $"branch:{repository}/{branch}", WorkUnitKind.Branch, new WorkOwner(repository, "default", SessionBranches.FileName, WorkRecord.DriverWriter),
        new WorkSubject(branch, recorded), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
    {
        Links = [new WorkLink(WorkRelation.ProducedBy, "quest:" + quest)],
    };

    private static Dictionary<string, QuestView> ById(params QuestView[] quests) => quests.ToDictionary(quest => quest.Id, StringComparer.Ordinal);

    /// <summary>A for-each-ref answer in <see cref="RefSnapshot.Format"/>.</summary>
    private static string Refs(params (string Branch, string Commit)[] heads) =>
        string.Join('\n', heads.Select(head => $"refs/heads/{head.Branch} {head.Commit} commit  ")) + "\n";

    [Fact]
    public void A_closing_quests_revision_moves_with_a_branch_tip_git_answered_and_its_record_alone_moves_nothing()
    {
        var q7 = Taken("q7");
        var quests = ById(q7);
        IReadOnlyList<WorkUnit> units = [QuestUnit(q7), Branch("daoris/s1", "q7"), Branch("daoris/s2", "q7")];
        var at = new Dictionary<string, string?> { ["branch:engine/daoris/s1"] = C1, ["branch:engine/daoris/s2"] = C2 };
        var moved = new Dictionary<string, string?>(at) { ["branch:engine/daoris/s2"] = C3 };

        Assert.Equal(QuestClosing.Revision(q7, quests, units, at), QuestClosing.Revision(q7, quests, units, new Dictionary<string, string?>(at)));
        Assert.NotEqual(QuestClosing.Revision(q7, quests, units, at), QuestClosing.Revision(q7, quests, units, moved));
        // A branch gone is a fact git answered, and moves it too.
        Assert.NotEqual(QuestClosing.Revision(q7, quests, units, at),
            QuestClosing.Revision(q7, quests, units, new Dictionary<string, string?>(at) { ["branch:engine/daoris/s2"] = "" }));
        // Without tips, the recorded revision as before: a seam that asks no git is judged on the records.
        Assert.Equal(QuestClosing.Revision(q7, quests, units), QuestClosing.Revision(q7, quests, units, null));
    }

    [Fact]
    public void A_branch_whose_tip_git_did_not_answer_leaves_the_close_waiting_naming_it()
    {
        var q7 = Taken("q7");
        var quests = ById(q7);
        IReadOnlyList<WorkUnit> units = [QuestUnit(q7), Branch("daoris/s1", "q7"), Branch("daoris/s2", "q7")];

        Assert.Null(QuestClosing.Unread(q7, quests, units, new Dictionary<string, string?> { ["branch:engine/daoris/s1"] = C1, ["branch:engine/daoris/s2"] = "" }));
        Assert.Equal("daoris/s2", QuestClosing.Unread(q7, quests, units, new Dictionary<string, string?> { ["branch:engine/daoris/s1"] = C1, ["branch:engine/daoris/s2"] = null }));
        Assert.Equal("daoris/s1", QuestClosing.Unread(q7, quests, units, new Dictionary<string, string?> { ["branch:engine/daoris/s2"] = C2 }));
        Assert.Null(QuestClosing.Unread(q7, quests, units, null));
    }

    [Fact]
    public async Task One_snapshot_per_repository_reads_every_branch_a_bound_chain_worked_there_and_nothing_else()
    {
        // q6 → q7, both here and bound; q5 here unbound; q9 bound in another repository.
        var q6 = Taken("q6") with { Then = [new QuestStepView("engine", "Ship q7", "")] };
        var q7 = Taken("q7") with { Parent = "q6" };
        var q5 = Taken("q5", closure: null);
        var q9 = Taken("q9") with { To = "ui" };
        IReadOnlyList<WorkUnit> units =
        [
            QuestUnit(q5), QuestUnit(q6), QuestUnit(q7), QuestUnit(q9),
            Branch("daoris/s1", "q6"), Branch("daoris/s2", "q7"), Branch("daoris/s3", "q7"), Branch("daoris/s5", "q5"), Branch("daoris/s9", "q9", "ui"),
        ];
        var taken = new List<string>();
        var reads = 0;
        RefSnapshot Take(string root)
        {
            taken.Add(root);
            return RefSnapshot.Take(_ =>
            {
                reads++;
                return Task.FromResult<string?>(root == "engine-checkout"
                    ? Refs(("daoris/s1", C1), ("daoris/s2", C2), ("daoris/s5", C3), ("main", C1))
                    : Refs(("daoris/s9", C3)));
            });
        }

        var tips = await QuestClosing.TipsAsync(units, new Dictionary<string, string> { ["engine"] = "engine-checkout", ["ui"] = "ui-checkout" }, None, Take);

        Assert.Equal(["engine-checkout", "ui-checkout"], taken.Order(StringComparer.Ordinal));
        Assert.Equal(2, reads);
        // s3 is not there: answered as gone. s5 served an unbound quest, so it is not read.
        Assert.Equal(new Dictionary<string, string?>
        {
            ["branch:engine/daoris/s1"] = C1, ["branch:engine/daoris/s2"] = C2, ["branch:engine/daoris/s3"] = "", ["branch:ui/daoris/s9"] = C3,
        }, tips);
    }

    [Fact]
    public async Task No_bound_quest_reads_no_git_and_a_checkout_not_here_or_a_failed_read_is_unread()
    {
        var q7 = Taken("q7");
        var taken = 0;
        RefSnapshot Failing(string root)
        {
            taken++;
            return RefSnapshot.Take(_ => Task.FromResult<string?>(null));
        }

        // The owner's install: no quest bound to a closure graph, so no snapshot is taken.
        var unbound = await QuestClosing.TipsAsync([QuestUnit(Taken("q7", closure: null)), Branch("daoris/s1", "q7")],
            new Dictionary<string, string> { ["engine"] = "engine-checkout" }, None, Failing);
        Assert.Empty(unbound);
        Assert.Equal(0, taken);

        // No checkout of its repository: unread, and nothing taken.
        var rootless = await QuestClosing.TipsAsync([QuestUnit(q7), Branch("daoris/s1", "q7")], new Dictionary<string, string>(), None, Failing);
        Assert.Equal(new Dictionary<string, string?> { ["branch:engine/daoris/s1"] = null }, rootless);
        Assert.Equal(0, taken);

        // git could not read the refs: unread, never gone.
        var failed = await QuestClosing.TipsAsync([QuestUnit(q7), Branch("daoris/s1", "q7")], new Dictionary<string, string> { ["engine"] = "engine-checkout" }, None, Failing);
        Assert.Equal(new Dictionary<string, string?> { ["branch:engine/daoris/s1"] = null }, failed);
        Assert.Equal(1, taken);
    }
}
