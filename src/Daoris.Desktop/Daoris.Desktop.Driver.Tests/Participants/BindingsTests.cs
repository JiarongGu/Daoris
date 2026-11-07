using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each session's conversation id is the desktop store's <c>bindings</c> row alone,
/// kept when the wire names it and read back by every resume, a deleted record taking its binding. The files an older build kept are the
/// home migration's (<see cref="BindingFilesStep"/>), not read or written here.
/// </summary>
/// <remarks>Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment, <c>DAORIS_HOME</c> least.</remarks>
public sealed class BindingsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bindings", Guid.NewGuid().ToString("N")[..8]);

    public BindingsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private HarnessConversations Conversations() => new(_home, () => StoreSettled.Yes);

    private string Db => DesktopStore.LocationIn(_home);

    private IReadOnlyList<(string Session, string Door, string Conversation)> Rows()
    {
        using var reader = DesktopStoreReader.Open(Db);
        return reader is null
            ? []
            : reader.Query("SELECT session, door, conversation FROM bindings ORDER BY session", row => (row.GetString(0), row.GetString(1), row.GetString(2)));
    }

    [Fact]
    public void A_keep_writes_the_binding_and_never_a_file_and_a_read_reads_the_binding()
    {
        var conversations = Conversations();

        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s1", "claude-code-acp", "c-1b");

        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1b"), conversations.Read("s1"));
        Assert.Equal([("s1", "claude-code-acp", "c-1b")], Rows());
        Assert.Empty(Directory.EnumerateFiles(_home, "*.harness.json*", SearchOption.AllDirectories));
        Assert.Equal((true, true), conversations.Kept("s1"));
        Assert.Equal((false, false), conversations.Kept("s2"));
        Assert.Null(conversations.Read("s2"));
    }

    [Fact]
    public void A_home_with_no_store_has_no_conversation_and_a_read_makes_none()
    {
        Assert.Null(Conversations().Read("s1"));
        Assert.Equal((false, false), Conversations().Kept("s1"));
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void A_keep_names_its_own_columns_and_leaves_the_rest_of_the_row_as_it_found_it()
    {
        var conversations = Conversations();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        // A later row's column (a-ii's pick), added as a nullable column and set by its own writer.
        using (var raw = new SqliteConnection($"Data Source={Db};Pooling=False"))
        {
            raw.Open();
            using var change = raw.CreateCommand();
            change.CommandText = "ALTER TABLE bindings ADD COLUMN picked TEXT; UPDATE bindings SET picked = 'the person' WHERE session = 's1'";
            change.ExecuteNonQuery();
        }

        conversations.Keep("s1", "codex-acp", "c-1b");

        using var reader = DesktopStoreReader.Open(Db)!;
        Assert.Equal([("codex-acp", "c-1b", "the person")], reader.Query("SELECT door, conversation, picked FROM bindings WHERE session = 's1'", row => (row.GetString(0), row.GetString(1), row.GetString(2))));
    }

    [Fact]
    public void A_record_deleted_takes_its_binding_and_leaves_the_others()
    {
        var conversations = Conversations();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "codex-acp", "c-2");
        conversations.Keep("s3", "claude-code-acp", "c-3");

        var removed = new SessionHomeFiles(_home).Remove(["s1", "s3"], events: null);

        Assert.Empty(removed.Failed);
        Assert.Equal(1, removed.Went["s1"].Count(name => name == SessionHomeFiles.Binding));
        Assert.Contains(SessionHomeFiles.Binding, removed.Went["s3"]);
        Assert.Equal([("s2", "codex-acp", "c-2")], Rows());
        Assert.Null(conversations.Read("s1"));
        Assert.Equal("c-2", conversations.Read("s2")!.Conversation);
    }

    [Fact]
    public void A_store_that_will_not_take_the_delete_leaves_the_binding_and_names_it_as_a_file_the_disk_would_not_let_go()
    {
        var conversations = Conversations();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "claude-code-acp", "c-2");
        // A newer Daoris migrated the store and stamped it past this build's: this build reads what it knows and writes nothing.
        using (DesktopStore.Open(Db, new DesktopStoreOptions
        {
            Writer = DesktopStore.WriterStamp + 1,
            Steps = [.. DesktopStore.Steps, new StoreMigration(DesktopStore.Steps.Count + 1, DesktopStore.WriterStamp + 1, write => write.Execute("CREATE TABLE raised (x TEXT)"))],
            Settled = () => StoreSettled.Yes,
        }))
        {
        }

        var removed = new SessionHomeFiles(_home).Remove(["s1", "s7"], events: null);

        Assert.Equal([Db], removed.Failed);
        Assert.Equal([SessionHomeFiles.Binding], removed.Stayed["s1"]);
        Assert.Empty(removed.Stayed["s7"]);
        Assert.Equal(["s1", "s2"], Rows().Select(row => row.Session));
    }

    [Fact]
    public void Each_open_of_a_process_records_one_run_and_never_one_per_use()
    {
        var conversations = Conversations();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s1", "claude-code-acp", "c-2");
        conversations.Keep("s1", "claude-code-acp", "c-3");

        using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes });

        // The run and its kind are the process's own, the one every store of the process writes under, the move store's too.
        Assert.Equal([(StoreProcess.Run, StoreProcess.Kind)], store.Runs().Select(run => (run.Run, run.Kind)));
    }
}
