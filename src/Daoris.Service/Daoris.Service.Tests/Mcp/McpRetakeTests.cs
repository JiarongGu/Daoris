using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// A take answered by a session of the same quest whose earlier taker ended without finishing is the carry-on's own: the dead
/// holder's take passes to the session that asks, so a retry never stands down against a holder that is no longer there.
/// </summary>
public sealed class McpRetakeTests : McpToolsFixture
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private async Task<(SessionLedger Ledger, SessionStore Sessions)> LedgerAsync()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        return (new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection)), sessions);
    }

    private QuestTools Connector(SessionLedger ledger, string session) =>
        new(_quests, new QuestExchange(_service, _quests, files: _files), In("Owner"), intake: new IntakeScope(null, session), ledger: ledger);

    private static async Task<Session> WorkingAsync(SessionLedger ledger, string quest, DateTimeOffset at)
    {
        var opened = await ledger.OpenAsync(quest, "stub", at);
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        foreach (var state in new[] { "starting", "working" }) await ledger.AdvanceAsync(opened.Session!.Id, state, null, null, null, at);
        return opened.Session!;
    }

    /// <summary>
    /// 🔴 A taker that failed (its agent signed out) still holds the take; the retry's take answered "already taken" and
    /// stood it down. It now answers the take as the retry's own, and the record says it took.
    /// </summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("stopped")]
    public async Task A_take_by_the_retry_of_a_taker_that_ended_is_its_own(string ending)
    {
        var (ledger, sessions) = await LedgerAsync();
        var quest = await _quests.PublishAsync("Asker", "Owner", "Draw the minimap", "The map needs one.", Now);
        var first = await WorkingAsync(ledger, quest.Id, Now);
        Assert.Contains("is now Taken", await Connector(ledger, first.Id).RespondToQuestAsync(quest.Id, "take"));
        await ledger.AdvanceAsync(first.Id, ending, "Failed to authenticate.", null, null, Now.AddMinutes(1), interrupted: ending == "stopped");
        var retry = await WorkingAsync(ledger, quest.Id, Now.AddMinutes(2));

        var answer = await Connector(ledger, retry.Id).RespondToQuestAsync(quest.Id, "take");

        Assert.DoesNotContain("already taken", answer);
        Assert.Contains("yours", answer);
        Assert.True((await sessions.FindAsync(retry.Id))!.Took);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
    }

    /// <summary>A holder still running keeps its take: the second session stands down as before.</summary>
    [Fact]
    public async Task A_take_against_a_holder_that_still_runs_is_refused()
    {
        var (ledger, _) = await LedgerAsync();
        var quest = await _quests.PublishAsync("Asker", "Owner", "Draw the minimap", "The map needs one.", Now);
        var first = await WorkingAsync(ledger, quest.Id, Now);
        await Connector(ledger, first.Id).RespondToQuestAsync(quest.Id, "take");
        var second = await ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(1));

        var answer = await Connector(ledger, second.Session?.Id ?? "s0000000").RespondToQuestAsync(quest.Id, "take");

        Assert.Contains("already taken", answer);
    }
}
