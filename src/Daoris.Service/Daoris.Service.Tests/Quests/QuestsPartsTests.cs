using System.Text.Json;
using Daoris.Contracts.Parts;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The quest store builds the operations its modules register, keyed by kind, as it opens: a kind declared twice, a mark naming another
/// kind than its module's, and a module that is no operation are refused there, never at a write. A registered kind writes and reads its
/// log payload exactly as the store wrote it before the kind moved, so a row from before replays the same.
/// </summary>
public sealed class QuestsPartsTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 4, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void The_store_builds_the_registered_operations_from_the_real_assembly_each_keyed_by_its_kind()
    {
        var operations = QuestOperations.Registered;

        Assert.Equal(
            ["accepted", "chosen", "conflict", "dismissed", "done", "evidenced", "reopened", "setup", "verdict", "waited"],
            operations.Order.Select(part => part.Name));
        Assert.All(operations.Order, part => Assert.Equal(QuestOperationPhase.Log, (QuestOperationPhase)part.Phase));
        Assert.All(operations.Order, part => Assert.Equal(part.Name, QuestStore.KindText(Enum.Parse<QuestOperationKind>(part.Name, ignoreCase: true))));
        Assert.IsType<QuestAcceptOperation>(operations.For(QuestOperationKind.Accepted));
        Assert.IsType<QuestChoiceOperation>(operations.For(QuestOperationKind.Chosen));
        Assert.IsType<QuestSetUpOperation>(operations.For(QuestOperationKind.SetUp));
        Assert.IsType<QuestVerdictOperation>(operations.For(QuestOperationKind.Verdict));
        Assert.IsType<QuestConflictOperation>(operations.For(QuestOperationKind.Conflict));
        Assert.IsType<QuestDismissOperation>(operations.For(QuestOperationKind.Dismissed));
        Assert.IsType<QuestCloseOperation>(operations.For(QuestOperationKind.Done));
        Assert.IsType<QuestReopenOperation>(operations.For(QuestOperationKind.Reopened));

        // A kind no module registers yet is written and read by the store's own fields.
        Assert.Null(operations.For(QuestOperationKind.Published));
    }

    [Fact]
    public void A_kind_declared_twice_a_mark_naming_another_kind_and_a_module_that_is_no_operation_are_refused()
    {
        Assert.Equal(
            "the part conflict is declared twice, by QuestConflictOperation and QuestDismissOperation",
            Assert.Throws<PartOrderException>(() => PartOrder.Of(
                [Declared("conflict", typeof(QuestConflictOperation)), Declared("conflict", typeof(QuestDismissOperation))])).Message);

        Assert.Equal(
            "the part dismissed (QuestConflictOperation) is the operation of `conflict`, so its mark names another kind",
            Assert.Throws<PartOrderException>(() => QuestOperations.Of([Declared("dismissed", typeof(QuestConflictOperation))])).Message);

        Assert.Equal(
            "the part conflict (QuestsPartsTests) is no IQuestOperation",
            Assert.Throws<PartOrderException>(() => QuestOperations.Of([Declared("conflict", typeof(QuestsPartsTests))])).Message);
    }

    [Fact]
    public void A_conflict_writes_the_move_it_attempted_its_note_and_the_reopen_it_lost_to_as_before_and_reads_them_back()
    {
        var conflict = new QuestOperation(
            "q1", QuestOperationKind.Conflict, "m1", 3, At, Note: "went", Attempted: QuestStatus.Taken, LostToTheReopen: true);

        AssertKept("""{"attempted":"Taken","note":"went","lostToTheReopen":true}""", conflict);
        AssertKept("""{"attempted":"Done"}""", conflict with { Note = null, Attempted = QuestStatus.Done, LostToTheReopen = false });
    }

    [Fact]
    public void A_dismissal_writes_the_conflict_it_names_and_the_take_it_brought_forward_as_before_and_reads_them_back()
    {
        var plain = new QuestOperation("q1", QuestOperationKind.Dismissed, "m1", 4, At, Dismisses: new QuestOperationRef("m2", 7));

        AssertKept("""{"dismisses":{"machine":"m2","sequence":7}}""", plain);
        AssertKept(
            """{"dismisses":{"machine":"m2","sequence":7},"forward":{"machine":"m1","sequence":9}}""",
            plain with { Forward = new QuestOperationRef("m1", 9) });
    }

    [Fact]
    public void A_reopen_writes_the_persons_words_and_the_submission_it_names_as_before_and_reads_them_back()
    {
        var onTheQuest = new QuestOperation(
            "q1", QuestOperationKind.Reopened, "m1", 5, At, Note: "not yet", Submission: new QuestOperationRef("m1", 2));

        AssertKept("""{"note":"not yet","submission":{"machine":"m1","sequence":2}}""", onTheQuest);
        AssertKept("""{"submission":{"machine":"m1","sequence":2}}""", onTheQuest with { Quest = "q2", Note = null });
    }

    [Fact]
    public void A_done_writes_its_note_answers_close_and_session_as_before_and_reads_them_back()
    {
        var closed = new QuestOperation(
            "q1", QuestOperationKind.Done, "m1", 6, At, Note: "shipped", Answers: [new QuestAnswer(1, "all of it", null, null)],
            Close: new QuestClose("k1", "t1"), Session: "s1");

        AssertKept(
            """{"note":"shipped","answers":[{"requirement":1,"met":"all of it"}],"close":{"key":"k1","token":"t1"},"session":"s1"}""",
            closed);
        AssertKept("""{}""", closed with { Note = null, Answers = null, Close = null, Session = null });
    }

    [Fact]
    public void A_yes_writes_nothing_but_an_empty_payload_as_before_and_reads_back_as_itself()
    {
        AssertKept("""{}""", new QuestOperation("q1", QuestOperationKind.Accepted, "m1", 7, At));
    }

    [Fact]
    public void A_wait_writes_the_question_it_waits_on_as_its_note_as_before_and_reads_it_back()
    {
        AssertKept("""{"note":"ab12cd34ef56"}""", new QuestOperation("q1", QuestOperationKind.Waited, "m1", 8, At, "ab12cd34ef56"));
    }

    /// <summary>The payload the store writes for <paramref name="operation"/>, and the operation a row of it reads back as.</summary>
    private static void AssertKept(string payload, QuestOperation operation)
    {
        Assert.Equal(payload, QuestStore.PayloadJson(operation));

        using var document = JsonDocument.Parse(payload);
        var columns = new QuestOperation(operation.Quest, operation.Kind, operation.Machine, operation.Sequence, operation.At);
        var read = QuestStore.ReadPayload(columns, document.RootElement);
        Assert.Equal(operation with { Answers = null }, read with { Answers = null });
        Assert.Equal(operation.Answers, read.Answers);
    }

    private static PartDeclaration Declared(string name, Type type) => new(name, QuestOperationPhase.Log, [], type);
}
