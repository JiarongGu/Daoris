using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BGWAIT1b: the one judgement a start and a resumed run share for a turn that ends on the session's own background work
/// (BGWAIT1): wait, as a park would leave the quest, or end as before. In-process over the stand-in ledger, the real client
/// and the real rule; the resumed run itself is the full set's, since the ledger's stand-in runs do not run
/// <c>Driver.ContinueAsync</c>.
/// </summary>
public sealed class BackgroundWaitJudgementTests : IDisposable
{
    private readonly StandInLedger _ledger = new();

    public BackgroundWaitJudgementTests()
    {
        _ledger.Register("engine", "engine-root");
        _ledger.Publish("q1", "engine");
        _ledger.Park("s1", "q1", "tree");
    }

    public void Dispose() => _ledger.Dispose();

    private static QuestView Planned(string status, string? awaits = null) =>
        new("q1", "game", "engine", "The work of #q1", "Stand-in work.", status) { Awaits = awaits };

    private async Task<bool?> JudgeAsync(QuestView planned, bool resumed)
    {
        var judge = Daoris.Driver.Driver.WaitsOnBackground(_ledger.Client(), planned, "s1", resumed);
        return judge is null ? null : await judge(CancellationToken.None);
    }

    [Fact]
    public async Task A_resumed_run_whose_quest_is_still_taken_waits()
    {
        Assert.True(await JudgeAsync(Planned("Taken"), resumed: true));
    }

    [Fact]
    public async Task A_resumed_run_on_a_quest_closed_as_planned_never_waits()
    {
        Assert.Null(await JudgeAsync(Planned("Done"), resumed: true));
        Assert.Null(await JudgeAsync(Planned("Declined"), resumed: true));
    }

    [Fact]
    public async Task A_resumed_run_that_asked_another_repository_anew_does_not_wait()
    {
        _ledger.Awaiting("q1", "other");

        Assert.False(await JudgeAsync(Planned("Taken"), resumed: true));
    }

    /// <summary>
    /// DONEEND1: the judgement is read again while the session waits, each read the quest as it stands, so a quest that reached done
    /// meanwhile ends the wait.
    /// </summary>
    [Fact]
    public async Task The_same_judgement_read_again_turns_false_once_the_quest_is_done()
    {
        var judge = Daoris.Driver.Driver.WaitsOnBackground(_ledger.Client(), Planned("Taken"), "s1", resumed: true)!;

        Assert.True(await judge(CancellationToken.None));
        _ledger.Move("q1", "Done");
        Assert.False(await judge(CancellationToken.None));
    }

    /// <summary>DONEEND1: a start's judgement, read again, holds while the quest it took stays taken, and ends once it is done.</summary>
    [Fact]
    public async Task A_start_s_judgement_read_again_holds_while_taken_and_turns_false_once_done()
    {
        _ledger.Driven("s2", "q1", "tree");
        var judge = Daoris.Driver.Driver.WaitsOnBackground(_ledger.Client(), Planned("Open"), "s2", resumed: false)!;

        Assert.True(await judge(CancellationToken.None));
        Assert.True(await judge(CancellationToken.None));
        _ledger.Move("q1", "Done");
        Assert.False(await judge(CancellationToken.None));
    }

    [Fact]
    public async Task A_start_that_did_not_take_its_quest_does_not_wait()
    {
        Assert.False(await JudgeAsync(Planned("Open"), resumed: false));
    }
}
