using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The files read, changed and written whole by any process on the home (<c>windows.json</c>, <c>reads.json</c>, <c>epochs.json</c>)
/// are edited under their keep lock, as <c>cooling.json</c> is: an edit waits while another writer holds it, and writes nothing before.
/// </summary>
public sealed class KeptFileLocksTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-kept-locks-" + Guid.NewGuid().ToString("N")[..8]);

    public KeptFileLocksTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The writer, run while another process's lock on the file is held: it waits, and writes once the lock is let go.</summary>
    private static async Task WaitsForTheLock(string file, Action write, Func<string> read)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        Task writing;
        var before = read();
        using (KeepLock.Take(file))
        {
            writing = Task.Run(write);
            await Task.Delay(400);
            Assert.False(writing.IsCompleted, "the edit did not wait for the lock");
            Assert.Equal(before, read());
        }

        await writing.WaitAsync(Poll.Patience);
        Assert.NotEqual(before, read());
    }

    private static string Text(string file) => File.Exists(file) ? File.ReadAllText(file) : "";

    [Fact]
    public Task A_weekly_limit_told_waits_for_the_windows_lock() => WaitsForTheLock(
        AccountWindows.PathOf(_home),
        () => AccountWindows.Told(_home, "claude-code", "account-1", Now.AddHours(100), Now, "s1"),
        () => Text(AccountWindows.PathOf(_home)));

    [Fact]
    public Task A_window_said_waits_for_the_windows_lock() => WaitsForTheLock(
        AccountWindows.PathOf(_home),
        () => AccountWindows.Said(_home, "claude-code", "account-1", [new WindowReading("session", 0.5, Now.AddHours(3))], Now, "s1"),
        () => Text(AccountWindows.PathOf(_home)));

    [Fact]
    public Task A_sign_in_read_waits_for_the_reads_lock() => WaitsForTheLock(
        AccountReads.PathOf(_home),
        () => AccountReads.Keep(_home, "claude-code", "account-1", LoginState.In, Now),
        () => Text(AccountReads.PathOf(_home)));

    [Fact]
    public Task A_binary_read_waits_for_the_reads_lock() => WaitsForTheLock(
        AccountReads.PathOf(_home),
        () => BinaryReads.Keep(_home, "claude-code-acp", new BinaryRead([], Present: false, null, null, Now)),
        () => Text(AccountReads.PathOf(_home)));

    [Fact]
    public Task A_sign_out_marked_in_the_history_waits_for_the_epochs_lock()
    {
        var epochs = DaorisHome.Entry(_home, HomeEntry.UsageHistory, "epochs.json");
        var window = new WindowSeen("seven_day", "weekly", null, 10080, 0.14, 0.01, Now.AddHours(100), UsageHistory.ResetSaid);
        UsageHistory.Kept(
            _home, new ReadingFrom("claude-code", "account-1", "claude-code-acp", UsageHistory.SourceFrame, 1, Now, "3f9c2a71", "max", "get_usage"),
            [window], Now);

        return WaitsForTheLock(
            epochs, () => UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Out, null, Now), () => Text(epochs));
    }
}
