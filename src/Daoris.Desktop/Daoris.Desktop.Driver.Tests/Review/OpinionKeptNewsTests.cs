using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BUS2b2: an author's answers reach the bus's log as they are given. The service journals each keep of the answers (its trigger), the
/// change stream wakes the lease holder's journal teller, and the teller tees the row as <c>opinion.kept</c>, an observation counted as the
/// answers stand: so an answer given mid-turn is in the log before the turn ends, and the gate still judges only the turn's end.
/// </summary>
public sealed class OpinionKeptNewsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "opinion-kept", Guid.NewGuid().ToString("N")[..8]);

    public OpinionKeptNewsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static JournalEntry Row(string[]? marks = null, string family = "opinions", string change = "answers-kept", string record = "op-1", string? from = "s-1", long position = 7) =>
        new(position, 1_700_000_000_000, record, family, change, null, null, null, null, null, marks ?? ["fixed:1", "rejected:0", "unresolved:2"], null, null) { FromSession = from };

    private static object? Of(BusDraft draft, string key) => draft.Payload.Single(pair => pair.Key == key).Value;

    /// <summary>🔴 An answers-kept row is the observed fact of its opinion, the counts as the row says them; any other row is none.</summary>
    [Fact]
    public void An_answers_kept_row_is_the_kept_fact_of_its_opinion_and_any_other_row_is_none()
    {
        var draft = OpinionKeptNews.Of(Row())!;
        Assert.Same(BusTypes.OpinionKept, draft.Type);
        Assert.Equal("op-1", draft.Subject);
        Assert.Equal(("s-1", 1L, 0L, 2L), ((string?)Of(draft, "session")!, Convert.ToInt64(Of(draft, "fixed")), Convert.ToInt64(Of(draft, "rejected")), Convert.ToInt64(Of(draft, "unresolved"))));
        Assert.False(draft.Commit);

        Assert.Null(OpinionKeptNews.Of(Row(family: "sessions")));
        Assert.Null(OpinionKeptNews.Of(Row(change: "state")));
        Assert.Null(OpinionKeptNews.Of(Row(record: "")));
    }

    /// <summary>🔴 A count the row does not say stays unknown, never zero.</summary>
    [Fact]
    public void A_count_the_row_does_not_say_is_unknown_never_zero()
    {
        var draft = OpinionKeptNews.Of(Row(marks: ["fixed:x", "unresolved:3"], from: null))!;
        Assert.Null(Of(draft, "session"));
        Assert.Null(Of(draft, "fixed"));
        Assert.Null(Of(draft, "rejected"));
        Assert.Equal(3L, Convert.ToInt64(Of(draft, "unresolved")));
    }

    /// <summary>
    /// 🔴 The teller tees each answers-kept row of a batch into the log as it is handed, before any turn ends: the log holds each in the order
    /// the journal gave them, and a row that is no answer is not teed.
    /// </summary>
    [Fact]
    public async Task The_teller_tees_each_kept_row_into_the_log_in_order_and_no_other()
    {
        await using var bus = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = new MovedClock() });
        var teller = OpinionKeptNews.Teller(bus, null, null);

        await teller.Told(new JournalBatch("store-1", 1, 9, [Row(position: 7), Row(family: "sessions", change: "state", position: 8), Row(["fixed:2", "rejected:0", "unresolved:0"], position: 9)]));
        await bus.FlushAsync();

        using var store = new DesktopBusLogStore(_home);
        var kept = store.Read(0, 100).Where(entry => entry.Kind == BusTypes.OpinionKept.Kind).ToList();
        Assert.Equal(2, kept.Count);
        Assert.Equal([1L, 2L], kept.Select(entry => Convert.ToInt64(entry.Payload.Single(pair => pair.Key == "fixed").Value)));
        Assert.All(kept, entry => Assert.Equal("op-1", entry.Subject));
    }
}
