using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 m-ii and m-iii (the foundation design §4.2, §4.5, §19.6): the news a journal row is, told by a teller that claims each
/// occurrence's key before it shows anything. A park is a move into the state that waits on the person, and a restart tells the one made
/// just before it, once; an end is the journal's own <c>part.ended</c>, judged by the one table.
/// </summary>
public sealed class NewsTellerTests : IDisposable
{
    private static readonly Func<StoreSettled> Settled = () => StoreSettled.Yes;

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "news-teller", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<NewsTold> _shown = [];

    public NewsTellerTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static long Ms(DateTimeOffset at) => at.ToUnixTimeMilliseconds();

    private NewsTeller Teller(string name = "lease", Func<JournalEntry, bool>? takes = null, List<JournalBatch>? incomplete = null) =>
        new(new News(_home, Settled), name, takes ?? (_ => true), () => DateTimeOffset.UtcNow, told => { lock (_shown) _shown.AddRange(told); return Task.CompletedTask; })
        {
            Incomplete = batch => incomplete?.Add(batch),
        };

    private static JournalEntry Row(long position, string old, string state, string marks = "driven", string? codes = null, string record = "s1", bool limited = false) =>
        new(position, Ms(DateTimeOffset.UtcNow.AddSeconds(-5)), record, "sessions", "state", old, state, false, limited, codes?.Split(','), marks.Split(','), "team", "q1");

    private static JournalBatch Batch(params JournalEntry[] rows) => new("store-1", 1, rows.Max(row => row.Position), rows);

    // ---- m-ii: parks ----

    /// <summary>A move into the state that waits on the person is a park, told once with the occurrence's key.</summary>
    [Fact]
    public async Task A_park_is_told_once()
    {
        await Teller().TellAsync(Batch(Row(5, "Working", "AwaitingPerson")));
        await Teller().TellAsync(Batch(Row(5, "Working", "AwaitingPerson")));

        var park = Assert.Single(_shown);
        Assert.Equal((AttentionKind.Parked, "s1", 5L, "store-1"), (park.Kind, park.Session, park.Position, park.Store));
    }

    /// <summary>A park made just before a restart, never read, is told once after it: the cursor the dead process kept is where the next reads from.</summary>
    [Fact]
    public async Task A_park_made_just_before_a_restart_is_told_once_after_it()
    {
        // A batch with no news is moved past by the feed's own keep, the teller writing nothing for it.
        await Teller().TellAsync(Batch(Row(4, "Queued", "Working")));
        Assert.Null(new News(_home, Settled).Advance("lease", new JournalPosition("store-1", 4)));
        Assert.Equal(new JournalPosition("store-1", 4), new News(_home, Settled).Cursor("lease"));
        Assert.Empty(_shown);

        // The process died before it read the row at 5; the restarted one reads from the kept cursor.
        await Teller().TellAsync(Batch(Row(5, "Working", "AwaitingPerson")));
        await Teller().TellAsync(Batch(Row(5, "Working", "AwaitingPerson")));

        Assert.Equal([5L], _shown.Select(told => told.Position));
        Assert.Equal(new JournalPosition("store-1", 5), new News(_home, Settled).Cursor("lease"));
    }

    /// <summary>A park, its answer and a park again are two parks at two positions; a same-state write and a reopen are none.</summary>
    [Fact]
    public async Task A_park_its_answer_and_a_park_again_are_two_parks()
    {
        await Teller().TellAsync(Batch(
            Row(5, "Working", "AwaitingPerson"), Row(6, "AwaitingPerson", "AwaitingPerson"), Row(7, "AwaitingPerson", "Working"), Row(8, "Working", "AwaitingPerson")));

        Assert.Equal([5L, 8L], _shown.Select(told => told.Position));
    }

    /// <summary>The conversations are the shell's and the driven work the lease's: a row another teller takes is neither told nor claimed here, so that teller tells it.</summary>
    [Fact]
    public async Task A_row_another_teller_takes_is_left_unclaimed()
    {
        var chat = Row(5, "Working", "AwaitingPerson", marks: "chat");

        await Teller("lease", row => !row.Marks.Contains("chat")).TellAsync(Batch(chat));
        Assert.Empty(_shown);
        await Teller("shell", row => row.Marks.Contains("chat")).TellAsync(Batch(chat));

        Assert.Equal([5L], _shown.Select(told => told.Position));
    }

    /// <summary>
    /// A teller whose cursor is below the floor tells what the journal still holds, none of the gap, and says its history is incomplete since its
    /// cursor (§4.1, §19.6).
    /// </summary>
    [Fact]
    public async Task A_teller_below_the_floor_says_so_and_tells_none_of_the_gap()
    {
        var incomplete = new List<JournalBatch>();
        var below = new JournalBatch("store-1", 90, 95, [Row(94, "Working", "AwaitingPerson")], Missed: true);

        await Teller(incomplete: incomplete).TellAsync(below);

        Assert.Single(incomplete);
        Assert.Equal([94L], _shown.Select(told => told.Position));
    }

}
