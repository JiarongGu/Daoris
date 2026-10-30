using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 z-v (the design §13.6): <c>data/notices.json</c> imported once into the store's <c>notices</c>, the file left in place as
/// the backup, every writer and reader on the store from the import, with the said-set's and the cursors' tables beside it. Homes under
/// <c>_fixtures/</c>; the settled reading is handed in.
/// </summary>
public sealed class NoticeStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly Func<StoreSettled> Settled = () => StoreSettled.Yes;

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "notice-store", Guid.NewGuid().ToString("N")[..8]);

    public NoticeStoreTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static Notice Ended(string session, DateTimeOffset at) =>
        new($"ended-{session}", Notices.Ended, at, Workspace: "games", Session: session, Repository: "Engine", State: "failed");

    private string File_ => Notices.PathOf(_home);

    private IReadOnlyList<T> InStore<T>(string sql, Func<Microsoft.Data.Sqlite.SqliteDataReader, T> map)
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(_home))!;
        return reader.Query(sql, map);
    }

    /// <summary>The file's notices move into <c>notices</c> once; the file stays as it was, the backup an older build reads.</summary>
    [Fact]
    public void The_file_is_imported_once_and_left_in_place()
    {
        Assert.Null(Notices.Add(_home, Ended("s1", Now.AddMinutes(-5)), Now));
        Assert.Null(Notices.MarkRead(_home, ["ended-s1"], Now));
        Assert.Null(Notices.Add(_home, Ended("s2", Now.AddMinutes(-1)), Now));
        var before = File.ReadAllBytes(File_);

        Assert.Equal(NoticesImported.Done, Notices.ImportFile(_home, Settled).Outcome);
        Assert.Equal(NoticesImported.Already, Notices.ImportFile(_home, Settled).Outcome);

        Assert.Equal(before, File.ReadAllBytes(File_));
        var read = Notices.Read(_home, Now)!;
        Assert.Equal(["ended-s2", "ended-s1"], read.Select(notice => notice.Id));
        Assert.Equal(Ended("s1", Now.AddMinutes(-5)) with { Read = true }, read[1]);
        Assert.Equal([("left", 2)], InStore("SELECT class, rows FROM imports WHERE family = 'notices'", row => (row.GetString(0), row.GetInt32(1))));
    }

    /// <summary>From the import every writer writes the store and never the file, so a reader never meets two homes of one record.</summary>
    [Fact]
    public void After_the_import_every_writer_writes_the_store_alone()
    {
        Assert.Null(Notices.Add(_home, Ended("s1", Now.AddMinutes(-5)), Now));
        Notices.ImportFile(_home, Settled);
        var before = File.ReadAllBytes(File_);

        Assert.Null(Notices.Add(_home, Ended("s2", Now.AddMinutes(-4)), Now));
        Assert.Null(Notices.Add(_home, Ended("s2", Now) with { State = "done" }, Now));
        Assert.Null(Notices.Renew(_home, Ended("s1", Now.AddMinutes(-1)), Now));
        Assert.Null(Notices.MarkRead(_home, ["ended-s2", "nothing"], Now));
        Assert.Null(Notices.Add(_home, Ended("s3", Now.AddMinutes(-2)), Now));
        Assert.Null(Notices.Dismiss(_home, ["ended-s3"], Now));

        Assert.Equal(before, File.ReadAllBytes(File_));
        var read = Notices.Read(_home, Now)!;
        Assert.Equal(["ended-s1", "ended-s2"], read.Select(notice => notice.Id));
        Assert.Equal([false, true], read.Select(notice => notice.Read));
        Assert.Equal("failed", read[1].State);
        Assert.Equal(Now.AddMinutes(-1), read[0].At);
    }

    /// <summary>The week and the count of 200 hold in the store as in the file.</summary>
    [Fact]
    public void The_week_and_the_count_hold_in_the_store()
    {
        Assert.Null(Notices.Add(_home, Ended("old", Now.AddDays(-8)), Now.AddDays(-8)));
        Notices.ImportFile(_home, Settled);
        for (var n = 0; n < Notices.Most + 5; n++) Assert.Null(Notices.Add(_home, Ended($"s{n}", Now.AddSeconds(n - 1000)), Now));

        Assert.Equal(Notices.Most, Notices.Read(_home, Now)!.Count);
        Assert.Equal(Notices.Most, InStore("SELECT count(*) FROM notices", row => row.GetInt32(0)).Single());
        Assert.DoesNotContain(Notices.Read(_home, Now)!, notice => notice.Id is "ended-s0" or "ended-old");
    }

    /// <summary>Until the start's confirmation is settled nothing is imported, and the file is the record as before.</summary>
    [Fact]
    public void Nothing_is_imported_before_the_start_is_settled()
    {
        Assert.Null(Notices.Add(_home, Ended("s1", Now.AddMinutes(-5)), Now));

        var held = Notices.ImportFile(_home, () => StoreSettled.Not("swap at started"));

        Assert.Equal(NoticesImported.Held, held.Outcome);
        Assert.Null(Notices.Add(_home, Ended("s2", Now), Now));
        Assert.Contains("ended-s2", File.ReadAllText(File_));
    }

    /// <summary>An unreadable file is evidence: set aside under a dated name as the writer does, and the store begins empty.</summary>
    [Fact]
    public void An_unreadable_file_stays_a_file_as_evidence()
    {
        File.WriteAllText(File_, "{ not json");

        Assert.Equal(NoticesImported.Done, Notices.ImportFile(_home, Settled).Outcome);

        Assert.Empty(Notices.Read(_home, Now)!);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(File_)!, "notices.unreadable-*.json"));
        Assert.Null(Notices.Add(_home, Ended("s1", Now), Now));
        Assert.Single(Notices.Read(_home, Now)!);
    }

    /// <summary>A file from a newer build is neither imported nor written over.</summary>
    [Fact]
    public void A_newer_file_is_left_alone()
    {
        File.WriteAllText(File_, """{ "schemaVersion": 2, "notices": {} }""");

        Assert.Equal(NoticesImported.Unread, Notices.ImportFile(_home, Settled).Outcome);
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)) && InStore("SELECT 1 FROM imports", row => row.GetInt32(0)).Count > 0);
    }

    /// <summary>Two tellers, each on its own connection as two processes are, lose nothing between them.</summary>
    [Fact]
    public async Task Two_tellers_in_two_connections_lose_none()
    {
        Assert.Null(Notices.Add(_home, Ended("first", Now.AddMinutes(-9)), Now));
        Notices.ImportFile(_home, Settled);

        await Task.WhenAll(
            Task.Run(() => { for (var n = 0; n < 25; n++) Assert.Null(Notices.Add(_home, Ended($"a{n}", Now.AddSeconds(-n)), Now)); }),
            Task.Run(() => { for (var n = 0; n < 25; n++) Assert.Null(Notices.Add(_home, Ended($"b{n}", Now.AddSeconds(-n)), Now)); }));

        Assert.Equal(51, Notices.Read(_home, Now)!.Count);
    }

    /// <summary>The said-set and the cursors have their tables, empty, ready for the tellers (§4.5, §13.3).</summary>
    [Fact]
    public void The_said_set_and_the_cursors_have_their_tables()
    {
        Assert.Null(Notices.Add(_home, Ended("s1", Now), Now));
        Notices.ImportFile(_home, Settled);

        Assert.Equal([0], InStore("SELECT count(*) FROM news_said", row => row.GetInt32(0)));
        Assert.Equal([0], InStore("SELECT count(*) FROM journal_cursors", row => row.GetInt32(0)));
    }
}
