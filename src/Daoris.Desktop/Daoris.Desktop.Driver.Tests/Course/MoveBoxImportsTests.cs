using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 2 (D212; the foundation design §3.3, §13.5): the importers of the two boxes an older build writes, the ended limit
/// move's <c>&lt;s&gt;.limit-move.json</c> and the conversation's hand-over <c>&lt;s&gt;.hand-over.json</c>. Each box is read once, its move
/// written as the store's own row with the import's receipt in one transaction, and only then set aside, and only if the file is still the
/// bytes it read; nothing is imported until the start's confirmation is settled, and a box whose record has a standing move waits behind it.
/// Built and called from no production site until rows f-v and f-vi (<see cref="MoveStoreCensusTests"/>).
/// </summary>
/// <remarks>Every case has its own scratch home under <c>_fixtures/</c>; nothing here starts a process.</remarks>
[Collection("agent operation ports")]
public sealed class MoveBoxImportsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-box-imports", Guid.NewGuid().ToString("N")[..8]);

    private StoreSettled _settled = StoreSettled.Yes;

    public MoveBoxImportsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private MoveStore Store(string run = "run-new") => new(_home, () => _settled, _ => true, run, StoreProcess.Shell);

    private LimitBoxImport Limits(MoveStore store, Func<string, int, bool>? limited = null) => new(_home, store, limited ?? ((_, _) => true));

    private HandOverBoxImport HandOvers(MoveStore store) => new(_home, store);

    private string LimitPath(string session) => new LimitBoxes(_home).PathOf(session);

    private string HandOverPath(string session) => new HandOverBox(_home).PathOf(session);

    private long Rows(string table)
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(_home));
        return reader is null ? 0 : reader.Query($"SELECT COUNT(*) FROM {table}", row => row.GetInt64(0))[0];
    }

    // ——— The ended limit move's box.

    [Fact]
    public void A_limit_box_with_no_new_part_is_imported_once_as_an_old_ended_move_claimed_by_the_importing_run_and_set_aside()
    {
        new LimitBoxes(_home).Write("s1", new LimitMoveBox(HandOverWhy.Limit, 4, 9, ["w1", "w2"]));
        var bytes = File.ReadAllBytes(LimitPath("s1"));
        var store = Store();

        var pass = Limits(store).Import();

        var move = Assert.Single(pass.Imported);
        Assert.Equal(("s1", MoveExecutors.Conversation, MoveHows.Brief, (MoveRecordPhase?)MoveRecordPhase.OldEnded), (move.Left, move.Executor, move.How, move.Phase));
        Assert.Equal(("run-new", 1L), (move.ClaimRun, move.ClaimGeneration));
        Assert.Equal((HandOverWhy.Limit, HandOverBy.Daoris, (string?)HandOverWhy.Limit, (int?)4, (int?)9), (move.Why, move.By, move.RecipeWhy, move.RecipeBound, move.RefusedBound));
        Assert.Null(move.Destination);
        Assert.Empty(store.WordsOf(move.Id));
        Assert.Empty(pass.Waiting);
        Assert.Empty(pass.Behind);

        // Set aside under the import's receipt, the very bytes it read, never deleted; today's readers find no box.
        Assert.False(File.Exists(LimitPath("s1")));
        Assert.Equal(bytes, File.ReadAllBytes(LimitPath("s1") + ".imported"));
        Assert.Null(new LimitBoxes(_home).Read("s1"));
        var receipt = store.LastImport("data/sessions/s1.limit-move.json")!;
        Assert.Equal((MoveStore.Family, (long)bytes.Length, ImportClass.SetAside, 1), (receipt.Import.Family, receipt.Import.Size, receipt.Import.Class, receipt.Import.Rows));

        // A second pass has no box to read and writes nothing.
        var again = Limits(store).Import();
        Assert.Empty(again.Imported);
        Assert.Equal(1, Rows("moves"));
    }

    [Fact]
    public void A_limit_box_naming_its_new_part_is_imported_at_opened_with_its_destination_and_the_words_it_said_in_order()
    {
        new LimitBoxes(_home).Write("s1", new LimitMoveBox(HandOverWhy.Limit, 4, 9, ["w2", "w1"], "s2"));
        var store = Store();

        var move = Assert.Single(Limits(store).Import().Imported);

        Assert.Equal((MoveRecordPhase?)MoveRecordPhase.Opened, move.Phase);
        Assert.Equal("s2", move.Destination);
        Assert.Null(move.Opens);
        Assert.Equal(
            [("w2", "limit-move/s1/w2", 0), ("w1", "limit-move/s1/w1", 1)],
            store.WordsOf(move.Id).Select(word => (word.Word, word.Transfer, word.Position)));
        Assert.Equal(2, store.LastImport("data/sessions/s1.limit-move.json")!.Import.Rows - 1);
    }

    [Theory]
    [InlineData(HandOverWhy.Limit, true, HandOverWhy.Limit)]
    [InlineData(ContinueWhy.Account, false, HandOverWhy.Retry)]
    [InlineData(HandOverWhy.Limit, false, HandOverWhy.Resume)]
    [InlineData("conversation-lost", true, HandOverWhy.Limit)]
    [InlineData("conversation-lost", false, HandOverWhy.Resume)]
    public void A_limit_box_s_move_goes_by_the_why_its_message_is_composed_by(string boxWhy, bool limited, string moveWhy)
    {
        new LimitBoxes(_home).Write("s1", new LimitMoveBox(boxWhy, 4, 9, []));
        var asked = new List<(string, int)>();

        var move = Assert.Single(Limits(Store(), (session, through) =>
        {
            asked.Add((session, through));
            return limited;
        }).Import().Imported);

        Assert.Equal((moveWhy, (string?)moveWhy), (move.Why, move.RecipeWhy));
        Assert.Equal([("s1", 9)], asked);
    }

    // ——— The conversation's hand-over box.

    [Fact]
    public void A_hand_over_box_is_imported_at_decided_with_its_agent_its_account_its_why_and_by_and_the_moment_it_was_asked()
    {
        var asked = new DateTimeOffset(2026, 10, 30, 8, 15, 0, TimeSpan.Zero);
        new HandOverBox(_home).Write("s1", new HandOverRequest("codex", "work", HandOverWhy.Near, HandOverBy.Person, asked));
        var store = Store();

        var pass = HandOvers(store).Import();

        var move = Assert.Single(pass.Imported);
        Assert.Equal(("s1", MoveExecutors.Conversation, (MoveRecordPhase?)MoveRecordPhase.Decided), (move.Left, move.Executor, move.Phase));
        Assert.Equal(((string?)"codex", (string?)"work", HandOverWhy.Near, HandOverBy.Person), (move.Agent, move.Account, move.Why, move.By));
        Assert.Equal(asked, DateTimeOffset.Parse(move.Moments[MoveRecordPhase.Decided], System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(File.Exists(HandOverPath("s1")));
        Assert.True(File.Exists(HandOverPath("s1") + ".imported"));
        Assert.Null(new HandOverBox(_home).Read("s1"));
    }

    // ——— When, and how often.

    [Fact]
    public void Nothing_is_imported_until_the_start_s_confirmation_is_settled_and_the_record_waits_meanwhile_then_the_next_pass_imports()
    {
        new HandOverBox(_home).Write("s1", new HandOverRequest("codex", null, HandOverWhy.Press, HandOverBy.Person, DateTimeOffset.UtcNow));
        _settled = StoreSettled.Not("the install's swap journal is at `started`");
        var store = Store();

        var held = HandOvers(store).Import();

        Assert.Empty(held.Imported);
        Assert.Equal(["s1"], held.Waiting);
        Assert.Contains("`started`", held.Held);
        Assert.True(File.Exists(HandOverPath("s1")));
        Assert.Equal(0, Rows("moves"));

        _settled = StoreSettled.Yes;
        var settled = HandOvers(store).Import();

        Assert.Single(settled.Imported);
        Assert.Empty(settled.Waiting);
        Assert.Null(settled.Held);
    }

    [Fact]
    public void A_box_whose_bytes_an_import_already_holds_is_only_set_aside_never_imported_twice()
    {
        new LimitBoxes(_home).Write("s1", new LimitMoveBox(HandOverWhy.Limit, 4, 9, ["w1"]));
        var bytes = File.ReadAllBytes(LimitPath("s1"));
        var store = Store();
        var move = Assert.Single(Limits(store).Import().Imported);
        store.Close(new MoveClaim(move.Id, store.Run, move.ClaimGeneration), MoveCloseWhy.Superseded);

        // A crash between the import and its set-aside leaves the very bytes the receipt names in place: the record is free again, and still
        // the box is not a second move.
        File.WriteAllBytes(LimitPath("s1"), bytes);
        var again = Limits(store).Import();

        Assert.Empty(again.Imported);
        Assert.Equal(1, Rows("moves"));
        Assert.False(File.Exists(LimitPath("s1")));
    }

    [Fact]
    public void A_box_rewritten_after_it_was_read_is_never_set_aside_and_waits_behind_the_move_its_first_bytes_made()
    {
        new LimitBoxes(_home).Write("s1", new LimitMoveBox(HandOverWhy.Limit, 4, 9, ["w1"]));
        var store = Store();
        var import = Limits(store);
        import.BeforeSetAside = path => new LimitBoxes(_home).Write("s1", new LimitMoveBox(HandOverWhy.Limit, 4, 11, ["w1", "w3"], "s2"));

        var pass = import.Import();

        Assert.Single(pass.Imported);
        Assert.Equal(11, new LimitBoxes(_home).Read("s1")!.Through);
        Assert.False(File.Exists(LimitPath("s1") + ".imported"));

        import.BeforeSetAside = null;
        var next = import.Import();

        Assert.Empty(next.Imported);
        Assert.Equal(["s1"], next.Behind);
        Assert.Equal(1, Rows("moves"));
        Assert.True(File.Exists(LimitPath("s1")));
    }

    [Fact]
    public void A_box_whose_record_has_a_standing_move_is_left_in_place_and_waits_behind_it()
    {
        var store = Store();
        store.Decide(new MoveDecision("s1", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Press, HandOverBy.Person));
        new HandOverBox(_home).Write("s1", new HandOverRequest("codex", null, HandOverWhy.Press, HandOverBy.Person, DateTimeOffset.UtcNow));

        var pass = HandOvers(store).Import();

        Assert.Empty(pass.Imported);
        Assert.Equal(["s1"], pass.Behind);
        Assert.Empty(pass.Waiting);
        Assert.True(File.Exists(HandOverPath("s1")));
        Assert.Equal(1, Rows("moves"));
        Assert.Equal(0, Rows("imports"));
    }

    [Fact]
    public void A_box_that_does_not_read_is_left_where_it_is_as_evidence_and_holds_no_record()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LimitPath("s1"))!);
        File.WriteAllText(LimitPath("s1"), "{ \"why\": ");
        File.WriteAllText(HandOverPath("s2"), "{ \"agent\": \"codex\", \"why\": \"no-such-why\", \"askedBy\": \"person\", \"at\": \"2026-10-30T08:00:00Z\" }");
        var store = Store();

        var limits = Limits(store).Import();
        var handOvers = HandOvers(store).Import();

        Assert.Equal((0, 1, 0, 0), (limits.Imported.Count, limits.Unread, limits.Waiting.Count, limits.Behind.Count));
        Assert.Equal((0, 1, 0, 0), (handOvers.Imported.Count, handOvers.Unread, handOvers.Waiting.Count, handOvers.Behind.Count));
        Assert.True(File.Exists(LimitPath("s1")));
        Assert.True(File.Exists(HandOverPath("s2")));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
    }

    [Fact]
    public void A_home_with_no_box_makes_no_store()
    {
        var pass = Limits(Store()).Import();

        Assert.Equal((0, 0, 0, 0, (string?)null), (pass.Imported.Count, pass.Unread, pass.Waiting.Count, pass.Behind.Count, pass.Held));
        Assert.False(File.Exists(DesktopStore.LocationIn(_home)));
        Assert.Empty(HandOvers(Store()).Import().Imported);
    }

    [Fact]
    public void Each_importer_names_its_box_and_lists_only_the_boxes_in_place()
    {
        new LimitBoxes(_home).Write("s1", new LimitMoveBox(HandOverWhy.Limit, 4, 9, []));
        new HandOverBox(_home).Write("s2", new HandOverRequest("codex", null, HandOverWhy.Press, HandOverBy.Person, DateTimeOffset.UtcNow));
        var store = Store();
        IMoveBoxImport limits = Limits(store);
        IMoveBoxImport handOvers = HandOvers(store);

        Assert.Equal(("limit-move", "hand-over"), (limits.Box, handOvers.Box));
        Assert.Equal(["s1"], limits.Pending());
        Assert.Equal(["s2"], handOvers.Pending());

        limits.Import();

        Assert.Empty(limits.Pending());
        Assert.Equal(["s2"], handOvers.Pending());
    }
}
