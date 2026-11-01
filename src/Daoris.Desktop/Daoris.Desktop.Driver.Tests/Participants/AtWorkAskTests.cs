using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1d: the composition of an ask's view (the design §3.5, §R11.5): a branch of its own with no attended part, its lines of work
/// folded to counts and its quests; an ask the asks slice holds with nothing at work is valid and empty, one it does not hold is neither.
/// </summary>
public sealed class AtWorkAskTests
{
    private const string Proc = "a000000000000001";
    private static readonly SliceHealth Judged = new(true);
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 8, 30, 0, TimeSpan.Zero);

    private static LineageReading Lineage(params string[] agents) => LineageReading.Of(ChildBindings.Fold(
        [ChildBindings.StartLine(Proc, "4242 638000000000000000", T0)!, .. agents.Select(agent =>
            new JsonObject { ["at"] = T0.AddSeconds(1).ToString("O"), ["by"] = "stream", ["event"] = "started", ["agent"] = agent, ["proc"] = Proc }.ToJsonString())],
        null, (_, _) => TokenReading.Watched));

    private static AtWorkHeld Compose(
        bool known, IReadOnlyList<AtWorkAskEntryIn> entries, Func<string, AtWorkRecord?>? record = null, Func<AtWorkRecord, LineageReading>? lineage = null,
        SliceHealth? asks = null, SliceHealth? groups = null, AtWorkAsked? half = null) =>
        AtWorkAsk.Compose("a1", known, entries, record ?? (id => new AtWorkRecord(id, "working")), lineage ?? (_ => LineageReading.None),
            groups ?? Judged, Judged, Judged, asks ?? Judged, () => half ?? new AtWorkAsked([], 0, null));

    [Fact]
    public void An_ask_the_slice_holds_with_nothing_at_work_is_valid_and_empty_unlike_an_unknown_one()
    {
        var empty = Compose(known: true, []);
        Assert.Equal([], empty.AskAnswer!.Entries);
        Assert.Null(empty.Answer);
        Assert.Null(empty.Pending);

        var unknown = Compose(known: false, []);
        Assert.Null(unknown.AskAnswer);
        Assert.Equal((true, AtWorkAsk.WaitsAsk), (unknown.Pending, unknown.Waits));
    }

    [Fact]
    public void Each_slice_is_judged_first_and_the_asks_slice_is_one_of_them()
    {
        Assert.Equal(AtWork.WaitsSlices, Compose(true, [], asks: new SliceHealth(false)).Waits);
        Assert.Equal(AtWork.WaitsSlices, Compose(true, [], groups: new SliceHealth(false)).Waits);
        Assert.Equal("the asks could not be read", Compose(true, [], asks: new SliceHealth(false, "the asks could not be read")).Failed?.Why);
        // Judged once and failing now: the answer, stale with the words, not a failure.
        var stale = Compose(true, [], asks: new SliceHealth(true, "timed out"));
        Assert.NotNull(stale.AskAnswer);
        Assert.Equal("timed out", stale.Stale?.Why);
    }

    [Fact]
    public void Each_line_of_work_is_folded_to_its_parts_working_parts_and_subagents()
    {
        var entries = new[]
        {
            new AtWorkAskEntryIn("e1", "engine", "s2", "working", ["s1", "s2"]),
            new AtWorkAskEntryIn("e2", "docs", "s3", "completed", ["s3"]),
        };
        var held = Compose(true, entries,
            record: id => new AtWorkRecord(id, id == "s1" ? "completed" : "working"),
            lineage: record => record.Id == "s2" ? Lineage("c1", "c2") : LineageReading.None);

        var (engine, docs) = (held.AskAnswer!.Entries[0], held.AskAnswer.Entries[1]);
        Assert.Equal(("engine", "s2", "working", 2, 1, 2), (engine.Repository, engine.Opens, engine.Shown, engine.Parts, engine.Working, engine.Subagents));
        Assert.Equal(("docs", 1, 1, 0), (docs.Repository, docs.Parts, docs.Working, docs.Subagents));
        Assert.Null(engine.Elsewhere);
    }

    [Fact]
    public void A_teammates_part_is_counted_a_part_and_its_subagents_are_said_not_visible_here()
    {
        var held = Compose(true, [new AtWorkAskEntryIn("e1", "engine", "s1", "working", ["s1", "kit/s7"])],
            lineage: record => record.Id == "s1" ? Lineage("c1") : throw new InvalidOperationException("a teammate's lineage is on another machine"));

        var entry = held.AskAnswer!.Entries[0];
        Assert.Equal((2, 1, true), (entry.Parts, entry.Subagents, entry.Elsewhere));
    }

    [Fact]
    public void The_entries_are_bounded_and_the_rest_counted()
    {
        var entries = Enumerable.Range(0, AtWork.Bound + 3).Select(i => new AtWorkAskEntryIn($"e{i}", "engine", $"s{i}", "working", [$"s{i}"])).ToList();

        var held = Compose(true, entries);

        Assert.Equal((AtWork.Bound, 3), (held.AskAnswer!.Entries.Count, held.AskAnswer.MoreEntries));
    }

    [Fact]
    public void The_quests_half_is_the_ports_and_absent_where_it_has_none()
    {
        var quest = new AtWorkAskedQuest("q1", "Index it", "Taken") { Taker = new AtWorkTaker { Session = "s1", Agent = "claude-code", State = "working" } };

        var held = Compose(true, [], half: new AtWorkAsked([quest], 2, null));
        Assert.Equal(("q1", 2), (held.AskAnswer!.Quests![0].Id, held.AskAnswer.MoreQuests));
        Assert.Null(Compose(true, []).AskAnswer!.Quests);
    }
}
