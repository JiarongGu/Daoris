using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RECLOCK2 (D173's FABRIC-RECLOCK2 note): the abandon record is edited holding its own lock under the home's <c>locks/records/</c>, so an edit waits while another
/// handle holds it, then reads what the holder wrote, and neither write is lost. As <see cref="HeldFactsTests"/>' lock test; the lock file is the contract a second process honours (<see cref="RecordLockProcessTests"/>).
/// </summary>
public sealed class RecordLockStoresTests : IDisposable
{
    private static readonly DateTimeOffset Then = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-record-stores-" + Guid.NewGuid().ToString("N")[..8]);

    public RecordLockStoresTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static AbandonEntry Abandoned(string id) => new("quest", id, Then, "window", "not needed");

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
