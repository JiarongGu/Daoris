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
        Assert.Null(await pressed.BeforePush(pushWaited, default));
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
        var held = await pressed.BeforePush(false, default);

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
        Assert.Null(await pressed.BeforePush(true, default));
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

        var pressed = await PressQuests.HandedAsync(Landed(advancedBy), SessionQuest, service.ReadAsync);

        Assert.NotNull(pressed);
        Assert.Equal([session], asked);
        Assert.Equal(quest, pressed.Pressed.Id);
        Assert.Equal(1, service.Reads);
        service.Quests[quest] = Submitted(sequence: 2) with { Id = quest };
        Assert.Contains($"`daoris-driver trees hand {session}`", await pressed.BeforePush(true, default));
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

        var pressed = await PressQuests.HandedAsync(
            Landed("s2"), (_, _) => Task.FromResult(status is null ? null : (string?)"q2"), service.ReadAsync);

        Assert.Null(pressed);
        Assert.Equal(reads, service.Reads);
    }
}
