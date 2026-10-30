using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1q (D212; the foundation design §2.3): the start layer's table, keyed by the work. A working session, driven or a
/// conversation, may ask for a co-worker; a co-worker, a reviewer, an intake and Ask Daoris may not (D203 point 6; §7 row 9). A refusal
/// names the asker by the mark the doors named it by before, a co-worker's <c>serves</c> first.
/// </summary>
public sealed class StartAdmissionTests
{
    private static WorkMarks Marks(string kind = "driven", string repository = "engine", string? ask = null, string? serves = null, string? piece = null, string? opinion = null) =>
        new(Mirror: false, kind, repository, ask, serves, piece, opinion);

    [Theory]
    [InlineData(ParticipantWork.Build, true)]
    [InlineData(ParticipantWork.Design, true)]
    [InlineData(ParticipantWork.Chat, true)]
    [InlineData(ParticipantWork.Help, false)]
    [InlineData(ParticipantWork.Partner, false)]
    [InlineData(ParticipantWork.Review, false)]
    [InlineData(ParticipantWork.Intake, false)]
    [InlineData(ParticipantWork.AskDaoris, false)]
    [InlineData(ParticipantWork.Brainstorm, false)]
    [InlineData(null, false)]
    public void Only_a_working_session_may_ask_for_a_co_worker(string? work, bool asks)
    {
        Assert.Equal(asks, StartAdmission.AsksCoWorker(work));
    }

    public static TheoryData<string, WorkMarks, string?> Askers => new()
    {
        { "a driven session", Marks(), null },
        { "a conversation", Marks(kind: "chat"), null },
        { "a helper", Marks(kind: "chat", serves: "s1"), StartAdmission.Helper },
        { "a partner", Marks(kind: "chat", serves: "s1", piece: "p1"), StartAdmission.Helper },
        { "a reviewer", Marks(kind: "chat", opinion: "o1"), StartAdmission.Reviewer },
        { "a reviewer that serves", Marks(kind: "chat", serves: "s1", opinion: "o1"), StartAdmission.Helper },
        { "an intake", Marks(kind: "chat", repository: "ask #a1", ask: "a1"), StartAdmission.Intake },
        { "Ask Daoris", Marks(kind: "chat", repository: "daoris:help"), StartAdmission.AskDaoris },
        { "an empty serves", Marks(serves: ""), StartAdmission.Helper },
        { "an empty ask", Marks(ask: ""), null },
    };

    [Theory]
    [MemberData(nameof(Askers))]
    public void A_helper_asker_is_refused_by_its_work_and_named_by_its_mark(string name, WorkMarks marks, string? refused)
    {
        Assert.True(refused == StartAdmission.HelperAsker(marks), name);
    }

    [Theory]
    [InlineData("driven", "engine", null, null, "trees/a", true)]
    [InlineData("chat", "engine", null, null, "trees/a", true)]
    [InlineData("driven", "engine", null, null, null, false)]
    [InlineData("chat", "daoris:help", null, null, "trees/a", false)]
    [InlineData("chat", "engine", null, "ask", "trees/a", false)]
    [InlineData("chat", "ask #a1", "a1", null, "trees/a", false)]
    public void A_piece_is_led_by_a_working_session_in_a_tree_of_its_own(string kind, string repository, string? ask, string? helpMode, string? tree, bool leads)
    {
        Assert.Equal(leads, StartAdmission.LeadsPiece(Marks(kind, repository, ask), helpMode, tree));
    }
}
