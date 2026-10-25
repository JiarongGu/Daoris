using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 z-i (D212, the design §13.5, §19.7): a start's confirmation is settled once its install's swap journal is at no phase the
/// launcher can still roll back from, and a journal that is there and does not read is never settled. The store's imports and stamp-raising
/// migrations wait on that reading, so a rollback before the confirmation meets every file as it was and a store it can still write.
/// </summary>
/// <remarks>
/// Real folders in a scratch install under <c>_fixtures/</c>, journals written by <see cref="StagedBuild.WriteJournal"/> as the launcher
/// writes them; no case sets a variable of the process's environment, <c>DAORIS_HOME</c> least, and none starts a process.
/// </remarks>
public sealed class StartConfirmationTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly string _install = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "start-confirmation", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<IDisposable> _open = [];

    public StartConfirmationTests() => Directory.CreateDirectory(Path.Combine(_install, "update"));

    public void Dispose()
    {
        foreach (var each in _open) each.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_install);
    }

    private void Journal(string phase, string? reason = null, bool? confirmed = null) =>
        StagedBuild.WriteJournal(_install, new SwapRecord(phase, "b1", "0.0.2", null, At, [], Reason: reason, Confirmed: confirmed));

    private string Db => Path.Combine(_install, "data", "desktop.db");

    private DesktopStore Open(DesktopStoreOptions options)
    {
        var store = DesktopStore.Open(Db, options);
        _open.Add(store);
        return store;
    }

    /// <summary>A newer build's options: stamp 2, a step that raises it, and the reading of this install's journal at each ask.</summary>
    private DesktopStoreOptions Newer(string kind = "shell") => new()
    {
        Kind = kind,
        Writer = 2,
        Steps = [.. DesktopStore.Steps, new StoreMigration(DesktopStore.Steps.Count + 1, 2, write => write.Execute("CREATE TABLE raised (x TEXT NOT NULL)"))],
        Settled = () => StartConfirmation.Read(_install),
    };

    // ——— The reading (§19.7).

    [Fact]
    public void A_build_that_runs_from_no_install_and_an_install_with_no_journal_are_settled()
    {
        Assert.True(StartConfirmation.Read(null).Is);
        File.Delete(StagedBuild.JournalOf(_install));
        Assert.True(StartConfirmation.Read(_install).Is);
        Assert.True(StartConfirmation.Read(Path.Combine(_install, "no-such-install")).Is);
    }

    [Theory]
    [InlineData(SwapPhase.Confirmed, true)]
    [InlineData(SwapPhase.Installed, true)]
    [InlineData(SwapPhase.RolledBack, true)]
    [InlineData(SwapPhase.Refused, true)]
    [InlineData(SwapPhase.Swapping, false)]
    [InlineData(SwapPhase.Started, false)]
    public void Each_phase_the_launcher_writes_is_settled_or_one_it_can_still_roll_back_from(string phase, bool settled)
    {
        Journal(phase);

        var read = StartConfirmation.Read(_install);

        Assert.Equal(settled, read.Is);
        Assert.Equal(settled, read.Said is null);
        if (!settled) Assert.Contains($"`{phase}`", read.Said);
    }

    [Fact]
    public void A_started_journal_with_a_reason_is_a_roll_back_stopped_part_way_and_is_not_settled()
    {
        Journal(SwapPhase.Started, reason: "exited");

        var read = StartConfirmation.Read(_install);

        Assert.False(read.Is);
        Assert.Contains("roll-back stopped part way", read.Said);
        Assert.Contains("exited", read.Said);
    }

    [Fact]
    public void The_silent_swap_is_settled_being_installed_though_it_never_confirmed()
    {
        Journal(SwapPhase.Installed, reason: "unconfirmed", confirmed: false);

        Assert.True(StartConfirmation.Read(_install).Is);
    }

    [Fact]
    public void A_phase_this_build_does_not_know_cannot_say_the_launcher_is_done()
    {
        File.WriteAllText(StagedBuild.JournalOf(_install), """{ "phase": "a-phase-from-later", "moves": [] }""");

        var read = StartConfirmation.Read(_install);

        Assert.False(read.Is);
        Assert.Contains("does not know", read.Said);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("""{ "moves": [] }""")]
    [InlineData("[]")]
    public void A_journal_that_is_there_and_does_not_read_is_never_settled_and_says_so(string text)
    {
        File.WriteAllText(StagedBuild.JournalOf(_install), text);
        Assert.Null(StagedBuild.ReadJournal(_install));   // the reader answers it as it answers no journal

        var read = StartConfirmation.Read(_install);

        Assert.False(read.Is);
        Assert.Contains("does not read", read.Said);
    }

    // ——— What a start does with it (§13.5; row z-i's proof).

    [Fact]
    public void A_start_finding_started_imports_nothing_and_raises_no_stamp()
    {
        Journal(SwapPhase.Started);
        var store = Open(Newer());
        Assert.Contains("`started`", store.Held);
        var loaded = false;

        var outcome = store.Import(new StoreImport("settings", "data/driver.json", 1, "x", 1, ImportClass.Left), _ => loaded = true);

        Assert.False(outcome.Done);
        Assert.False(loaded);
        Assert.Equal((DesktopStore.Steps.Count, 1), store.Stamp);
        Assert.Empty(store.Query("SELECT 1 FROM imports", row => 1));
        Assert.Empty(store.Revisions());
    }

    [Fact]
    public void A_start_finding_a_journal_that_does_not_read_imports_nothing_and_says_so()
    {
        File.WriteAllText(StagedBuild.JournalOf(_install), "{ half a journal");
        var store = Open(Newer());

        Assert.Contains("does not read", store.Held);
        var outcome = store.Import(new StoreImport("settings", "data/driver.json", 1, "x", 1, ImportClass.Left));

        Assert.False(outcome.Done);
        Assert.Contains("does not read", outcome.Said);
        Assert.Equal((DesktopStore.Steps.Count, 1), store.Stamp);
        Assert.Empty(store.Query("SELECT 1 FROM imports", row => 1));
    }

    [Fact]
    public void The_silent_swap_s_start_imports_and_raises_the_stamp_at_its_first_look()
    {
        Journal(SwapPhase.Installed, reason: "unconfirmed", confirmed: false);
        var store = Open(Newer());

        Assert.Null(store.Held);
        Assert.Equal((DesktopStore.Steps.Count + 1, 2), store.Stamp);
        Assert.True(store.Import(new StoreImport("settings", "data/driver.json", 1, "x", 1, ImportClass.Left)).Done);
    }

    [Fact]
    public void A_headless_loop_beside_a_swap_waiting_finds_it_settled_at_its_next_look_and_then_imports_and_raises()
    {
        Journal(SwapPhase.Swapping);
        var loop = Open(Newer("host"));
        Assert.False(loop.Import(new StoreImport("settings", "data/driver.json", 1, "x", 1, ImportClass.Left)).Done);
        Assert.False(loop.Migrate());
        Assert.Equal((DesktopStore.Steps.Count, 1), loop.Stamp);

        // The swap goes on and the new application confirms: the launcher's journal moves, and the loop's next look reads it.
        Journal(SwapPhase.Started);
        Assert.False(loop.Migrate());
        Journal(SwapPhase.Confirmed);

        Assert.True(loop.Migrate());
        Assert.Equal((DesktopStore.Steps.Count + 1, 2), loop.Stamp);
        Assert.True(loop.Import(new StoreImport("settings", "data/driver.json", 1, "x", 1, ImportClass.Left)).Done);
        Assert.Equal(1, loop.Revisions()["settings"]);
    }

    [Fact]
    public void A_rollback_after_a_first_start_meets_every_file_it_acts_on_as_it_was_and_a_store_it_can_still_write()
    {
        // The files an older build acts on, as the install holds them, before the newer build's first start.
        var files = new Dictionary<string, string>
        {
            ["data/driver.json"] = """{ "drivable": [ "a" ] }""",
            ["data/sessions/s1.harness.json"] = """{ "id": "c1" }""",
            ["data/standing-confirmation.json"] = """{ "presses": [] }""",
        };
        foreach (var (path, text) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_install, path))!);
            File.WriteAllText(Path.Combine(_install, path), text);
        }

        var before = files.Keys.ToDictionary(path => path, path => Hash(Path.Combine(_install, path)));
        Journal(SwapPhase.Started);

        // The newer build's first start: its store, its imports asked, its stamp-raising migration asked, all held by the journal.
        var newer = Open(Newer());
        foreach (var path in files.Keys)
        {
            Assert.False(newer.Import(new StoreImport("family", path, 1, "x", 1, ImportClass.SetAside)).Done);
        }

        newer.Migrate();
        newer.Dispose();

        // The launcher rolls it back: the journal says so, and the older build starts on the same home.
        Journal(SwapPhase.RolledBack, reason: "exited");
        var older = Open(new DesktopStoreOptions { Kind = "shell", Settled = () => StartConfirmation.Read(_install) });

        Assert.Equal(before, files.Keys.ToDictionary(path => path, path => Hash(Path.Combine(_install, path))));
        Assert.All(files.Keys, path => Assert.False(File.Exists(Path.Combine(_install, path + ".imported"))));
        Assert.Equal((DesktopStore.Steps.Count, 1), older.Stamp);
        Assert.False(older.Newer);
        older.Write("settings", _ => { });   // a store it can still write
        Assert.Equal(1, older.Revisions()["settings"]);
        Assert.Empty(older.Query("SELECT 1 FROM imports", row => 1));
    }

    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
}
