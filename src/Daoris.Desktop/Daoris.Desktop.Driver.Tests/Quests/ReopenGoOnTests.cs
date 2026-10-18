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
}
