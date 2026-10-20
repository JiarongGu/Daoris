using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FLOWCHART1a2 (D186; the flowchart design §2.1, §7 traps 13 and 20): a quest's history read as its moves, which its page draws
/// on its flowchart and a trace says. Each move in the order the history replays, never by time; whether the replay applied it,
/// as the service's replay decides; the status it reached the quest at; and its fields by allow-list, never a payload, a note's
/// body, the answers or a path.
/// </summary>
public sealed class QuestMovesTests
{
    private const string Id = "f10c4a2a0001";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-20T09:00:00Z");

    private static readonly Quest Asked = new(Id, "ask #a1b2c3", "reports", "Write the report", "The body nobody else reads.", QuestStatus.Open, null, Now, Now)
    {
        Requirements = [new QuestRequirement("write the report", "The report is in docs/report.md.")],
    };

    private static QuestOperation Op(QuestOperationKind kind, string machine, long sequence, int minutes, long? number = null) =>
        new(Id, kind, machine, sequence, Now.AddMinutes(minutes), Number: number);

    private static QuestOperation Publish(int minutes = 0, long? number = 1) =>
        Op(QuestOperationKind.Published, "m2", 1, minutes, number) with { Published = Asked };

    /// <summary>
    /// Trap 20: the order is the history's, as the remote numbered it and then this machine's pending ones, and never the times,
    /// which are each machine's own: a pending move made on a clock behind the remote's comes last all the same.
    /// </summary>
    [Fact]
    public async Task The_moves_keep_the_replay_order_a_pending_move_after_the_accepted_ones_whatever_its_time()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = await QuestStore.OpenAsync(connection);
        await store.IntegrateAsync(Workspaces.Default,
        [
            Publish(minutes: 60, number: 1),
            Op(QuestOperationKind.Taken, "m3", 1, minutes: 90, number: 2),
        ], 2);
        // This machine's own wait, pending, made "earlier" by its own clock than both the remote accepted.
        Assert.True((await store.WaitAsync(Id, "fedcbafedcba", Now)).Moved);

        var moves = QuestMoves.Read(await store.HistoryAsync(Id));

        Assert.Equal([QuestOperationKind.Published, QuestOperationKind.Taken, QuestOperationKind.Waited], moves.Select(move => move.Kind));
        Assert.Equal([1L, 2L, null], moves.Select(move => move.Number));
        Assert.True(moves[2].At < moves[0].At);
        Assert.Equal(store.Machine, moves[2].Machine);
    }

    /// <summary>
    /// Trap 20: a recorded operation is not always a move. A second take after another machine's, and a done after a decline,
    /// are recorded and not applied, each read from the status it reached the quest at; the take says no session, since a take
    /// keeps none (only a submission names one).
    /// </summary>
    [Fact]
    public void A_move_the_replay_does_not_apply_is_read_as_not_applied_from_the_status_it_reached()
    {
        var moves = QuestMoves.Read(
        [
            Publish(),
            Op(QuestOperationKind.Taken, "m3", 1, 1),
            Op(QuestOperationKind.Taken, "m4", 1, 2),
            Op(QuestOperationKind.Declined, "m3", 2, 3) with { Note = "Not ours after all." },
            Op(QuestOperationKind.Done, "m4", 2, 4) with { Note = "It is done." },
        ]);

        Assert.Equal([true, true, false, true, false], moves.Select(move => move.Applied));
        Assert.Equal([null, QuestStatus.Open, QuestStatus.Taken, QuestStatus.Taken, QuestStatus.Declined], moves.Select(move => move.From));
        Assert.All(moves, move => Assert.Null(move.Session));
    }

    /// <summary>
    /// Trap 20: a dismissal names the conflict it settles by the machine and sequence the conflict keeps (`QuestLog.cs`'s
    /// <see cref="QuestOperationRef"/>), so a page matches the two; the conflict says the status it attempted.
    /// </summary>
    [Fact]
    public void A_dismissal_names_its_conflict_by_the_reference_the_conflict_keeps()
    {
        var moves = QuestMoves.Read(
        [
            Publish(),
            Op(QuestOperationKind.Taken, "m3", 1, 1),
            Op(QuestOperationKind.Conflict, "m4", 7, 2) with { Attempted = QuestStatus.Taken, Note = "Machine four's session." },
            Op(QuestOperationKind.Dismissed, "m3", 2, 3) with { Dismisses = new QuestOperationRef("m4", 7) },
        ]);

        var conflict = moves[2];
        Assert.Equal((QuestStatus.Taken, true), (conflict.Attempted, conflict.Applied));
        Assert.Equal(new QuestOperationRef(conflict.Machine, conflict.Sequence), moves[3].Dismisses);
    }

    /// <summary>
    /// The hold a move left the quest under, where it changed it: a departing done holds it for the person's yes, their yes lets it
    /// go (<c>none</c>), and a move that leaves the hold as it was says nothing of one.
    /// </summary>
    [Fact]
    public void A_move_says_the_hold_it_set_or_let_go_and_nothing_where_it_left_it()
    {
        var moves = QuestMoves.Read(
        [
            Publish(),
            Op(QuestOperationKind.Taken, "m3", 1, 1),
            Op(QuestOperationKind.Done, "m3", 2, 2) with
            {
                Note = "Done, in the wiki.", Answers = [new QuestAnswer(1, null, Departed: "It went into the wiki.", Quote: "write the report")],
            },
            Op(QuestOperationKind.Accepted, "m1", 1, 3),
        ]);

        Assert.Equal([null, null, "departed", QuestMoves.NoHold], moves.Select(move => move.Hold));
    }

    /// <summary>
    /// A wait answers the quest it waits on, which its note is; a note that does not read as a quest's id is no quest, and is not
    /// answered: what a move says is never a note's words.
    /// </summary>
    [Fact]
    public void A_wait_answers_the_quest_it_waits_on_and_never_a_note_that_is_not_an_id()
    {
        var waits = QuestMoves.Read(
        [
            Publish(),
            Op(QuestOperationKind.Taken, "m3", 1, 1),
            Op(QuestOperationKind.Waited, "m3", 2, 2) with { Note = "fedcbafedcba" },
            Op(QuestOperationKind.Waited, "m3", 3, 3) with { Note = "a sentence, with a path X:/daoris/notes.md" },
        ]);

        Assert.Equal("fedcbafedcba", waits[2].WaitsOn);
        Assert.Null(waits[3].WaitsOn);
    }

    /// <summary>
    /// The codes an evidence read, a set-up and a verdict answer: what was found, where it was shown, and what the person said.
    /// A commit read with nothing to find (a submission's commit, FABRIC-CLOSE1c6) is neither found nor missing.
    /// </summary>
    [Fact]
    public void An_evidence_read_a_set_up_and_a_verdict_answer_their_codes()
    {
        const string commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
        QuestOperation Evidenced(long sequence, params QuestEvidenceRead[] items) =>
            Op(QuestOperationKind.Evidenced, "m1", sequence, (int)sequence) with { Evidence = new QuestEvidenceVerdict(commit, "session-end", items) };
        QuestMoveReading Read(QuestOperation operation) => QuestMoves.Read([operation]).Single();

        Assert.Equal(QuestMoves.Found, Read(Evidenced(1, new QuestEvidenceRead(1, "docs/report.md", null, "found"))).Evidence);
        Assert.Equal(QuestMoves.Missing, Read(Evidenced(2, new QuestEvidenceRead(1, "docs/report.md", null, "missing"))).Evidence);
        Assert.Equal(QuestMoves.CommitOnly, Read(Evidenced(3)).Evidence);
        Assert.Equal(QuestMoves.Local,
            Read(Op(QuestOperationKind.SetUp, "m1", 4, 4) with { SetUp = new QuestSetUp(commit) { Local = true, Look = "http://localhost:5173" } }).SetUp);
        Assert.Equal(QuestMoves.Deployed,
            Read(Op(QuestOperationKind.SetUp, "m1", 5, 5) with { SetUp = new QuestSetUp(commit) { Look = "https://dev.example.test" } }).SetUp);
        Assert.Equal("not-yet",
            Read(Op(QuestOperationKind.Verdict, "m1", 6, 6) with { Verdict = new QuestReviewVerdict("not-yet") { Words = "Not the colour." } }).Verdict);
    }

    /// <summary>
    /// Trap 13: a move answers only the allow-list, every move's fields and its kind's own, and nothing else a reading could carry: a
    /// property added to the reading is classed here first, so a note, a payload or a path cannot reach a page by being added.
    /// </summary>
    [Fact]
    public void A_move_answers_only_the_allow_list()
    {
        var properties = typeof(QuestMoveReading).GetProperties().Select(property => property.Name).Order();

        Assert.Equal(QuestMovesFields.Every.Concat(QuestMovesFields.Own).Order(), properties);
    }
}
