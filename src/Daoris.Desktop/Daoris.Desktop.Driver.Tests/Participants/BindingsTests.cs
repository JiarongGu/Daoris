using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 z-iii (D212, the design §13.5 and §13.6's row z-iii): each session's <c>&lt;session&gt;.harness.json</c> imported into the desktop
/// store's <c>bindings</c> once the start's confirmation is settled and set aside under the import's receipt, every resume reading its
/// conversation id from the binding once imported and from the file before, a deleted record taking its binding, an older build after the
/// import finding no file and carrying on rather than resuming, and a file an older build wrote during a downgrade imported at the return,
/// its conversation id winning for its own session.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment, <c>DAORIS_HOME</c> least.
/// "An older build" is what the code before the row did, written out beside the case: it reads <c>&lt;session&gt;.harness.json</c> alone and, when
/// the wire names a conversation, writes that file whole, so two builds meet over one home as two processes' builds would. The start's
/// confirmation is a reading each case holds in its hand. Nothing here starts a process, a window or an agent.
/// </remarks>
public sealed class BindingsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bindings", Guid.NewGuid().ToString("N")[..8]);

    private StoreSettled _settled = StoreSettled.Yes;

    public BindingsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    /// <summary>The newer build's conversations over this home, the start's confirmation read as the case holds it now.</summary>
    private HarnessConversations Newer() => new(_home, () => _settled);

    private string Db => DesktopStore.LocationIn(_home);

    private string FileOf(string session) => new HarnessConversations(_home).PathOf(session);

    /// <summary>What an older build did with a session's conversation: read the file alone, null for none or one that does not read.</summary>
    private HarnessConversation? OlderRead(string session) => GoOnFiles.ReadConversation(FileOf(session));

    /// <summary>What an older build did when the wire named a conversation: the file, written whole, replacing what was there.</summary>
    private void OlderKeep(string session, string adapter, string conversation) => GoOnFiles.WriteConversation(FileOf(session), adapter, conversation);

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private IReadOnlyList<(string Session, string Adapter, string Conversation)> Rows()
    {
        using var reader = DesktopStoreReader.Open(Db);
        return reader is null
            ? []
            : reader.Query("SELECT session, adapter, conversation FROM bindings ORDER BY session", row => (row.GetString(0), row.GetString(1), row.GetString(2)));
    }

    private IReadOnlyList<(string Path, string Sha, string Class, int Rows)> Receipts()
    {
        using var reader = DesktopStoreReader.Open(Db);
        return reader is null
            ? []
            : reader.Query(
                "SELECT path, sha256, class, rows FROM imports WHERE family = 'bindings' ORDER BY id",
                row => (row.GetString(0), row.GetString(1), row.GetString(2), row.GetInt32(3)));
    }

    // ——— Before the import, the file; the import waits for the confirmation (§13.5).

    [Fact]
    public void Before_the_import_a_keep_writes_the_file_and_a_read_reads_it_and_no_store_is_made()
    {
        var conversations = Newer();

        conversations.Keep("s1", "claude-code-acp", "c-1");

        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1"), conversations.Read("s1"));
        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1"), OlderRead("s1"));
        Assert.False(File.Exists(Db));
        Assert.False(conversations.InStore());
    }

    [Fact]
    public void While_the_confirmation_is_not_settled_nothing_is_imported_and_every_file_is_as_it_was_for_a_build_rolled_back()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "codex-acp", "c-2");
        var before = new[] { "s1", "s2" }.ToDictionary(session => session, session => File.ReadAllBytes(FileOf(session)));
        _settled = StoreSettled.Not("the install's swap journal is at `started`, which the launcher can still roll back from");

        var held = conversations.ImportFiles();

        Assert.Equal(0, held.Imported);
        Assert.Contains("`started`", held.Held);
        Assert.False(File.Exists(Db));
        foreach (var (session, bytes) in before)
        {
            Assert.Equal(bytes, File.ReadAllBytes(FileOf(session)));
            Assert.False(File.Exists(FileOf(session) + ".imported"));
        }

        // The rolled-back build meets each file it resumes from, and the newer build, until it imports, reads and keeps the files too.
        Assert.Equal(new HarnessConversation("codex-acp", "c-2"), OlderRead("s2"));
        Assert.Equal(new HarnessConversation("codex-acp", "c-2"), conversations.Read("s2"));
        conversations.Keep("s2", "codex-acp", "c-2b");
        Assert.Equal("c-2b", OlderRead("s2")!.Conversation);

        _settled = StoreSettled.Yes;
        Assert.Equal(2, conversations.ImportFiles().Imported);
    }

    // ——— The import: into bindings, the file set aside under the receipt.

    [Fact]
    public void Each_file_is_imported_into_a_binding_and_set_aside_under_a_receipt_never_deleted()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "codex-acp", "c-2");
        var s1 = File.ReadAllBytes(FileOf("s1"));
        var sha1 = Sha(FileOf("s1"));

        var done = conversations.ImportFiles();

        Assert.Equal((2, 0, null), (done.Imported, done.Unread, done.Held));
        Assert.Equal([("s1", "claude-code-acp", "c-1"), ("s2", "codex-acp", "c-2")], Rows());
        foreach (var session in new[] { "s1", "s2" })
        {
            Assert.False(File.Exists(FileOf(session)), session);
            Assert.True(File.Exists(FileOf(session) + ".imported"), session);
        }

        Assert.Equal(s1, File.ReadAllBytes(FileOf("s1") + ".imported"));
        var receipt = Receipts().First();
        Assert.Equal(("data/sessions/s1.harness.json", sha1, ImportClass.SetAside, 1), receipt);
        Assert.Equal(2, Receipts().Count);
        Assert.True(conversations.InStore());
    }

    [Fact]
    public void Importing_again_with_no_file_asks_nothing_of_the_store()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();
        var revision = Revision();

        var again = conversations.ImportFiles();

        Assert.Equal(BindingImport.None, again);
        Assert.Equal(revision, Revision());
    }

    private long Revision()
    {
        using var reader = DesktopStoreReader.Open(Db)!;
        return reader.Revisions().GetValueOrDefault("bindings");
    }

    [Fact]
    public void A_file_that_does_not_read_as_a_conversation_is_left_where_it_is_and_the_rest_import()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf("s9"))!);
        File.WriteAllText(FileOf("s9"), "{ half a file");

        var done = conversations.ImportFiles();

        Assert.Equal((1, 1, null), (done.Imported, done.Unread, done.Held));
        Assert.True(File.Exists(FileOf("s9")));
        Assert.Equal([("s1", "claude-code-acp", "c-1")], Rows());
        Assert.Null(conversations.Read("s9"));
        Assert.Equal((true, false), conversations.Kept("s9"));
    }

    // ——— Every resume reads the binding once imported.

    [Fact]
    public void Once_imported_a_keep_writes_the_binding_and_never_the_file_and_a_read_reads_the_binding()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();

        conversations.Keep("s1", "claude-code-acp", "c-1-after-fallback");
        conversations.Keep("s4", "codex-acp", "c-4");

        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1-after-fallback"), conversations.Read("s1"));
        Assert.Equal(new HarnessConversation("codex-acp", "c-4"), conversations.Read("s4"));
        Assert.Equal(new HarnessConversation("codex-acp", "c-4"), new HarnessConversations(_home).Read("s4"));
        Assert.False(File.Exists(FileOf("s1")));
        Assert.False(File.Exists(FileOf("s4")));
        Assert.Equal([("s1", "claude-code-acp", "c-1-after-fallback"), ("s4", "codex-acp", "c-4")], Rows());
        Assert.Equal((true, true), conversations.Kept("s4"));
        Assert.Equal((false, false), conversations.Kept("s5"));
    }

    [Fact]
    public void A_keep_names_its_own_columns_and_leaves_the_rest_of_the_row_as_it_found_it()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();
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
        Assert.Equal([("codex-acp", "c-1b", "the person")], reader.Query("SELECT adapter, conversation, picked FROM bindings WHERE session = 's1'", row => (row.GetString(0), row.GetString(1), row.GetString(2))));
    }

    [Fact]
    public void A_home_whose_store_holds_no_import_of_the_bindings_keeps_the_file_whatever_else_the_store_holds()
    {
        using (var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes })) store.Write("settings", _ => { });
        var conversations = Newer();

        conversations.Keep("s1", "claude-code-acp", "c-1");

        Assert.True(File.Exists(FileOf("s1")));
        Assert.Empty(Rows());
        Assert.Equal("c-1", OlderRead("s1")!.Conversation);
    }

    // ——— An older build after the import carries on rather than resuming.

    [Fact]
    public void An_older_build_after_the_import_finds_no_file_and_carries_on_rather_than_resuming_a_stale_id()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "codex-acp", "c-2");
        conversations.ImportFiles();
        Assert.Null(OlderRead("s1"));

        // The newer build's wire names the conversation again, as a fallback's new one or a resume's own does: the id the file held is stale now.
        conversations.Keep("s1", "claude-code-acp", "c-1-replaced");

        Assert.Null(OlderRead("s1"));
        Assert.Null(OlderRead("s2"));
        Assert.Equal("c-1-replaced", conversations.Read("s1")!.Conversation);
    }

    // ——— A record deleted takes its binding.

    [Fact]
    public void A_record_deleted_takes_its_binding_and_its_set_aside_file_and_leaves_the_others()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "codex-acp", "c-2");
        conversations.ImportFiles();
        conversations.Keep("s3", "claude-code-acp", "c-3");

        var removed = new SessionHomeFiles(_home).Remove(["s1", "s3"], events: null);

        Assert.Empty(removed.Failed);
        Assert.Contains("harness", removed.Went["s1"]);
        Assert.Contains("harness", removed.Went["s3"]);
        Assert.Equal(1, removed.Went["s1"].Count(name => name == "harness"));
        Assert.Equal([("s2", "codex-acp", "c-2")], Rows());
        Assert.Null(conversations.Read("s1"));
        Assert.Null(conversations.Read("s3"));
        Assert.False(File.Exists(FileOf("s1") + ".imported"));
        Assert.True(File.Exists(FileOf("s2") + ".imported"));
        Assert.Equal("c-2", conversations.Read("s2")!.Conversation);
    }

    [Fact]
    public void A_record_deleted_before_the_import_takes_its_file_and_makes_no_store()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");

        var removed = new SessionHomeFiles(_home).Remove(["s1"], events: null);

        Assert.Contains("harness", removed.Went["s1"]);
        Assert.False(File.Exists(FileOf("s1")));
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void A_store_that_will_not_take_the_delete_leaves_the_binding_and_names_it_as_a_file_the_disk_would_not_let_go()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Keep("s2", "claude-code-acp", "c-2");
        conversations.ImportFiles();
        // A newer Daoris migrated the store and stamped it past this build's: this build reads what it knows and writes nothing.
        using (DesktopStore.Open(Db, new DesktopStoreOptions
        {
            Writer = 2,
            Steps = [.. DesktopStore.Steps, new StoreMigration(DesktopStore.Steps.Count + 1, 2, write => write.Execute("CREATE TABLE raised (x TEXT)"))],
            Settled = () => StoreSettled.Yes,
        }))
        {
        }

        var removed = new SessionHomeFiles(_home).Remove(["s1", "s7"], events: null);

        Assert.Equal([Db], removed.Failed);
        Assert.Equal(["harness"], removed.Stayed["s1"]);
        Assert.Empty(removed.Stayed["s7"]);
        Assert.Equal(["s1", "s2"], Rows().Select(row => row.Session));
    }

    // ——— A file an older build wrote during a downgrade, imported at the return, its id winning.

    [Fact]
    public void A_binding_file_an_older_build_wrote_during_a_downgrade_is_imported_at_the_return_its_id_winning_for_its_own_session()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();
        conversations.Keep("s1", "claude-code-acp", "c-1-newer");

        // The person runs an older build on the home: it finds no file, carries on, and the wire names its own new conversation.
        Assert.Null(OlderRead("s1"));
        OlderKeep("s1", "claude-code-acp", "c-1-older");
        OlderKeep("s6", "codex-acp", "c-6-older");

        // The newer build comes back: its next look imports both, the older build's id winning over the newer build's for s1.
        var back = conversations.ImportFiles();

        Assert.Equal((2, 0, null), (back.Imported, back.Unread, back.Held));
        Assert.Equal([("s1", "claude-code-acp", "c-1-older"), ("s6", "codex-acp", "c-6-older")], Rows());
        Assert.False(File.Exists(FileOf("s1")));
        Assert.False(File.Exists(FileOf("s6")));
        Assert.True(File.Exists(FileOf("s1") + ".imported"));
        Assert.Equal("c-1-older", conversations.Read("s1")!.Conversation);
        // And set aside again: an older build run once more finds none.
        Assert.Null(OlderRead("s1"));
        Assert.Equal(3, Receipts().Count);
    }

    [Fact]
    public void A_resume_that_meets_an_older_build_s_file_before_the_look_does_reads_its_id_and_imports_it()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();
        conversations.Keep("s1", "claude-code-acp", "c-1-newer");
        OlderKeep("s1", "claude-code-acp", "c-1-older");

        var read = conversations.Read("s1");

        Assert.Equal(new HarnessConversation("claude-code-acp", "c-1-older"), read);
        Assert.Equal([("s1", "claude-code-acp", "c-1-older")], Rows());
        Assert.False(File.Exists(FileOf("s1")));
    }

    [Fact]
    public void An_older_build_s_file_the_store_will_not_take_is_still_the_id_a_resume_reads()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();
        OlderKeep("s1", "claude-code-acp", "c-1-older");
        using (DesktopStore.Open(Db, new DesktopStoreOptions
        {
            Writer = 2,
            Steps = [.. DesktopStore.Steps, new StoreMigration(DesktopStore.Steps.Count + 1, 2, write => write.Execute("CREATE TABLE raised (x TEXT)"))],
            Settled = () => StoreSettled.Yes,
        }))
        {
        }

        Assert.Equal("c-1-older", conversations.Read("s1")!.Conversation);

        // Held for the look as well, said, the file left for the build that can write.
        var held = conversations.ImportFiles();
        Assert.Equal(0, held.Imported);
        Assert.Contains("newer Daoris", held.Held);
        Assert.True(File.Exists(FileOf("s1")));
    }

    [Fact]
    public void A_crash_between_the_receipt_and_the_set_aside_sets_the_file_aside_and_never_replaces_a_later_binding_with_its_stale_id()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();
        conversations.Keep("s1", "claude-code-acp", "c-1-newer");
        // The file is back as the import left it before its set-aside: the same bytes the receipt holds.
        File.Copy(FileOf("s1") + ".imported", FileOf("s1"));

        var done = conversations.ImportFiles();

        Assert.Equal(1, done.Imported);
        Assert.Equal([("s1", "claude-code-acp", "c-1-newer")], Rows());
        Assert.False(File.Exists(FileOf("s1")));
        Assert.Single(Receipts());
    }

    [Fact]
    public void A_file_an_older_build_rewrites_while_it_is_imported_is_not_set_aside_and_is_imported_at_the_next_look()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        // The older build's write lands between the import's read of the file and its set-aside.
        var racing = new BindingStore(_home, () => StoreSettled.Yes, path =>
        {
            var read = GoOnFiles.ReadConversation(path);
            GoOnFiles.WriteConversation(path, "claude-code-acp", "c-1-older");
            return read is { } kept ? (kept.Adapter, kept.Conversation) : null;
        });

        Assert.Equal(1, racing.ImportFiles().Imported);

        Assert.Equal([("s1", "claude-code-acp", "c-1")], Rows());
        Assert.True(File.Exists(FileOf("s1")));
        Assert.Equal("c-1-older", OlderRead("s1")!.Conversation);
        Assert.Equal(1, conversations.ImportFiles().Imported);
        Assert.Equal([("s1", "claude-code-acp", "c-1-older")], Rows());
        Assert.False(File.Exists(FileOf("s1")));
    }

    // ——— The store's own side: a table a z-i build never opens.

    [Fact]
    public void The_bindings_table_is_a_new_table_with_every_column_required_that_leaves_the_stamp_where_it_was()
    {
        // A store as the row before this one made it: version 1, stamp 1.
        using (var first = DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [DesktopStore.Steps[0]], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal((1, 1), first.Stamp);
        }

        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        Assert.Equal(1, conversations.ImportFiles().Imported);

        using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        Assert.Equal((DesktopStore.Steps.Count, 1), store.Stamp);
        var columns = store.Query("SELECT name, \"notnull\" FROM pragma_table_info('bindings')", row => (row.GetString(0), row.GetInt32(1)));
        Assert.Equal([("session", 0), ("adapter", 1), ("conversation", 1), ("kept", 1)], columns);

        // The build of the row before reads and writes the store as it did: it never opens the new table, and its stamp is not past.
        using var older = DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [DesktopStore.Steps[0]], Settled = () => StoreSettled.Yes });
        Assert.False(older.Newer);
        older.Write("settings", _ => { });
        Assert.Equal(1, older.Revisions()["settings"]);
        Assert.Equal([("s1", "claude-code-acp", "c-1")], Rows());
    }

    [Fact]
    public void Each_open_of_a_process_records_one_run_and_never_one_per_use()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();
        conversations.Keep("s1", "claude-code-acp", "c-2");
        conversations.Keep("s1", "claude-code-acp", "c-3");
        OlderKeep("s2", "codex-acp", "c-4");
        conversations.ImportFiles();

        using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes });

        Assert.Equal([BindingStore.Run], store.Runs().Select(run => run.Run));
    }
}
