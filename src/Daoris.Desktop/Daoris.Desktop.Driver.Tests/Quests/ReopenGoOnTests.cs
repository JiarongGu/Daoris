using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1c5 (§E5): which commit a step the person went on with is brought up to date with. Its source's current submission's,
/// where the step grew from that one now, and none otherwise. Runs no process: the fast half (MOD8).
/// </summary>
public sealed class ReopenGoOnTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static readonly QuestView Step =
        new("q8", "game", "engine", "Next", "", "Taken") { Parent = "q7", GrewFrom = new QuestSubmissionRef("desk", 43) };

    private static QuestView Source(QuestSubmissionView? submission) =>
        new("q7", "game", "engine", "Ship it", "", "Taken") { Closure = "gph-0123", Submission = submission };

    [Fact]
    public void The_sources_current_submission_names_the_commit_where_the_step_grew_from_it()
    {
        Assert.Equal(Commit, Planner.SourceCommit(Step, Source(new QuestSubmissionView("desk", 43, null, "ready", Commit))));
    }

    [Fact]
    public void No_commit_where_the_source_is_unread_names_another_submission_none_or_no_commit()
    {
        Assert.Null(Planner.SourceCommit(Step, null));
        Assert.Null(Planner.SourceCommit(Step, Source(null)));
        Assert.Null(Planner.SourceCommit(Step, Source(new QuestSubmissionView("desk", 44, null, "ready", Commit))));
        Assert.Null(Planner.SourceCommit(Step, Source(new QuestSubmissionView("laptop", 43, null, "ready", Commit))));
        Assert.Null(Planner.SourceCommit(Step, Source(new QuestSubmissionView("desk", 43, null, "ready"))));
        Assert.Null(Planner.SourceCommit(Step with { GrewFrom = null }, Source(new QuestSubmissionView("desk", 43, null, "ready", Commit))));
        Assert.Null(Planner.SourceCommit(Step, Source(new QuestSubmissionView("desk", 43, null, "ready", Commit)) with { Id = "q6" }));
    }

    // FABRIC-CLOSE1c13: which going-on is brought up to date with the source first. The session the reopen stopped, as CLOSE1c5 has
    // it, and a session that ended normally going on with the words Daoris told it of the go-on to the submission the step grows
    // from now; every other going-on syncs with the line alone, as before.

    private static PriorSession Record(params string[] words) =>
        new("s1", "D:/trees/s1", "awaiting-person", "It stopped with its quest still taken, to ask you:", Repository: "engine")
        {
            Said = [.. words.Select((text, at) => new SaidWordView($"w{at}", text, DateTimeOffset.UnixEpoch, [], Reopens: false))],
        };

    [Fact]
    public void A_session_going_on_with_the_go_ons_words_is_brought_up_to_date_with_the_source()
    {
        var told = ReopenTelling.GoneOnWords("q8", "q7", new QuestSubmissionRef("desk", 43));

        Assert.True(ReopenTelling.BringsToSource(Step, Record(told)));
        Assert.True(ReopenTelling.BringsToSource(Step, Record("Also add the changelog line.", told)));
    }

    [Fact]
    public void Any_other_going_on_is_synced_with_the_line_alone()
    {
        var told = ReopenTelling.GoneOnWords("q8", "q7", new QuestSubmissionRef("desk", 43));

        Assert.False(ReopenTelling.BringsToSource(Step, Record()));
        Assert.False(ReopenTelling.BringsToSource(Step, Record("Also add the changelog line.")));
        // Told of a go-on to another submission, which a later go-on moved past, or for another quest.
        Assert.False(ReopenTelling.BringsToSource(Step, Record(ReopenTelling.GoneOnWords("q8", "q7", new QuestSubmissionRef("desk", 41)))));
        Assert.False(ReopenTelling.BringsToSource(Step, Record(ReopenTelling.GoneOnWords("q9", "q7", new QuestSubmissionRef("desk", 43)))));
        // A step grown from no submission, or one with a result again.
        Assert.False(ReopenTelling.BringsToSource(Step with { GrewFrom = null }, Record(told)));
        Assert.False(ReopenTelling.BringsToSource(Step with { Submission = new QuestSubmissionView("desk", 47, null, "ready") }, Record(told)));
    }
}
