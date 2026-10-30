using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1x step 2 (the foundation design §2.7, §13.3's Bindings family, O3-16): the role a start took and its revision are kept on the
/// session's binding in the desktop store, each writer of the binding naming its own columns, so the conversation's writer and the role's
/// never replace each other's, a build that knows no role leaves it where it is, and a deleted record takes it.
/// </summary>
public sealed class BindingRolesTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "binding-roles", Guid.NewGuid().ToString("N")[..8]);

    public BindingRolesTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private HarnessConversations Newer() => new(_home, () => StoreSettled.Yes);

    private string Db => DesktopStore.LocationIn(_home);

    [Fact]
    public void The_role_a_start_took_and_its_revision_are_kept_beside_the_conversation_each_writer_leaving_the_other_s_columns()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.ImportFiles();

        conversations.KeepRole("s1", "designer", 4);
        conversations.Keep("s1", "claude-code-acp", "c-1-again");

        Assert.Equal(("designer", 4L), conversations.RoleOf("s1"));
        Assert.Equal("c-1-again", conversations.Read("s1")!.Conversation);

        conversations.KeepRole("s1", "build", 0);
        Assert.Equal(("build", 0L), conversations.RoleOf("s1"));
        Assert.Null(conversations.RoleOf("s2"));
    }

    [Fact]
    public void A_build_that_knows_no_role_writes_its_binding_by_name_and_leaves_the_role_where_it_is()
    {
        var conversations = Newer();
        conversations.KeepRole("s1", "designer", 2);

        // The store as the build before this row opened it: its first two steps, and the statement it ran for a binding then.
        using (var older = DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(2)], Settled = () => StoreSettled.Yes }))
        {
            older.Write("bindings", write => write.Execute(
                """
                INSERT INTO bindings (session, adapter, conversation, kept) VALUES ('s1', 'claude-code-acp', 'older', 'then')
                ON CONFLICT (session) DO UPDATE SET adapter = excluded.adapter, conversation = excluded.conversation, kept = excluded.kept
                """));
        }

        Assert.Equal(("designer", 2L), conversations.RoleOf("s1"));
    }

    [Fact]
    public void A_deleted_record_takes_its_role_and_no_store_is_made_to_read_one()
    {
        Assert.Null(Newer().RoleOf("s1"));
        Assert.False(File.Exists(Db));

        var conversations = Newer();
        conversations.KeepRole("s1", "designer", 2);
        Assert.Equal(["s1"], conversations.ForgetBindings(["s1", "s2"]));

        Assert.Null(conversations.RoleOf("s1"));
    }
}
