using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1h (the fabric design §E5, D173's note): the result a landing and its gates judge. A closure graph's quest at its
/// submission reads as the done it stands for, held or not, so the landing and its gates wait on an unheld submission where they wait
/// on an unheld done; every other quest reads exactly as it stands, so the owner's install, which binds nothing, reads as before.
/// </summary>
public sealed class QuestResultsTests
{
    private const string Graph = "sha256:closure";

    private static QuestView Quest(string status) => new("q1", "game", "engine", "Fix the gap", "Fix it.", status);

    private static QuestSubmissionView Made(long sequence) => new("desk", sequence, DateTimeOffset.UnixEpoch, "ready");

    public static TheoryData<string, QuestView?, bool, bool, string?> Rows => new()
    {
        { "a done", Quest("Done"), false, true, "Done" },
        { "a held done", Quest("Done") with { Held = true, Hold = EvidenceCodes.Departed }, false, true, "Done" },
        { "an unheld submission", Quest("Taken") with { Closure = Graph, Submission = Made(3) }, true, true, "Done" },
        { "a held submission", Quest("Taken") with { Closure = Graph, Submission = Made(3), Held = true, Hold = EvidenceCodes.MissingHold }, true, true, "Done" },
        { "bound, with none made or one reopened", Quest("Taken") with { Closure = Graph }, false, false, "Taken" },
        { "a submission read with no graph", Quest("Taken") with { Submission = Made(3) }, false, false, "Taken" },
        { "taken, unbound", Quest("Taken"), false, false, "Taken" },
        { "open", Quest("Open"), false, false, "Open" },
        { "declined", Quest("Declined"), false, false, "Declined" },
        { "none", null, false, false, null },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void A_submission_reads_as_its_done_and_every_other_quest_as_it_stands(
        string row, QuestView? quest, bool submitted, bool judged, string? status)
    {
        Assert.True(submitted == QuestResults.Submitted(quest), row);
        Assert.True(judged == QuestResults.Judged(quest), row);
        Assert.True(status == QuestResults.Status(quest), row);
    }

    [Fact]
    public void Two_reads_stand_on_one_result_only_at_the_same_submission()
    {
        var done = Quest("Done");
        var at3 = Quest("Taken") with { Closure = Graph, Submission = Made(3) };

        Assert.True(QuestResults.Same(done, done with { Held = true }));
        Assert.True(QuestResults.Same(at3, at3 with { Held = true, Submission = Made(3) with { Note = "again" } }));
        Assert.False(QuestResults.Same(at3, at3 with { Submission = Made(4) }), "a later ready is another submission");
        Assert.False(QuestResults.Same(at3, at3 with { Submission = Made(3) with { Machine = "laptop" } }));
        Assert.False(QuestResults.Same(at3, at3 with { Submission = null }), "a reopen takes the submission back");
        Assert.False(QuestResults.Same(at3, null));
    }
}
