using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 m-i (the foundation design §4.5, §19.6): the news owned in one place, each piece told at most once by its key claimed in the
/// said-set, durably, before anything is shown, and one journal cursor for each teller beside it.
/// </summary>
public sealed class NewsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly Func<StoreSettled> Settled = () => StoreSettled.Yes;

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "news", Guid.NewGuid().ToString("N")[..8]);

    public NewsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private News Desk() => new(_home, Settled);

    private static NewsOccurrence Said(string key, int minutesAgo = 1, Notice? notice = null) => new(key, Now.AddMinutes(-minutesAgo), notice);

    private static Notice Ended(string session) => new($"ended-{session}", Notices.Ended, Now, Session: session, State: "failed");

    /// <summary>A key is told the first time it is claimed and never again, by the same desk or another.</summary>
    [Fact]
    public void A_key_is_told_once()
    {
        var first = Desk().Claim("lease", [Said("a"), Said("b")], null, Now);
        var again = Desk().Claim("lease", [Said("a"), Said("c")], null, Now);

        Assert.Equal(["a", "b"], first.Told.Select(each => each.Key));
        Assert.Equal(["c"], again.Told.Select(each => each.Key));
    }

    /// <summary>
    /// A crash between the claim and the balloon loses that one announcement and repeats none: the claim is durable before anything is shown, so a
    /// new process finds the key claimed.
    /// </summary>
    [Fact]
    public void A_crash_between_the_claim_and_the_balloon_tells_nothing_twice()
    {
        var claimed = Desk().Claim("shell", [Said("park-1")], null, Now);
        Assert.Single(claimed.Told);

        // The process dies here, before showing it. The next one reads the same row again.
        var restarted = Desk().Claim("shell", [Said("park-1")], null, Now);

        Assert.Empty(restarted.Told);
    }

    /// <summary>Two tellers that read one row tell it once between them.</summary>
    [Fact]
    public void Two_tellers_reading_one_row_tell_it_once()
    {
        var lease = Desk().Claim("lease", [Said(News.KeyOf("store-1", 7))], null, Now);
        var shell = Desk().Claim("shell", [Said(News.KeyOf("store-1", 7))], null, Now);

        Assert.Equal([1, 0], [lease.Told.Count, shell.Told.Count]);
    }

    /// <summary>A position alone carries no store: a record event of a store with another identity at an equal position is told as its own.</summary>
    [Fact]
    public void A_position_of_another_store_is_told_as_its_own()
    {
        var first = Desk().Claim("lease", [Said(News.KeyOf("store-1", 7))], null, Now);
        var other = Desk().Claim("lease", [Said(News.KeyOf("store-2", 7))], null, Now);

        Assert.Equal([1, 1], [first.Told.Count, other.Told.Count]);
    }

    /// <summary>Each teller has a cursor of its own, with the store it was read against, and a claim moves it in the transaction that claims.</summary>
    [Fact]
    public void Each_teller_keeps_a_cursor_of_its_own_and_a_claim_moves_it()
    {
        var desk = Desk();
        Assert.Null(desk.Cursor("lease"));

        desk.Claim("lease", [Said("a")], new JournalPosition("store-1", 12), Now);
        desk.Advance("shell", new JournalPosition("store-1", 4));

        Assert.Equal(new JournalPosition("store-1", 12), desk.Cursor("lease"));
        Assert.Equal(new JournalPosition("store-1", 4), desk.Cursor("shell"));
        desk.Advance("lease", new JournalPosition("store-2", 1));
        Assert.Equal(new JournalPosition("store-2", 1), desk.Cursor("lease"));
        Assert.Equal(new JournalPosition("store-1", 4), desk.Cursor("shell"));
    }

    /// <summary>
    /// A notice a piece of news carries is kept in the same transaction as its claim, so a claim the store refuses (the key held already) writes no
    /// second notice and a key told leaves its notice, whichever teller told it.
    /// </summary>
    [Fact]
    public void A_notice_is_kept_with_its_claim_and_never_beside_a_refused_one()
    {
        Assert.Null(Notices.Add(_home, Ended("s0"), Now));
        Notices.ImportFile(_home, Settled);

        Desk().Claim("lease", [Said("end-1", notice: Ended("s1"))], null, Now);
        Desk().Claim("shell", [Said("end-1", notice: Ended("s1") with { State = "stopped" })], null, Now);

        var kept = Notices.Read(_home, Now)!;
        Assert.Equal(["ended-s0", "ended-s1"], kept.Select(notice => notice.Id).Order());
        Assert.Equal("failed", kept.Single(notice => notice.Id == "ended-s1").State);
    }

    /// <summary>A store that refuses the claim shows nothing, which keeps a piece of news at most once.</summary>
    [Fact]
    public void A_store_that_refuses_the_claim_tells_nothing()
    {
        // A store a newer Daoris stamped: this build writes nothing there.
        using (DesktopStore.Open(
            DesktopStore.LocationIn(_home),
            new DesktopStoreOptions
            {
                Writer = 2, Settled = Settled,
                Steps = [.. DesktopStore.Steps, new StoreMigration(DesktopStore.Steps.Count + 1, 2, write => write.Execute("CREATE TABLE raised (x TEXT NOT NULL)"))],
            }))
        {
        }

        var told = Desk().Claim("lease", [Said("a")], null, Now);

        Assert.Empty(told.Told);
        Assert.NotNull(told.Refused);
    }
}
