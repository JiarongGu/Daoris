using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RECLOCK2 (D173's FABRIC-RECLOCK2 note): the other shared records under FABRIC-END2's lock. <c>sessions/auto-landings.json</c>
/// and the abandon record are each edited holding their own lock under the home's <c>locks/records/</c>, so an edit waits while another
/// handle holds it, then reads what the holder wrote, and neither write is lost. As <see cref="HeldFactsTests"/>' lock test for the two
/// END2 records; the lock file is the contract a second process honours (<see cref="RecordLockProcessTests"/>).
/// </summary>
public sealed class RecordLockStoresTests : IDisposable
{
    private static readonly DateTimeOffset Then = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-record-stores-" + Guid.NewGuid().ToString("N")[..8]);

    public RecordLockStoresTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static AutoLanding Due(string session) => new(session, "q-" + session, "engine", "default", "trees/" + session, Then);

    private static AbandonEntry Abandoned(string id) => new("quest", id, Then, "window", "not needed");

    /// <summary>Each store's lock is its own file under <c>locks/records/</c>, named for its record.</summary>
    [Fact]
    public void Each_store_names_its_own_lock_under_the_records_locks()
    {
        var locks = DaorisHome.Entry(_home, HomeEntry.Locks);
        var due = new AutoLandings(_home).LockPath;
        var abandoned = new AbandonRecord(_home).LockPath;

        Assert.StartsWith(locks, due, StringComparison.Ordinal);
        Assert.StartsWith(locks, abandoned, StringComparison.Ordinal);
        Assert.Equal(RecordLock.PathOf(_home, AutoLandings.FileName), due);
        Assert.Equal(RecordLock.PathOf(_home, AbandonRecord.FileName), abandoned);
        Assert.Equal(AbandonRecord.FileName, Path.GetFileName(new AbandonRecord(_home).FilePath));
        Assert.NotEqual(due, abandoned);
    }

    /// <summary>🔴 <c>auto-landings.json</c>: a due waits for the lock, and the holder's own write stays beside it.</summary>
    [Fact]
    public void A_due_waits_for_the_auto_landings_lock_and_loses_nothing()
    {
        var due = new AutoLandings(_home);
        due.Due(Due("first"));

        WaitsWhileHeld(due.LockPath, due.FilePath, "\"first\"", "\"holder\"", () => due.Due(Due("second")));

        Assert.Equal(["holder", "second"], due.All().Select(each => each.Session));
    }

    /// <summary>🔴 A try kept on an entry waits too.</summary>
    [Fact]
    public void A_try_waits_for_the_auto_landings_lock_and_loses_nothing()
    {
        var due = new AutoLandings(_home);
        due.Due(Due("first"));
        due.Due(Due("second"));

        WaitsWhileHeld(due.LockPath, due.FilePath, "\"first\"", "\"holder\"",
            () => due.Tried("second", new AutoTry(Then, AutoLandingCode.Landed), close: true));

        Assert.Equal(["holder", "second"], due.All().Select(each => each.Session));
        Assert.Equal(AutoLandingCode.Landed, due.Of("second")!.Last!.Code);
    }

    /// <summary>🔴 A history clear's forget waits too, and drops only the closed entry it named.</summary>
    [Fact]
    public void A_forget_waits_for_the_auto_landings_lock_and_loses_nothing()
    {
        var due = new AutoLandings(_home);
        due.Due(Due("first"));
        due.Due(Due("gone"));
        due.Tried("gone", new AutoTry(Then, AutoLandingCode.Landed), close: true);

        WaitsWhileHeld(due.LockPath, due.FilePath, "\"first\"", "\"holder\"", () => due.Forget(["gone"]));

        Assert.Equal(["holder"], due.All().Select(each => each.Session));
    }

    /// <summary>🔴 The abandon record: a press's entry waits for the lock, and the holder's own write stays beside it.</summary>
    [Fact]
    public void An_abandon_waits_for_the_abandon_records_lock_and_loses_nothing()
    {
        var record = new AbandonRecord(_home);
        record.Write(Abandoned("first"));

        WaitsWhileHeld(record.LockPath, record.FilePath, "\"first\"", "\"holder\"", () => record.Write(Abandoned("second")));

        Assert.Equal(["holder", "second"], record.Entries().Select(each => each.Id));
    }

    /// <summary>🔴 A history clear's forget of the abandon record waits too, and drops only what it no longer knows.</summary>
    [Fact]
    public void A_forget_waits_for_the_abandon_records_lock_and_loses_nothing()
    {
        var record = new AbandonRecord(_home);
        record.Write(Abandoned("first"));
        record.Write(Abandoned("gone"));

        WaitsWhileHeld(record.LockPath, record.FilePath, "\"first\"", "\"holder\"", () => record.Forget(entry => entry.Id != "gone"));

        Assert.Equal(["holder"], record.Entries().Select(each => each.Id));
    }

    /// <summary>
    /// Holds <paramref name="lockPath"/> as another process would, starts <paramref name="edit"/>, and once the edit has found the lock
    /// held (<see cref="RecordLock.Waiting"/>, TESTCLOCK2) makes the holder's own read-modify-write, renaming <paramref name="from"/> to
    /// <paramref name="to"/>; then lets go and waits for the edit to finish.
    /// </summary>
    private static void WaitsWhileHeld(string lockPath, string file, string from, string to, Action edit)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        Task running = Task.CompletedTask;
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Waits(string path)
        {
            if (path == lockPath) waiting.TrySetResult();
        }

        RecordLock.Waiting += Waits;
        try
        {
            using var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            running = Task.Run(edit);
            Assert.True(waiting.Task.Wait(Poll.Patience), "the edit never found the lock held");
            Assert.False(running.IsCompleted);
            var text = File.ReadAllText(file);
            Assert.Contains(from, text, StringComparison.Ordinal);
            File.WriteAllText(file, text.Replace(from, to, StringComparison.Ordinal));
        }
        finally
        {
            RecordLock.Waiting -= Waits;
        }

        Assert.True(running.Wait(Poll.Patience));
    }
}
