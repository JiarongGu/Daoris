using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A home holding Ask Daoris's <c>.help.json</c> files and the archive marks of <c>archived.json</c> is migrated once into the
/// conversations' facts: the same names, pins and archive as before, a copy of each file kept, the files gone, a second run changing nothing.
/// </summary>
public sealed class ConversationFactsStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "conversation-facts-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Sessions => DaorisHome.Entry(_home, HomeEntry.Sessions);

    private void Put(string name, string text)
    {
        Directory.CreateDirectory(Sessions);
        File.WriteAllText(Path.Combine(Sessions, name), text);
    }

    /// <summary>The ledger as the service keeps it: each part's id, its conversation and when it began.</summary>
    private void Ledger(params (string Id, string Line, string Created)[] parts)
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var make = connection.CreateCommand();
        make.CommandText = "CREATE TABLE sessions (id TEXT PRIMARY KEY, line TEXT NULL, created TEXT NOT NULL)";
        make.ExecuteNonQuery();
        foreach (var (id, line, created) in parts)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO sessions (id, line, created) VALUES ($id, $line, $created)";
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$line", line);
            insert.Parameters.AddWithValue("$created", created);
            insert.ExecuteNonQuery();
        }
    }

    private static readonly DateTimeOffset Monday = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Both_old_files_are_read_into_the_facts_and_taken_out_once()
    {
        Put("h1.help.json", """{"name":"Workspaces","pinned":"2026-10-05T09:00:00Z"}""");
        Put("h2.help.json", """{"from":"h1","handed":"brief"}""");
        Put("broken.help.json", """{ "name": """);
        Ledger(("a1", "a1", "2026-10-01T00:00:00Z"), ("a2", "a1", "2026-10-02T00:00:00Z"), ("b1", "b1", "2026-10-01T00:00:00Z"), ("b2", "b1", "2026-10-02T00:00:00Z"));
        // a2 is its conversation's newest part and marked: archived at its time. b1 is an older part marked alone: dropped. gone has no record: its own conversation.
        Put("archived.json", """{"archived":[{"session":"a2","at":"2026-10-03T09:00:00Z"},{"session":"a1","at":"2026-10-04T09:00:00Z"},{"session":"b1","at":"2026-10-03T09:00:00Z"},{"session":"gone","at":"2026-10-06T09:00:00Z"}]}""");

        var ran = HomeMigration.Run(_home, [new ConversationFactsStep()]);

        Assert.Equal(["conversation-facts"], ran.Ran);
        var facts = new ConversationFacts(_home);
        Assert.Equal(new HelpKept("Workspaces", Monday, null, null), facts.Read("h1"));
        Assert.Equal(("h1", "brief"), (facts.Read("h2").From, facts.Read("h2").Handed));
        Assert.Equal(["a1", "gone"], facts.Archives().Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero), facts.Archives()["a1"]);
        Assert.Empty(Directory.GetFiles(Sessions, "*.help.json"));
        Assert.False(File.Exists(Path.Combine(Sessions, "archived.json")));
        var kept = Path.Combine(Sessions, "h1.help.json");
        Assert.True(File.Exists(Path.Combine(
            DaorisHome.Entry(_home, HomeEntry.Salvage, HomeMigration.Name), Path.GetRelativePath(DaorisHome.FolderOf(_home).Folder, kept))));
    }

    [Fact]
    public void A_second_run_changes_nothing_and_never_overwrites_what_the_person_kept_since()
    {
        Put("h1.help.json", """{"name":"Workspaces"}""");
        var step = new ConversationFactsStep();
        step.Run(new HomeMigrationRun(_home, HomeMigration.Name));
        new ConversationFacts(_home).Rename("h1", "Renamed since");

        Put("h1.help.json", """{"name":"Workspaces","pinned":"2026-10-05T09:00:00Z"}""");
        step.Run(new HomeMigrationRun(_home, HomeMigration.Name));

        var kept = new ConversationFacts(_home).Read("h1");
        Assert.Equal(("Renamed since", (DateTimeOffset?)Monday), (kept.Name, kept.Pinned));
    }

    [Fact]
    public void A_home_with_neither_file_is_left_alone_and_makes_no_store()
    {
        Directory.CreateDirectory(Sessions);

        new ConversationFactsStep().Run(new HomeMigrationRun(_home, HomeMigration.Name));

        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }
}
