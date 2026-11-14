using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run still going on whose work ended done under the older meaning asks once, at its look, that its sessions wait on its next step; a run
/// that ended, or whose work no done ended, asks nothing.
/// </summary>
public sealed class RunSessionReopensTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-15T10:00:00Z");

    private static QuestView Quest(string status, long? submitted = null) =>
        new("q1", "Asker", "Owner", "Do the thing", "Here is why.", status)
        {
            Closure = submitted is null ? null : "sha256:0f1e2d3c4b5a6978",
            Submission = submitted is { } sequence ? new QuestSubmissionView("m1", sequence, At, "Built.") : null,
        };

    [Fact]
    public async Task A_run_going_on_whose_quest_ended_done_asks_once_that_its_sessions_wait_on_its_next_step()
    {
        var sent = new List<RunReopen>();
        var asked = new HashSet<string>(StringComparer.Ordinal);
        var said = new List<string>();
        Task<RunReopenResponse?> Reopen(RunReopen reopen, CancellationToken _)
        {
            sent.Add(reopen);
            return Task.FromResult<RunReopenResponse?>(new RunReopenResponse("Run `q1`: 1 session(s) ended done wait on its next step.", ["s1"]));
        }

        await RunSessionReopens.AskAsync(Reopen, asked, "q1", null, [Quest("Done")], ["s1", "s1"], "check", said, CancellationToken.None);
        await RunSessionReopens.AskAsync(Reopen, asked, "q1", null, [Quest("Done")], ["s1"], "check", said, CancellationToken.None);

        var reopen = Assert.Single(sent);
        Assert.Equal(["s1"], reopen.Sessions);
        Assert.Equal(new StepWait("q1", "check"), StepWaits.From(reopen.Note.Parts));
        Assert.Contains("ended done wait on its next step", Assert.Single(said), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_that_ended_or_whose_work_no_done_ended_asks_nothing()
    {
        var sent = new List<RunReopen>();
        Task<RunReopenResponse?> Reopen(RunReopen reopen, CancellationToken _)
        {
            sent.Add(reopen);
            return Task.FromResult<RunReopenResponse?>(null);
        }

        await RunSessionReopens.AskAsync(Reopen, [], "q1", new RunEnd("q1", At), [Quest("Done")], ["s1"], null, [], CancellationToken.None);
        await RunSessionReopens.AskAsync(Reopen, [], "q1", null, [Quest("Taken")], ["s1"], null, [], CancellationToken.None);
        await RunSessionReopens.AskAsync(Reopen, [], "q1", null, [Quest("Done")], [], null, [], CancellationToken.None);
        Assert.Empty(sent);

        await RunSessionReopens.AskAsync(Reopen, [], "q1", null, [Quest("Taken", 4)], ["s1"], null, [], CancellationToken.None);
        Assert.Single(sent);
    }
}
