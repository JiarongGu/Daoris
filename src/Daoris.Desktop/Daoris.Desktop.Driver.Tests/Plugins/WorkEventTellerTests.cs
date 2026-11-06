using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// EVENTHOOK1d-i (D244 points 1 and 2, the event channel design §3.1, §3.2): the lease's teller of the quests' moves, each <c>quests</c> row
/// of the journal the kind it says, handed to its sink in position order; a take told nothing; one <c>events.missed</c> at the first place
/// of its life and one for each batch whose history is incomplete; its cursor held in memory and never kept.
/// </summary>
public sealed class WorkEventTellerTests
{
    private static readonly DateTimeOffset Moved = DateTimeOffset.Parse("2026-11-06T10:00:00Z");

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-06T11:00:00Z");

    private readonly List<WorkEvent> _told = [];

    private WorkEventTeller Teller() => new(_told.Add, () => Now);

    private static JournalEntry Quest(long position, string change, string? old, string state, string quest = "48f8ed9121ec", string family = "quests") =>
        new(position, Moved.ToUnixTimeMilliseconds(), quest, family, change, old, state, null, null, null, [], "team", quest);

    private static JournalBatch Batch(bool missed = false, params JournalEntry[] rows) =>
        new("store-1", 1, rows.Length == 0 ? 1 : rows.Max(row => row.Position), rows, missed);

    [Fact]
    public async Task Each_quest_move_is_the_kind_it_says_and_a_take_is_told_nothing()
    {
        await Teller().TellAsync(Batch(false,
            Quest(1, "opened", null, "Open"),
            Quest(2, "state", "Open", "Taken"),
            Quest(3, "state", "Taken", "Done"),
            Quest(4, "state", "Done", "Taken"),
            Quest(5, "state", "Open", "Declined"),
            Quest(6, "state", "Declined", "Open")));

        Assert.Equal(
            [
                new WorkEvent(WorkEventKinds.QuestPublished, Moved) { Quest = "48f8ed9121ec" },
                new WorkEvent(WorkEventKinds.QuestClosed, Moved) { Quest = "48f8ed9121ec", State = "done" },
                new WorkEvent(WorkEventKinds.QuestCloseLost, Moved) { Quest = "48f8ed9121ec", State = "taken" },
                new WorkEvent(WorkEventKinds.QuestClosed, Moved) { Quest = "48f8ed9121ec", State = "declined" },
                new WorkEvent(WorkEventKinds.QuestCloseLost, Moved) { Quest = "48f8ed9121ec", State = "open" },
            ],
            _told);
    }

    /// <summary>
    /// What C5.4 named is gone by construction: a quest published and closed between two looks is two rows, so two events; a held quest's
    /// close is its move, told once, whatever any list keeps. Another family's row, even one naming the quest, is no quest event.
    /// </summary>
    [Fact]
    public async Task A_quest_published_and_closed_between_two_looks_is_two_events_and_another_familys_row_none()
    {
        await Teller().TellAsync(Batch(false,
            Quest(1, "opened", null, "Open", quest: "a1b2c3"),
            Quest(2, "quest-op", null, "done", quest: "a1b2c3", family: "quest_log"),
            Quest(3, "state", "Open", "Done", quest: "a1b2c3"),
            Quest(4, "state", "Working", "Done", quest: "a1b2c3", family: "sessions")));

        Assert.Equal([WorkEventKinds.QuestPublished, WorkEventKinds.QuestClosed], _told.Select(each => each.Kind).ToList());
        Assert.All(_told, each => Assert.Equal("a1b2c3", each.Quest));
    }

    /// <summary>
    /// 🔴 The place a teller with no cursor is handed is a gap: one <c>events.missed</c> at the first place of its life, none at later ones,
    /// and one for each batch whose history is incomplete, before that batch's own events. Its cursor is its memory alone.
    /// </summary>
    [Fact]
    public async Task Its_first_place_and_each_incomplete_batch_are_told_as_one_gap_and_its_cursor_is_memory()
    {
        var teller = Teller();
        var feed = teller.Teller;
        Assert.Null(feed.Load!());

        feed.Keep!(new JournalPosition("store-1", 40));
        feed.Keep!(new JournalPosition("store-1", 41));
        Assert.Equal(new JournalPosition("store-1", 41), feed.Load!());
        Assert.Equal([new WorkEvent(WorkEventKinds.Missed, Now)], _told);

        await feed.Told(Batch(true, Quest(90, "state", "Taken", "Done")));

        Assert.Equal([WorkEventKinds.Missed, WorkEventKinds.Missed, WorkEventKinds.QuestClosed], _told.Select(each => each.Kind).ToList());
        // A second teller's life is its own: it says its own first gap.
        Teller().Teller.Keep!(new JournalPosition("store-1", 41));
        Assert.Equal(3, _told.Count(each => each.Kind == WorkEventKinds.Missed));
    }
}
