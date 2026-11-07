using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 a-ii (D212, the design §3.2 step 4 and §13.3's Bindings family): that the person picked a session's account is kept on its
/// binding in the desktop store, never on the service's record or a wire, and the account rule reads it as its constraint. A binding of a
/// record from before, of an agent's pick and of a teammate's mirror holds no pick; a deleted record takes its pick; and a build from
/// before the row, which knows the store at its second step, keeps reading and writing its bindings as it did and reads a picked
/// record as the pick it knew.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment, <c>DAORIS_HOME</c> least.
/// "An older build" is the store's own code with the steps it had before the row (<see cref="DesktopStoreOptions.Steps"/>) and the statements it
/// ran then, written out beside the case. Nothing here starts a process, a window or an agent.
/// </remarks>
public sealed class BindingPicksTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "binding-picks", Guid.NewGuid().ToString("N")[..8]);

    public BindingPicksTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private HarnessConversations Newer() => new(_home, () => StoreSettled.Yes);

    private string Db => DesktopStore.LocationIn(_home);

    private IReadOnlyList<(string Session, string Account)> Picks()
    {
        using var reader = DesktopStoreReader.Open(Db);
        return reader is null || reader.Version < 3
            ? []
            : reader.Query("SELECT session, account FROM binding_picks ORDER BY session", row => (row.GetString(0), row.GetString(1)));
    }

    /// <summary>The store as the build before this row opened it: its first two steps, writing the stamp it always wrote.</summary>
    private DesktopStore OlderOpen() => DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(2)], Settled = () => StoreSettled.Yes });

    // ——— A pick is kept for the account the person named, beside the binding.

    [Fact]
    public void A_pick_is_kept_for_its_session_and_read_as_the_pick_of_that_account_alone()
    {
        var conversations = Newer();

        conversations.Pick("s1", "account-2");

        Assert.True(conversations.PickedOn("s1", "account-2"));
        Assert.True(conversations.PickedOn("s1", " Account-2 "));
        Assert.False(conversations.PickedOn("s1", "account-1"));
        Assert.False(conversations.PickedOn("s1", null));
        Assert.False(conversations.PickedOn("s2", "account-2"));
        Assert.Equal([("s1", "account-2")], Picks());
    }

    [Fact]
    public void A_later_pick_replaces_the_account_and_a_blank_one_keeps_nothing()
    {
        var conversations = Newer();
        conversations.Pick("s1", "account-1");

        conversations.Pick("s1", "account-3");
        conversations.Pick("s1", " ");
        conversations.Pick("s1", null);

        Assert.Equal([("s1", "account-3")], Picks());
    }

    [Fact]
    public void The_wire_naming_a_conversation_keeps_the_pick_beside_its_binding()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Pick("s1", "account-2");

        // The wire names the conversation again, as a resume's own or a fallback's does: the binding is replaced by name, the pick is not.
        conversations.Keep("s1", "claude-code-acp", "c-1-again");

        Assert.Equal("c-1-again", conversations.Read("s1")!.Conversation);
        Assert.True(conversations.PickedOn("s1", "account-2"));
    }

    // ——— What holds no pick.

    [Fact]
    public void A_binding_of_a_record_from_before_holds_no_pick()
    {
        // A conversation kept with no start naming an account for it, as an imported file's is: the keep writes no pick.
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");

        Assert.NotNull(conversations.Read("s1"));
        Assert.False(conversations.PickedOn("s1", "account-1"));
        Assert.False(conversations.PickedOn("s1", null));
        Assert.Empty(Picks());

        // And a binding the wire keeps afterwards, with no pick recorded for the start, holds none either.
        conversations.Keep("s2", "claude-code-acp", "c-2");
        Assert.False(conversations.PickedOn("s2", "account-1"));
        Assert.Empty(Picks());
    }

    [Fact]
    public void A_mirror_of_a_teammates_record_holds_no_pick_and_no_store_is_made_for_it()
    {
        var conversations = Newer();

        conversations.Pick("laptop:s1", "account-1");

        Assert.False(conversations.PickedOn("laptop:s1", "account-1"));
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void A_home_with_no_store_holds_no_pick_and_reading_makes_none()
    {
        Assert.False(Newer().PickedOn("s1", "account-1"));
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void A_pick_the_store_will_not_take_is_lost_and_said_and_the_conversation_reads_as_an_agents_pick()
    {
        // A newer Daoris migrated the store and stamped it past this build's: this build reads what it knows and writes nothing.
        using (DesktopStore.Open(Db, new DesktopStoreOptions
        {
            Writer = DesktopStore.WriterStamp + 1,
            Steps = [.. DesktopStore.Steps, new StoreMigration(DesktopStore.Steps.Count + 1, DesktopStore.WriterStamp + 1, write => write.Execute("CREATE TABLE raised (x TEXT)"))],
            Settled = () => StoreSettled.Yes,
        }))
        {
        }

        var conversations = Newer();
        conversations.Pick("s1", "account-1");

        Assert.False(conversations.PickedOn("s1", "account-1"));
        Assert.Empty(Picks());
    }

    // ——— A deleted record takes its pick.

    [Fact]
    public void A_deleted_record_takes_its_pick_with_its_binding_and_a_pick_alone_is_named_among_what_went()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Pick("s1", "account-1");
        conversations.Pick("s2", "account-2");
        conversations.Pick("s3", "account-3");

        var removed = new SessionHomeFiles(_home).Remove(["s1", "s2"], events: null);

        Assert.Contains(SessionHomeFiles.Binding, removed.Went["s1"]);
        Assert.Contains(SessionHomeFiles.Binding, removed.Went["s2"]);
        Assert.Equal([("s3", "account-3")], Picks());
        Assert.False(conversations.PickedOn("s1", "account-1"));
        Assert.Null(conversations.Read("s1"));
    }

    // ——— A build from before the row.

    [Fact]
    public void A_build_from_before_deleting_a_record_leaves_its_pick_which_names_no_record_and_no_later_session_reads()
    {
        var conversations = Newer();
        conversations.Keep("s1", "claude-code-acp", "c-1");
        conversations.Pick("s1", "account-2");

        using (var older = OlderOpen())
        {
            older.Write(BindingStore.Family, write => write.Execute("DELETE FROM bindings WHERE session = $session", ("$session", "s1")));
        }

        Assert.Null(conversations.Read("s1"));
        // The older build knows no pick to take with the record: it stays, and the newer build's delete of the id names it among what went.
        Assert.Equal([("s1", "account-2")], Picks());
        Assert.Equal(["s1"], conversations.BindingsOf(["s1"]));
        Assert.Equal(["s1"], conversations.ForgetBindings(["s1"]));
        Assert.Empty(Picks());
    }
}
