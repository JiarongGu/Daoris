using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The keep lock over the account files' writers (PUSH1f2e, the alone-path design §1.4): <c>accounts.json</c>'s and
/// <c>cooling.json</c>'s writers hold it from their read to their write, and <c>harnesses.json</c>'s through
/// <see cref="HarnessSettings.Change"/>, so a terminal's change and the window's never decide against a file the other changed
/// meanwhile. Each writer is caught between its read and its write: the lock is held there (another taker is refused), and a change
/// another writer starts then lands after this one's write, on the file as it left it, both kept.
/// </summary>
public sealed class AccountFilesKeepLockTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "account-files-keep-lock", Guid.NewGuid().ToString("N")[..8]);

    public AccountFilesKeepLockTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Whether another taker of <paramref name="file"/>'s lock, on another thread, is refused within a moment: it is held.</summary>
    private static bool Held(string file) =>
        Task.Run(() =>
        {
            try
            {
                using var taken = KeepLock.Take(file, TimeSpan.FromMilliseconds(100));
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }).Result;

    [Fact]
    public async Task An_account_s_name_holds_the_lock_from_its_read_and_a_name_given_meanwhile_is_kept()
    {
        var path = AccountNames.PathOf(Home);
        string[] accounts = ["acct-0000000a", "acct-0000000b"];
        var held = false;
        Task? other = null;
        AccountNames.Rename(Home, "claude-code", accounts, "acct-0000000a", "work", () =>
        {
            held = Held(path);
            other = Task.Run(() => AccountNames.Rename(Home, "claude-code", accounts, "acct-0000000b", "home"));
            other.Wait(TimeSpan.FromMilliseconds(300));
        });
        await other!.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.True(held, "accounts.json's keep lock is held between the read and the write");
        Assert.Equal("work", AccountNames.NameOf(Home, "claude-code", "acct-0000000a"));
        Assert.Equal("home", AccountNames.NameOf(Home, "claude-code", "acct-0000000b"));
        Assert.False(File.Exists(KeepLock.PathOf(path)));
    }

    [Fact]
    public async Task A_cool_off_holds_the_lock_from_its_read_and_one_written_meanwhile_is_kept()
    {
        var path = AccountCooling.PathOf(Home);
        CoolingEntry Entry(string account) => new("claude-code", account, Now.AddHours(3), true, "5h", Now, null);
        var held = false;
        Task? other = null;
        AccountCooling.Cool(Home, Entry("acct-0000000a"), Now, () =>
        {
            held = Held(path);
            other = Task.Run(() => AccountCooling.Cool(Home, Entry("acct-0000000b"), Now));
            other.Wait(TimeSpan.FromMilliseconds(300));
        });
        await other!.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.True(held, "cooling.json's keep lock is held between the read and the write");
        Assert.NotNull(AccountCooling.Of(Home, "claude-code", "acct-0000000a", Now));
        Assert.NotNull(AccountCooling.Of(Home, "claude-code", "acct-0000000b", Now));
        Assert.False(File.Exists(KeepLock.PathOf(path)));
    }

    [Fact]
    public async Task A_wiring_change_holds_the_lock_from_its_read_and_a_change_made_meanwhile_is_kept()
    {
        var path = DaorisHome.Entry(Home, HomeEntry.Harnesses);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "note": "mine" }""");
        var held = false;
        Task? other = null;
        var written = HarnessSettings.Change(path, read => read.WithVersion("claude-code", "1.0.0"), () =>
        {
            held = Held(path);
            other = Task.Run(() => HarnessSettings.Change(path, read => read.WithVersion("codex", "2.0.0")));
            other.Wait(TimeSpan.FromMilliseconds(300));
        });
        await other!.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.True(held, "harnesses.json's keep lock is held between the read and the write");
        Assert.Equal("1.0.0", written.ResolveVersion("claude-code", null, null));
        var file = HarnessSettings.Load(path);
        Assert.Equal("1.0.0", file.ResolveVersion("claude-code", null, null));
        Assert.Equal("2.0.0", file.ResolveVersion("codex", null, null));
        Assert.Equal("mine", JsonNode.Parse(File.ReadAllText(path))!["note"]!.GetValue<string>());
        Assert.False(File.Exists(KeepLock.PathOf(path)));
    }

    /// <summary>An edit the change refuses writes nothing, and lets the lock go.</summary>
    [Fact]
    public void A_wiring_change_its_edit_refuses_writes_nothing()
    {
        var path = DaorisHome.Entry(Home, HomeEntry.Harnesses);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "note": "mine" }""");

        Assert.Throws<DriverException>(() => HarnessSettings.Change(path, _ => throw new DriverException("no.")));

        Assert.Equal("""{ "note": "mine" }""", File.ReadAllText(path));
        Assert.False(File.Exists(KeepLock.PathOf(path)));
    }
}
