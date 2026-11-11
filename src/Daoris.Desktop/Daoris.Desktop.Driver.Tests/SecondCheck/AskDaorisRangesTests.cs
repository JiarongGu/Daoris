using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A selected turn's place in its conversation's ranges: joined to the queued range while the packet's bound holds, a new range past it or
/// once a reader took the queued one, a closed <i>disabled</i> report while the check is off, nothing for a turn that only read; a range
/// queued a day closes unread; and the catch-up at start selects only what ended past the conversation's seen mark.
/// </summary>
public sealed class AskDaorisRangesTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ask-daoris-ranges", Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = Start;

    public void Dispose() => GitSeed.Discard(_home);

    private AskDaorisReports Reports => new(_home);

    private AskDaorisRanges Ranges(int bound = AskDaorisRanges.Bound) => new(Reports, () => _now, bound);

    private static ReadTurn Turn(string id, int size = 100, bool selected = true, long seq = 1) => new(
        id, seq, seq + 1, Start, Start, false, "words", selected ? [new SelectedCall("c-" + id, SelectedWhy.Act, "edit", "edit", null, "completed", [], new string('x', size), null, [])] : [],
        [], [], new Dictionary<string, int>(), []);

    [Fact]
    public void Turns_that_end_while_their_range_is_queued_join_it_and_a_turn_that_only_read_is_written_nowhere()
    {
        var ranges = Ranges();

        var first = ranges.Take("s1", Turn("w1"), on: true)!;
        var second = ranges.Take("s1", Turn("w2"), on: true)!;
        var read = ranges.Take("s1", Turn("w3", selected: false), on: true);

        Assert.Null(read);
        Assert.Equal(first.Range, second.Range);
        var report = Assert.Single(Reports.Of("s1"));
        Assert.Equal(AskDaorisReport.Queued, report.State);
        Assert.Equal(["w1", "w2"], report.Turns.Select(turn => turn.Id));
    }

    [Fact]
    public void A_turn_past_the_bound_opens_the_next_range_and_one_alone_over_it_is_a_range_of_its_own_marked()
    {
        var ranges = Ranges(bound: 1_000);

        var first = ranges.Take("s1", Turn("w1", size: 500), on: true)!;
        var next = ranges.Take("s1", Turn("w2", size: 500), on: true)!;
        var over = ranges.Take("s1", Turn("w3", size: 5_000), on: true)!;

        Assert.NotEqual(first.Range, next.Range);
        Assert.NotEqual(next.Range, over.Range);
        Assert.Contains(over.Limits, limit => limit.Code == ReadLimit.OverBound);
        Assert.Equal(3, Reports.Of("s1").Count);
    }

    [Fact]
    public void A_range_a_reader_took_never_grows_and_each_conversation_has_its_own()
    {
        var ranges = Ranges();
        var taken = ranges.Take("s1", Turn("w1"), on: true)!;
        Reports.Write(taken with { State = AskDaorisReport.Running });

        var after = ranges.Take("s1", Turn("w2"), on: true)!;
        var other = ranges.Take("s2", Turn("w9"), on: true)!;

        Assert.NotEqual(taken.Range, after.Range);
        Assert.Equal(["w1"], Reports.Of("s1").Single(report => report.Range == taken.Range).Turns.Select(turn => turn.Id));
        Assert.Equal("s2", Assert.Single(Reports.Of("s2")).Session);
        Assert.NotEqual(after.Range, other.Range);
    }

    [Fact]
    public void While_the_check_is_off_a_selected_turn_is_kept_in_a_report_closed_disabled()
    {
        var report = Ranges().Take("s1", Turn("w1"), on: false)!;

        Assert.Equal(AskDaorisReport.Disabled, report.State);
        Assert.NotNull(report.Closed);
        Assert.Equal(AskDaorisReport.Disabled, Assert.Single(Reports.Of("s1")).State);
    }

    [Fact]
    public void A_range_queued_for_a_day_closes_unread_with_why_and_the_next_turn_opens_the_next()
    {
        var ranges = Ranges();
        var queued = ranges.Take("s1", Turn("w1"), on: true)!;

        _now = Start.AddHours(23);
        Assert.Equal(0, ranges.CloseStale());
        _now = Start.AddHours(24).AddMinutes(1);
        Assert.Equal(1, ranges.CloseStale());

        var closed = Reports.Of("s1").Single(report => report.Range == queued.Range);
        Assert.Equal(AskDaorisReport.Unread, closed.State);
        Assert.NotNull(closed.Why);
        Assert.NotEqual(queued.Range, ranges.Take("s1", Turn("w2"), on: true)!.Range);
    }

    [Fact]
    public void The_catch_up_selects_only_turns_ended_since_the_conversation_was_last_seen_and_a_new_one_from_now()
    {
        var ranges = Ranges();
        IReadOnlyList<ReadTurn> log = [Turn("w1", seq: 1), Turn("w2", seq: 3), Turn("w3", seq: 5)];

        Assert.Empty(ranges.CatchUp("s1", log, on: true));
        Assert.Empty(Reports.Of("s1"));
        Assert.Equal(6, Reports.Seen("s1"));

        IReadOnlyList<ReadTurn> grown = [.. log, Turn("w4", seq: 7), Turn("w5", seq: 9, selected: false)];
        var taken = ranges.CatchUp("s1", grown, on: true);

        Assert.Equal(["w4"], taken.SelectMany(report => report.Turns).Select(turn => turn.Id).Distinct());
        Assert.Equal(10, Reports.Seen("s1"));
        Assert.Empty(ranges.CatchUp("s1", grown, on: true));
    }

    [Fact]
    public void A_turn_s_end_takes_a_conversation_never_seen_from_this_run_s_start_and_one_seen_from_its_mark()
    {
        var ranges = Ranges();
        IReadOnlyList<ReadTurn> log = [Turn("w1", seq: 1) with { Ended = Start.AddMinutes(-5) }, Turn("w2", seq: 3) with { Ended = Start.AddMinutes(1) }];

        var first = ranges.Ended("s1", log, on: true, running: Start);
        var again = ranges.Ended("s1", [.. log, Turn("w3", seq: 5)], on: true, running: Start);

        Assert.Equal(["w2"], first.SelectMany(report => report.Turns).Select(turn => turn.Id));
        Assert.Equal(["w2", "w3"], Assert.Single(Reports.Of("s1")).Turns.Select(turn => turn.Id));
        Assert.Single(again);
    }
}
