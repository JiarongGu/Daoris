using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A place's rules kept in the desktop store's step 19, as <see cref="StaffMovesTests"/> holds <c>role_moves</c>: a home with
/// no store holds none and takes none, a write is judged against every place's set in its own transaction, and a refused write leaves nothing.
/// </summary>
public sealed class SupervisorRuleStoreTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-rule-store", Guid.NewGuid().ToString("N")[..8]);

    private static readonly SupervisorRuleWorld World = new(["build", "chat", "helper"]);

    public SupervisorRuleStoreTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private SupervisorRuleStore Store() => new(_home, () => StoreSettled.Yes);

    private static IReadOnlyList<SupervisorRule> Rules(string place, string json) => SupervisorRulesTests.Rules(JsonNode.Parse(json), place);

    private const string Seed = """
        [{ "id": "m-aa", "count": "answers-alone", "at": 2, "do": "hold" },
         { "id": "m-t", "count": "tries", "at": 5, "do": "hold" },
         { "id": "m-mv", "count": "moves", "at": 3, "do": "ask" }]
        """;

    [Fact]
    public void A_home_without_a_store_holds_no_rule_and_a_read_makes_no_file()
    {
        var store = Store();
        Assert.Empty(store.All());
        Assert.Equal(new SupervisorRuleSet("machine", 0, []), store.Of("machine"));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    [Fact]
    public void A_set_is_judged_then_kept_whole_with_the_next_revision_and_read_back_by_a_new_reader()
    {
        var first = Store().Write("machine", 0, Rules("machine", Seed), "test", World);
        Assert.Equal(1, first.Revision);
        var read = Store().Of("machine");
        Assert.Equal(1, read.Revision);
        Assert.Equal(["m-aa", "m-t", "m-mv"], read.Rules.Select(rule => rule.Id));
        Assert.Equal(Rules("machine", Seed), read.Rules);
    }

    [Fact]
    public void A_refused_write_leaves_the_set_and_its_revision_where_they_were_and_a_stale_revision_is_refused()
    {
        var store = StoreMade();
        Assert.Equal(1, store.Write("machine", 0, Rules("machine", Seed), "test", World).Revision);

        var unbounded = store.Write("repository:daoris", 0, Rules("repository:daoris", """[{ "id": "x", "count": "quiet", "at": 2, "do": "next" }]"""), "test", World);
        Assert.Equal("at", unbounded.Refusal?.Code);
        Assert.Equal(0, store.Of("repository:daoris").Revision);

        Assert.Equal(1, store.Write("repository:daoris", 0, Rules("repository:daoris", """[{ "id": "x", "count": "quiet", "at": 1, "do": "next" }]"""), "test", World).Revision);
        Assert.Equal("revision", store.Write("repository:daoris", 0, [], "test", World).Refusal?.Code);
        Assert.Equal(1, store.Of("repository:daoris").Rules.Count);

        // A wider write that would leave the narrower place's move unbounded is refused, and nothing of it is kept.
        var wider = store.Write("machine", 1, Rules("machine", """[{ "id": "m-t", "count": "tries", "at": 5, "do": "hold" }]"""), "test", World);
        Assert.Equal("moves-unbounded", wider.Refusal?.Code);
        Assert.Equal("repository:daoris", wider.Refusal?.Place);
        Assert.Equal(3, store.Of("machine").Rules.Count);
        Assert.Equal(1, store.Of("machine").Revision);
    }

    [Fact]
    public void A_place_emptied_keeps_its_revision_so_the_next_write_is_read_against_it()
    {
        var store = StoreMade();
        store.Write("machine", 0, Rules("machine", Seed), "test", World);
        store.Write("workspace:w1", 0, Rules("workspace:w1", """[{ "id": "x", "count": "failures", "at": 3, "do": "hold" }]"""), "test", World);
        var emptied = store.Write("workspace:w1", 1, [], "test", World);
        Assert.Equal(2, emptied.Revision);
        var read = store.Of("workspace:w1");
        Assert.Equal(2, read.Revision);
        Assert.Empty(read.Rules);
    }

    // The store is made by the first thing that opens it for a write, as StaffMoves' is; a first write is the run that makes it.
    private SupervisorRuleStore StoreMade()
    {
        using (DesktopStore.Open(DesktopStore.LocationIn(_home), StoreProcess.Options(() => StoreSettled.Yes))) { }
        return Store();
    }
}
