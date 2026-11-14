using System.Text.Json;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The home's migration builds the head of every long events record once, so the first window after an update reads rows instead of
/// parsing records: bounded by a time budget, each head saved as it is built, and a record the budget left is built by the next run or
/// its first reader.
/// </summary>
public sealed class SessionEventHeadsStepTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-headstep-" + Guid.NewGuid().ToString("N")[..8]);

    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    public SessionEventHeadsStepTests() => Directory.CreateDirectory(Sessions);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        SessionEventHeads.LetGoOfMemory();
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private void Record(string id, int lines)
    {
        var events = Enumerable.Range(1, lines).Select(seq => JsonSerializer.Serialize(
            new SessionEvent { Seq = seq, At = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(seq), Kind = SessionEventKind.Message, Text = new string('m', 300) },
            SessionEventText.Json));
        File.WriteAllLines(Path.Combine(Sessions, id + ".events.jsonl"), events);
    }

    private IReadOnlyList<string> Headed()
    {
        using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(_home));
        return store is null ? [] : store.Query("SELECT session FROM session_event_heads ORDER BY session", row => row.GetString(0));
    }

    private void Run(TimeSpan budget) => new SessionEventHeadsStep(budget).Run(new HomeMigrationRun(_home, HomeMigration.Name));

    [Fact]
    public void Every_long_record_has_its_head_after_the_step_and_a_short_one_none()
    {
        Record("long1", 3000);
        Record("long2", 3000);
        Record("short1", 50);

        Run(TimeSpan.FromMinutes(1));

        Assert.Equal(["long1", "long2"], Headed());
    }

    [Fact]
    public void A_step_out_of_budget_builds_none_and_the_next_run_builds_what_is_left()
    {
        Record("long1", 3000);
        Record("long2", 3000);

        Run(TimeSpan.Zero);
        Assert.Empty(Headed());

        Run(TimeSpan.FromMinutes(1));
        Assert.Equal(["long1", "long2"], Headed());

        // Run again over what stands: nothing changes.
        Run(TimeSpan.FromMinutes(1));
        Assert.Equal(["long1", "long2"], Headed());
    }

    [Fact]
    public void A_home_with_no_sessions_is_left_as_it_is()
    {
        Directory.Delete(Sessions);

        Run(TimeSpan.FromMinutes(1));

        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }
}
