using Daoris.Driver;
using Daoris.Driver.Tests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A harness that compacts a session's context is read off its counter falling (<c>usage_update</c>), and Daoris puts back what a
/// summary loses: the quest's requirements, quoted as the start handed them, beside Daoris's brief of the line, as one urgent word.
/// Against a stand-in agent over in-memory streams: no process and no model.
/// </summary>
public sealed class CompactedSessionTests
{
    private static string Usage(long used) =>
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update","used":"""
        + used + ""","size":1000000}}}""";

    private static SessionTarget Target() => new("q1", "Title", "Body", "person", "engine", "root", "http://localhost")
    {
        Requirements = [new QuestRequirementView("the report names its requirements", "a compacted session still names them")],
    };

    [Fact]
    public async Task A_context_counter_that_falls_to_under_half_is_a_compaction_told_once_each()
    {
        var agent = AcpTransientTests.Scripted((n, frame, self) =>
        {
            foreach (var used in new long[] { 40_000, 180_000, 22_000, 60_000, 31_000 }) self.Push(Usage(used));
            return AcpTransientTests.Answer(frame, "end_turn");
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        var told = 0;
        session.Compacted += () => Interlocked.Increment(ref told);

        await session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None).WaitAsync(Poll.Patience);

        // 180000 to 22000 compacted; 22000 to 60000 grew; 60000 to 31000 is not under half. The largest reading stays the usage.
        Assert.Equal(1, told);
        Assert.Equal(180_000, session.Usage!.Used);
    }

    [Fact]
    public void A_small_context_halving_is_no_compaction()
    {
        var drop = new ContextDrop();
        Assert.False(drop.Dropped(9_000));
        Assert.False(drop.Dropped(2_000));
        Assert.False(drop.Dropped(50_000));
        Assert.True(drop.Dropped(20_000));
    }

    [Fact]
    public async Task The_word_names_the_requirements_and_the_brief_and_is_taken_off_when_the_run_ends()
    {
        var said = new StandInSaid();
        using var word = new CompactWord(
            "s1", new DrivenInbox(_ => { }), said, _ => Task.FromResult(CompactedWords.Of(Target(), "BRIEF OF THE LINE")), failed: null);

        Assert.True(await word.TellAsync());

        var kept = Assert.Single(said.Said);
        Assert.Contains("harness compacted your context", kept.Text, StringComparison.Ordinal);
        Assert.Contains("the report names its requirements", kept.Text, StringComparison.Ordinal);
        Assert.Contains("a compacted session still names them", kept.Text, StringComparison.Ordinal);
        Assert.EndsWith("BRIEF OF THE LINE", kept.Text, StringComparison.Ordinal);
        Assert.True(kept.Daoris);
        Assert.StartsWith("compact/s1/", kept.Key, StringComparison.Ordinal);

        word.Dispose();
        Assert.Equal([kept.Id!], said.TakenOff);
    }
}
