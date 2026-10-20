using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1h3 (D173's note; the fabric design §E5): the person's press and an entry's Accept are judged again under their
/// landing's holds, on the result the press was made on. Only that result moving stops the press, a reopen, a later submission or
/// another <c>ready</c> at the same commit; a quest's hold never does, since the press is the person's own word. A press on no quest,
/// or on a quest whose result is not in, holds to nothing and lands as before. Tables and counted reads, so the suite's fast half; the
/// presses over real git are <see cref="AutoLandingTests"/> and <see cref="EntryLandingTests"/>.
/// </summary>
public sealed class PressedResultTests
{
    private const string Graph = "sha256:closure";

    private static QuestSubmissionView Made(long sequence, string machine = "desk") => new(machine, sequence, DateTimeOffset.UtcNow, "ready");

    private static QuestView Quest(string status = "Done", QuestSubmissionView? submission = null, bool held = false)
    {
        var view = new QuestView("q1", "game", "engine", "Fix the gap", "Fix it.", status) { Held = held, Hold = held ? EvidenceCodes.Departed : null };
        return submission is null ? view : view with { Closure = Graph, Submission = submission };
    }

    private static QuestView Submitted(long sequence = 1, string machine = "desk", bool held = false) =>
        Quest("Taken", Made(sequence, machine), held);

    /// <summary>The quests a stand-in service holds, and how often the press read one.</summary>
    private sealed class Service
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Reads { get; private set; }

        public Task<QuestView?> ReadAsync(string id, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(Quests.GetValueOrDefault(id));
        }
    }

    // ——— what a press holds to

    /// <summary>
    /// Decision 3: a conversation with no quest, and a quest whose result is not in (open, or taken with no submission), hold to nothing,
    /// so the press lands exactly as before. A done, and a closure graph's submission held or not, are what the press was made on.
    /// </summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("Open", false)]
    [InlineData("Taken", false)]
    [InlineData("taken-bound", false)]
    [InlineData("Done", true)]
    [InlineData("submitted", true)]
    [InlineData("submitted-held", true)]
    public async Task A_press_holds_to_its_quests_result_only_where_that_result_is_in(string? status, bool holds)
    {
        var service = new Service();
        if (status is not null)
        {
            service.Quests["q1"] = status switch
            {
                "taken-bound" => Quest("Taken") with { Closure = Graph },
                "submitted" => Submitted(),
                "submitted-held" => Submitted(held: true),
                _ => Quest(status),
            };
        }

        var pressed = await new PressQuests(service.ReadAsync).PressedAsync("s1", status is null ? null : "q1");

        Assert.Equal(holds, pressed is not null);
        // A conversation reads nothing; a quest is read once.
        Assert.Equal(status is null ? 0 : 1, service.Reads);
    }

    /// <summary>
    /// The door's own reads name the landing (the chain's walk reads the session's quest first), so the press is made on that read and
    /// reads nothing of its own: the owner's install, whose presses wait for nothing, reads no quest more than it did.
    /// </summary>
    [Fact]
    public async Task The_press_is_made_on_the_read_its_door_already_made()
    {
        var service = new Service { Quests = { ["q1"] = Quest() } };
        var quests = new PressQuests(service.ReadAsync);

        Assert.NotNull(await quests.FindAsync("q1"));
        Assert.NotNull(await quests.FindAsync("#q1"));
        var pressed = await quests.PressedAsync("s1", "q1");

        Assert.NotNull(pressed);
        Assert.Equal(1, service.Reads);
    }

    // ——— what moves it

    /// <summary>
    /// Decision 2: the result moving is a reopen (taken back, or turned down), a later submission, or another <c>ready</c> at the same
    /// commit (a submission is known by its machine and sequence, CLOSE1q's identity). Decision 1: a hold on the same result is no move,
    /// nor is its graph closing it done on that submission.
    /// </summary>
    [Theory]
    [InlineData("submitted", "same", null)]
    [InlineData("submitted", "held", null)]
    [InlineData("submitted", "closed", null)]
    [InlineData("submitted", "reopened", "its quest was taken back")]
    [InlineData("submitted", "gone", "its quest was taken back")]
    [InlineData("submitted", "later", "its quest's submission was made again")]
    [InlineData("submitted", "elsewhere", "its quest's submission was made again")]
    [InlineData("done", "same", null)]
    [InlineData("done", "held", null)]
    [InlineData("done", "turned-down", "its quest was taken back")]
    public void What_moves_the_result_a_press_was_made_on(string pressed, string now, string? why)
    {
        var at = pressed == "submitted" ? Submitted() : Quest();
        QuestView? then = now switch
        {
            "same" => at,
            "held" => at with { Held = true, Hold = EvidenceCodes.Departed },
            "closed" => at with { Status = "Done" },
            "reopened" => at with { Submission = null },
            "gone" => null,
            "later" => Submitted(sequence: 2),
            "elsewhere" => Submitted(machine: "laptop"),
            "turned-down" => at with { Status = "Taken" },
            _ => throw new ArgumentOutOfRangeException(nameof(now)),
        };

        Assert.Equal(why, PressedResult.Moved(at, then));
    }

    // ——— when it is read again

    /// <summary>
    /// Under the tree's hold the result is read again only where the press waited for another landing of the tree, or was made earlier
    /// than its landing (an entry's part, behind the press's fetch and its earlier parts). Just before the push, a submission is read
    /// every time, and a done only where the push waited for another push of its branch. So an uncontended press reads one quest more
    /// at most, and only on a submission.
    /// </summary>
    [Theory]
    [InlineData("done", false, false, false, 0, 0)]
    [InlineData("done", true, false, false, 1, 0)]
    [InlineData("done", false, false, true, 0, 1)]
    [InlineData("done", false, true, false, 1, 0)]
    [InlineData("submitted", false, false, false, 0, 1)]
    [InlineData("submitted", true, false, true, 1, 1)]
    [InlineData("submitted", false, true, false, 1, 1)]
    public async Task A_press_reads_its_quest_again_only_where_time_could_have_moved_it(
        string status, bool treeWaited, bool late, bool pushWaited, int underHold, int beforePush)
    {
        var service = new Service { Quests = { ["q1"] = status == "done" ? Quest() : Submitted() } };
        var pressed = (await new PressQuests(service.ReadAsync).PressedAsync("s1", "q1", late))!;
        var read = service.Reads;

        Assert.Null(await pressed.Judged(treeWaited, default));
        Assert.Equal(underHold, service.Reads - read);
        read = service.Reads;
        Assert.Null(await pressed.BeforePush(null, pushWaited, default));
        Assert.Equal(beforePush, service.Reads - read);
    }

    /// <summary>
    /// Decision 4: moved under the tree's hold, the press makes nothing and says why, and that the person accepts again once they have
    /// looked; moved just before the push, the branch stands made and recorded, and the sentence names the hand-off that pushes it.
    /// </summary>
    [Theory]
    [InlineData("reopened", AutoLandingCode.Undone, "its quest was taken back")]
    [InlineData("later", AutoLandingCode.Refused, "its quest's submission was made again")]
    public async Task A_moved_result_refuses_under_the_tree_and_holds_the_push_back(string change, string code, string why)
    {
        var service = new Service { Quests = { ["q1"] = Submitted() } };
        var pressed = (await new PressQuests(service.ReadAsync).PressedAsync("s1", "q1"))!;
        service.Quests["q1"] = change == "reopened" ? service.Quests["q1"] with { Submission = null } : Submitted(sequence: 2);

        var refused = await pressed.Judged(true, default);
        var held = await pressed.BeforePush(null, false, default);

        Assert.NotNull(refused);
        Assert.False(refused.Landed);
        Assert.Equal(code, refused.Refusal);
        Assert.StartsWith("Nothing was landed: ", refused.Message);
        Assert.Contains(why, refused.Message);
        Assert.Contains("accept it again", refused.Message);
        Assert.NotNull(held);
        Assert.StartsWith(char.ToUpperInvariant(why[0]) + why[1..] + " before its plugin was told", held);
        Assert.Contains("its push was not tried", held);
        Assert.Contains("`daoris-driver trees hand s1`", held);
    }

    /// <summary>Decision 1: a hold that comes while the press waits is the quest's, not the press's, and stops neither stage.</summary>
    [Fact]
    public async Task A_hold_on_the_same_result_stops_neither_stage()
    {
        var service = new Service { Quests = { ["q1"] = Submitted() } };
        var pressed = (await new PressQuests(service.ReadAsync).PressedAsync("s1", "q1"))!;
        service.Quests["q1"] = service.Quests["q1"] with { Held = true, Hold = EvidenceCodes.Departed };

        Assert.Null(await pressed.Judged(true, default));
        Assert.Null(await pressed.BeforePush(null, true, default));
    }

    // ——— the earlier sessions' work a press's push carries (FABRIC-CLOSE1h11)

    /// <summary>
    /// FABRIC-CLOSE1h11 (D173's CLOSELAND1 note): a press of s2 moves s1's branch on, and its push carries s1's work wherever no push
    /// carried it yet, so s1's quest is judged too, by the person's rule: its result taken back, or a submission made again since its
    /// landing, holds the push back, in words naming s1 and its quest; a hold does not. Where the branch was pushed at s1's tip, or the
    /// record does not say which quest s1 served (a landing from before), nothing more is read.
    /// </summary>
    [Theory]
    [InlineData("standing", null, 1)]
    [InlineData("held", null, 1)]
    [InlineData("reopened", "was taken back", 1)]
    [InlineData("gone", "was taken back", 1)]
    [InlineData("made-again", "was submitted again", 1)]
    [InlineData("pushed", null, 0)]
    [InlineData("unrecorded", null, 0)]
    public async Task A_press_whose_push_carries_an_earlier_sessions_work_is_judged_on_that_sessions_quest_too(
        string change, string? why, int reads)
    {
        var service = new Service { Quests = { ["q1"] = Submitted() with { Id = "q1" }, ["q2"] = Quest() with { Id = "q2" } } };
        var pressed = (await new PressQuests(service.ReadAsync).PressedAsync("s2", "q2"))!;
        var record = Landed("s2") with { Served = "q1", Submission = new QuestSubmissionRef("desk", 1) };
        service.Quests["q1"] = change switch
        {
            "held" => service.Quests["q1"] with { Held = true, Hold = EvidenceCodes.Departed },
            "reopened" => service.Quests["q1"] with { Submission = null },
            "made-again" => Submitted(sequence: 2) with { Id = "q1" },
            _ => service.Quests["q1"],
        };
        if (change == "gone") service.Quests.Remove("q1");
        if (change == "pushed") record = record with { Pushed = true, PushedTip = "c0ffee" };
        if (change == "unrecorded") record = record with { Served = null, Submission = null };
        var read = service.Reads;

        var withheld = await pressed.BeforePush(record, false, default);

        Assert.Equal(reads, service.Reads - read);
        if (why is null)
        {
            Assert.Null(withheld);
            return;
        }

        Assert.NotNull(withheld);
        Assert.StartsWith($"Session s1's quest #q1, whose work this push carries, {why} before its plugin was told", withheld);
        Assert.Contains("its push was not tried", withheld);
        Assert.Contains("`daoris-driver trees hand s2`", withheld);
    }

    // ——— a person's hand-off (FABRIC-CLOSE1h8)

    /// <summary>A branch s1's landing made, moved on by each of <paramref name="advancedBy"/> in turn.</summary>
    private static LandedBranch Landed(params string[] advancedBy) =>
        new("engine", "aurora", "feature/q1-fix-the-gap", "main", "c0ffee", "s1", "q1", "Fix the gap", DateTimeOffset.UtcNow)
        {
            Advances = [.. advancedBy.Select(session => new LandedAdvance("c0ffee", "decade", DateTimeOffset.UtcNow, session))],
        };

    /// <summary>
    /// The parent's decision 1: a hand-off is held to the quest of the session that last moved its branch, its newest advance's, else
    /// its landing's, read where the door names the branch; the record's founding quest is not read. Its push's sentence names that
    /// session's hand-off.
    /// </summary>
    [Theory]
    [InlineData(new string[0], "s1", "q1")]
    [InlineData(new[] { "s2" }, "s2", "q2")]
    [InlineData(new[] { "s2", "s3" }, "s3", "q3")]
    public async Task A_hand_off_is_held_to_the_quest_of_the_session_that_last_moved_its_branch(string[] advancedBy, string session, string quest)
    {
        var service = new Service();
        foreach (var each in new[] { "q1", "q2", "q3" }) service.Quests[each] = Submitted() with { Id = each };
        var asked = new List<string>();
        Task<string?> SessionQuest(string id, CancellationToken ct)
        {
            asked.Add(id);
            return Task.FromResult<string?>($"q{id[1..]}");
        }

        var handed = await PressQuests.HandedAsync(Landed(advancedBy), SessionQuest, service.ReadAsync);

        Assert.NotNull(handed.Pressed);
        Assert.Equal(session, handed.Session);
        Assert.Equal([session], asked);
        Assert.Equal(quest, handed.Pressed.Pressed.Id);
        Assert.Equal(1, service.Reads);
        service.Quests[quest] = Submitted(sequence: 2) with { Id = quest };
        Assert.Contains($"`daoris-driver trees hand {session}`", await handed.BeforePush(Landed(advancedBy), true, default));
    }

    /// <summary>
    /// The parent's decision 5: a session on no quest reads no quest, and a quest whose result is not in holds to nothing, so the
    /// hand-off hands on as before.
    /// </summary>
    [Theory]
    [InlineData(null, 0)]
    [InlineData("Open", 1)]
    [InlineData("Taken", 1)]
    public async Task An_unbound_hand_off_or_one_on_no_result_holds_to_nothing(string? status, int reads)
    {
        var service = new Service();
        if (status is not null) service.Quests["q2"] = Quest(status);

        var handed = await PressQuests.HandedAsync(
            Landed("s2"), (_, _) => Task.FromResult(status is null ? null : (string?)"q2"), service.ReadAsync);

        Assert.Null(handed.Pressed);
        Assert.Null(await handed.BeforePush(Landed("s2"), true, default));
        Assert.Equal(reads, service.Reads);
    }

    // ——— a later session's advance while the hand-off waited (FABRIC-CLOSE1h10)

    /// <summary>
    /// FABRIC-CLOSE1h10 (D173's note): the door read the branch with s1 last, and the record under the branch's hold names s2, whose done
    /// moved it on meanwhile. The hand-off is judged on s2's quest, by the person's rule: its result not in (taken back, or gone) holds the
    /// push back in words that name s2's hand-off; standing, held or not, it goes on, since the hand-off is the person's own word; a session
    /// on no quest holds it to nothing. s1's result moving does not count, as it is not the branch's last. s2's quest is read every time,
    /// whether the hold waited or not: the door never read it.
    /// </summary>
    [Theory]
    [InlineData("Done", false)]
    [InlineData("submitted-held", false)]
    [InlineData("Open", true)]
    [InlineData("gone", true)]
    [InlineData(null, false)]
    public async Task A_hand_off_whose_branch_a_later_session_moved_on_is_judged_on_that_sessions_quest(string? later, bool holds)
    {
        var service = new Service();
        service.Quests["q1"] = Submitted() with { Id = "q1" };
        if (later is "Done" or "Open") service.Quests["q2"] = Quest(later) with { Id = "q2" };
        if (later is "submitted-held") service.Quests["q2"] = Submitted(held: true) with { Id = "q2" };
        var asked = new List<string>();
        Task<string?> SessionQuest(string id, CancellationToken ct)
        {
            asked.Add(id);
            return Task.FromResult(id == "s1" ? "q1" : later is null ? null : (string?)"q2");
        }

        var handed = await PressQuests.HandedAsync(Landed(), SessionQuest, service.ReadAsync);
        service.Quests["q1"] = Submitted(sequence: 2) with { Id = "q1" };
        var withheld = await handed.BeforePush(Landed("s2"), false, default);

        Assert.Equal(["s1", "s2"], asked);
        Assert.Equal(later is null ? 1 : 2, service.Reads);
        if (!holds)
        {
            Assert.Null(withheld);
            return;
        }

        Assert.NotNull(withheld);
        Assert.Contains("its quest was taken back", withheld, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("its push was not tried", withheld);
        Assert.Contains("`daoris-driver trees hand s2`", withheld);
    }

    /// <summary>
    /// The parent's decision 4: where no later session moved the branch, the hand-off is judged exactly as FABRIC-CLOSE1h8 judges it, with
    /// no read more: an unbound session reads nothing, and a done that did not wait is not read again.
    /// </summary>
    [Theory]
    [InlineData(null, 0)]
    [InlineData("Done", 1)]
    public async Task A_hand_off_whose_branch_no_later_session_moved_reads_no_more_than_before(string? status, int reads)
    {
        var service = new Service();
        if (status is not null) service.Quests["q1"] = Quest(status);
        var asked = 0;

        var handed = await PressQuests.HandedAsync(
            Landed(), (_, _) => { asked++; return Task.FromResult(status is null ? null : (string?)"q1"); }, service.ReadAsync);

        Assert.Null(await handed.BeforePush(Landed() with { Session = "S1" }, false, default));
        Assert.Equal((1, reads), (asked, service.Reads));
    }

    /// <summary>
    /// FABRIC-CLOSE1h13 (D173's CLOSELAND1 note): the record now keeps the submission each advance was accepted on, so a later session whose
    /// submission was made again since its advance (another <c>ready</c>, at another commit or the same one) holds the hand-off back too,
    /// by the person's rule, in words naming its hand-off: the work on the branch is what that submission replaced. Standing on the same
    /// submission it goes on; an advance from before, which keeps none, is judged only on its result being in, as before.
    /// </summary>
    [Theory]
    [InlineData("same", true, null)]
    [InlineData("made-again", true, "its quest's submission was made again")]
    [InlineData("elsewhere", true, "its quest's submission was made again")]
    [InlineData("made-again", false, null)]
    public async Task A_later_session_whose_submission_was_made_again_since_its_advance_holds_the_hand_off_back(
        string change, bool kept, string? why)
    {
        var service = new Service { Quests = { ["q1"] = Submitted() with { Id = "q1" }, ["q2"] = Submitted() with { Id = "q2" } } };
        var handed = await PressQuests.HandedAsync(Landed(), (id, _) => Task.FromResult<string?>($"q{id[1..]}"), service.ReadAsync);
        var advanced = Landed() with
        {
            Advances = [new LandedAdvance("c0ffee", "decade", DateTimeOffset.UtcNow, "s2")
            {
                Quest = "q2", Submission = kept ? new QuestSubmissionRef("desk", 1) : null,
            }],
        };
        service.Quests["q2"] = change switch
        {
            "made-again" => Submitted(sequence: 2) with { Id = "q2" },
            "elsewhere" => Submitted(machine: "laptop") with { Id = "q2" },
            _ => service.Quests["q2"],
        };

        var withheld = await handed.BeforePush(advanced, false, default);

        if (why is null)
        {
            Assert.Null(withheld);
            return;
        }

        Assert.NotNull(withheld);
        Assert.StartsWith(char.ToUpperInvariant(why[0]) + why[1..] + " before its plugin was told", withheld);
        Assert.Contains("`daoris-driver trees hand s2`", withheld);
    }
}
