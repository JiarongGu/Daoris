using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 z-iii (D212, the design §13.5, §13.6): the loop's look imports the sessions' binding files into the desktop store, so a driver
/// that comes up on a home holding them, or on one an older build wrote a file into during a downgrade, reads each resume's conversation id
/// from the binding. Driven through the real look over a stand-in ledger, in-process, as <see cref="BroughtForwardLookTests"/> drives it;
/// the cases of the import itself are <see cref="BindingsTests"/>'. The scratch home is the case's own and no process is started.
/// </summary>
public sealed class BindingsLookTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bindings-look", Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromMinutes(5));

    public BindingsLookTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private Daoris.Driver.Driver Driver()
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            _ledger.Client(), DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub", Cap = 3, PollSeconds = 1, Trees = ["engine"] },
            adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private string FileOf(string session) => new HarnessConversations(_home).PathOf(session);

    [Fact]
    public async Task A_look_imports_the_binding_files_and_a_look_after_a_downgrade_imports_the_one_an_older_build_wrote()
    {
        var conversations = new HarnessConversations(_home);
        conversations.Keep("s1", "claude-code-acp", "c-1");
        var driver = Driver();

        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.False(File.Exists(FileOf("s1")));
        Assert.True(File.Exists(FileOf("s1") + ".imported"));
        Assert.True(conversations.InStore());
        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1"), conversations.Read("s1"));

        // An older build run on the home meanwhile finds no file, and writes its own when the wire names its conversation.
        Assert.Null(GoOnFiles.ReadConversation(FileOf("s1")));
        GoOnFiles.WriteConversation(FileOf("s1"), "claude-code-acp", "c-1-older");
        File.WriteAllText(FileOf("s2"), "{ not a conversation");

        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.False(File.Exists(FileOf("s1")));
        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1-older"), conversations.Read("s1"));
        Assert.True(File.Exists(FileOf("s2")));
    }

    [Fact]
    public async Task A_look_over_a_home_with_no_binding_file_makes_no_store()
    {
        await Driver().TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }
}
