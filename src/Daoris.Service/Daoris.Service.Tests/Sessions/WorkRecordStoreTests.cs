using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KEEPSIZE1b (D195's keep-size note; the keep-size design §2.1, §2.2): a finished tree's record kept in <c>work_records</c> and
/// <c>work_record_sessions</c>, one per tree, naming every session that ran in it before its move, attributed only where their bases bound
/// the range; a second post kept once; a store a newer build stamped refused whole; history: gone with its last session through
/// <see cref="SessionStore.DeleteAsync"/>, which both the single delete and the clear of history call, and a record with no session taken
/// by a clear of history by its own <c>at</c>.
/// </summary>
public sealed class WorkRecordStoreTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private AskStore _asks = null!;
    private WorkRecordStore _records = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T10:00:00Z");
    private const string Tree = "/home/work/trees/games/Owner/s-1";

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);
        _records = await WorkRecordStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.RegisterAsync(new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/owner"), Now);
        await _service.RegisterAsync(new Registration("Asker", Adopted: true, "Asks.", [], [], [], Entries: 0, Root: "/trees/asker"), Now);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static WorkRecordCommit Commit(string id, string subject) => new(id, subject, [new("src/a.cs", 3, 1)]);

    private static WorkRecordPost Post(
        string folder = "s-1-20261024T090000000Z", IReadOnlyList<WorkRecordCommit>? commits = null, string grew = "base0", string tree = Tree) =>
        new("default", "Owner", folder, tree, Now.AddHours(-1), "unit-1", "daoris/s-1", "main",
            new WorkRecordHold("squash", "landing/s-1", "branch"), commits?.LastOrDefault()?.Id ?? "tip9", grew, "refs/daoris/discarded/daoris/s-1",
            commits ?? [Commit("c1", "First"), Commit("c2", "Second")], null, new WorkRecordLeft(["notes.md"], 1, false), Whole: false);

    private async Task<Session> Ran(string? baseCommit, DateTimeOffset at, string? quest = null, string tree = Tree)
    {
        var session = await _sessions.CreateAsync(quest, "Owner", "stub", at, tree: tree, baseCommit: baseCommit, kind: quest is null ? SessionKind.Chat : SessionKind.Driven);
        await _sessions.SetStateAsync(session.Id, SessionState.Completed, null, null, null, at.AddMinutes(5));
        return session;
    }

    [Fact]
    public async Task A_record_is_kept_whole_and_read_back()
    {
        var (record, kept) = await _records.PostAsync(Post(), Now);

        Assert.True(kept);
        var read = await _records.FindAsync("default", "Owner", "s-1-20261024T090000000Z");
        Assert.NotNull(read);
        Assert.Equal("tip9", read!.Post.Tip);
        Assert.Equal(["c1", "c2"], read.Post.Commits!.Select(each => each.Id));
        Assert.Equal("squash", read.Post.Hold.Kind);
        Assert.Equal("refs/daoris/discarded/daoris/s-1", read.Post.Ref);
        Assert.Equal(["notes.md"], read.Post.Left.Names);
        Assert.Equal(Now, read.At);
        Assert.Equal(WorkRecordStore.RecordSchema, read.Schema);
        Assert.Equal(record.At, read.At);
    }

    /// <summary>🔴 Round 3's finding 11: the pass and the press may both post one folder's record; the second answers as the first did.</summary>
    [Fact]
    public async Task A_record_posted_twice_is_kept_once()
    {
        await Ran("base0", Now.AddHours(-3));
        var (first, kept) = await _records.PostAsync(Post(), Now);
        var (second, again) = await _records.PostAsync(Post(commits: [Commit("other", "Another")]), Now.AddHours(2));

        Assert.True(kept);
        Assert.False(again);
        Assert.Equal(first.At, second.At);
        Assert.Equal(["c1", "c2"], second.Post.Commits!.Select(each => each.Id));
        Assert.Single(second.Sessions);
        Assert.Equal(1, await _records.CountAsync());
    }

    /// <summary>🔴 Opus finding 10, Codex finding 6: a chain's two sessions in one tree, one record naming both, attributed by their bases.</summary>
    [Fact]
    public async Task A_chains_two_sessions_in_one_tree_give_one_record_attributed_by_their_bases()
    {
        var first = await Ran("base0", Now.AddHours(-4));
        var second = await Ran("c2", Now.AddHours(-3));
        var later = await Ran("c3", Now.AddMinutes(-1));
        var elsewhere = await Ran("base0", Now.AddHours(-4), tree: "/home/work/trees/games/Owner/s-2");

        var (record, _) = await _records.PostAsync(
            Post(commits: [Commit("c1", "One"), Commit("c2", "Two"), Commit("c3", "Three")]), Now);

        Assert.Equal([first.Id, second.Id], record.Sessions.Select(each => each.Session));
        Assert.Equal(["c1", "c2"], record.Sessions[0].Commits!);
        Assert.Equal(["c3"], record.Sessions[1].Commits!);
        Assert.DoesNotContain(record.Sessions, each => each.Session == later.Id || each.Session == elsewhere.Id);
        var read = await _records.FindAsync("default", "Owner", record.Post.Folder);
        Assert.Equal(["c3"], read!.Sessions[1].Commits!);
    }

    /// <summary>A session based elsewhere is named and not attributed: never an attribution its base cannot read.</summary>
    [Fact]
    public async Task A_session_based_elsewhere_is_named_and_not_attributed()
    {
        var first = await Ran("base0", Now.AddHours(-4));
        var helper = await Ran("somewhere-else", Now.AddHours(-3));

        var (record, _) = await _records.PostAsync(Post(), Now);

        Assert.Equal([first.Id, helper.Id], record.Sessions.Select(each => each.Session));
        Assert.All(record.Sessions, each => Assert.Null(each.Commits));
    }

    /// <summary>🔴 Round 3's finding 11 (KSCHEMA1's way): a store a newer build stamped reads and writes no record, and is never lowered.</summary>
    [Fact]
    public async Task Records_stamped_newer_are_refused_and_never_touched()
    {
        var session = await Ran("base0", Now.AddHours(-3));
        await _records.PostAsync(Post(), Now);
        await using (var stamp = _connection.CreateCommand())
        {
            stamp.CommandText = $"UPDATE work_record_version SET version = {WorkRecordStore.Version + 1}";
            await stamp.ExecuteNonQueryAsync();
        }

        var older = await WorkRecordStore.OpenAsync(_connection);

        Assert.Equal(WorkRecordStore.Version + 1, older.NewerVersion);
        await Assert.ThrowsAsync<NewerWorkRecordsException>(() => older.PostAsync(Post("another"), Now));
        await Assert.ThrowsAsync<NewerWorkRecordsException>(() => older.FindAsync("default", "Owner", "s-1-20261024T090000000Z"));
        Assert.Equal(0, await older.CountAsync());
        // Its session's delete leaves the newer build's rows as they are.
        await _sessions.DeleteAsync(session.Id);
        Assert.Equal(1, await Count("work_records"));
        Assert.Equal(1, await Count("work_record_sessions"));
        await using var read = _connection.CreateCommand();
        read.CommandText = "SELECT version FROM work_record_version";
        Assert.Equal(WorkRecordStore.Version + 1, Convert.ToInt32(await read.ExecuteScalarAsync()));
    }

    /// <summary>🔴 Opus finding 13: the single delete takes the record with its last session, and not before.</summary>
    [Fact]
    public async Task The_single_delete_takes_the_record_with_its_last_session()
    {
        var first = await Ran("base0", Now.AddHours(-4));
        var second = await Ran("c2", Now.AddHours(-3));
        await _records.PostAsync(Post(), Now);
        var ledger = new SessionLedger(_quests, _sessions, _service, _asks);

        Assert.Equal(SessionDeleteRefusal.None, (await ledger.DeleteAsync(first.Id)).Refusal);
        Assert.NotNull(await _records.FindAsync("default", "Owner", "s-1-20261024T090000000Z"));
        Assert.Equal(SessionDeleteRefusal.None, (await ledger.DeleteAsync(second.Id)).Refusal);

        Assert.Null(await _records.FindAsync("default", "Owner", "s-1-20261024T090000000Z"));
        Assert.Equal(0, await Count("work_record_sessions"));
    }

    /// <summary>🔴 Opus finding 13: the clear of history takes the record with the sessions it clears.</summary>
    [Fact]
    public async Task The_history_clear_takes_the_record_with_its_last_session()
    {
        var quest = await Closed();
        await Ran("base0", Now.AddHours(-3), quest.Id);
        await _records.PostAsync(Post(), Now);

        var outcome = Assert.Single(await Desk().ClearAsync([new HistoryUnitRef(HistoryUnitKind.Quest, quest.Id)], Now.AddHours(1)));

        Assert.True(outcome.Cleared);
        Assert.Equal(0, await _records.CountAsync());
    }

    /// <summary>
    /// 🔴 Round 3's finding 11: a record posted after its sessions were cleared names none; it is history by its own <c>at</c>, taken by a
    /// clear of history whose range reaches it, and kept by one whose range ends before it.
    /// </summary>
    [Fact]
    public async Task A_record_posted_after_its_sessions_were_cleared_goes_by_its_at_with_a_clear_of_history()
    {
        var early = await Closed("Early");
        await Ran("base0", Now.AddHours(-6), early.Id);
        await Desk().ClearAsync([new HistoryUnitRef(HistoryUnitKind.Quest, early.Id)], Now.AddHours(1));
        var lone = await _records.PostAsync(Post(), Now.AddHours(1));
        Assert.Empty(lone.Record.Sessions);

        // A clear whose sessions last moved before the record was kept leaves it.
        var before = await Closed("Before");
        await Ran(null, Now.AddHours(-2), before.Id, tree: "/elsewhere");
        await Desk().ClearAsync([new HistoryUnitRef(HistoryUnitKind.Quest, before.Id)], Now.AddHours(1));
        Assert.Equal(1, await _records.CountAsync());

        // One whose range reaches past its `at` takes it.
        var after = await Closed("After");
        await Ran(null, Now.AddHours(2), after.Id, tree: "/elsewhere");
        await Desk().ClearAsync([new HistoryUnitRef(HistoryUnitKind.Quest, after.Id)], Now.AddHours(3));

        Assert.Equal(0, await _records.CountAsync());
    }

    /// <summary>
    /// 🔴 Codex's read of the branch, finding 9: a handle opened before a newer build stamped the store refuses from then on, read inside its
    /// operation, as one opened after does.
    /// </summary>
    [Fact]
    public async Task An_already_open_handle_refuses_once_a_newer_build_stamps_the_store()
    {
        await _records.PostAsync(Post(), Now);
        await using (var stamp = _connection.CreateCommand())
        {
            stamp.CommandText = $"UPDATE work_record_version SET version = {WorkRecordStore.Version + 1}";
            await stamp.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<NewerWorkRecordsException>(() => _records.PostAsync(Post("another"), Now));
        await Assert.ThrowsAsync<NewerWorkRecordsException>(() => _records.FindAsync("default", "Owner", "s-1-20261024T090000000Z"));
        Assert.Equal(0, await _records.CountAsync());
        Assert.Equal(1, await Count("work_records"));
    }

    /// <summary>Codex's read of the design, finding 6: the landing the driver captured before the move is kept, and read back as posted.</summary>
    [Fact]
    public async Task A_records_landing_is_kept_as_posted()
    {
        var landed = Now.AddHours(-5);
        await _records.PostAsync(Post() with { Landing = new WorkRecordLanding("feature/q1-work", "main", landed, null) }, Now);
        await _records.PostAsync(Post("s-2-20261024T090000000Z") with { LandingWhy = "no landing is recorded for its work unit" }, Now);

        var read = await _records.FindAsync("default", "Owner", "s-1-20261024T090000000Z");
        Assert.Equal(new WorkRecordLanding("feature/q1-work", "main", landed, null), read!.Post.Landing);
        var none = await _records.FindAsync("default", "Owner", "s-2-20261024T090000000Z");
        Assert.Equal((null, "no landing is recorded for its work unit"), (none!.Post.Landing, none.Post.LandingWhy));
    }

    /// <summary>
    /// 🔴 Codex's read of the branch, finding 8: a record no session names ages by its own <c>at</c> under history's age, with no session cleared
    /// beside it; one a session still names, or kept after the cutoff, stays.
    /// </summary>
    [Fact]
    public async Task A_record_no_session_names_ages_by_its_own_at()
    {
        await _records.PostAsync(Post("s-old-20261001T090000000Z"), Now.AddDays(-40));
        await _records.PostAsync(Post("s-new-20261020T090000000Z"), Now.AddDays(-2));
        await Ran("base0", Now.AddHours(-3));
        await _records.PostAsync(Post("s-named-20261024T090000000Z"), Now.AddDays(-40));

        Assert.Equal(1, await _records.ForgetOrphansAsync(Now.AddDays(-30), null));

        Assert.Null(await _records.FindAsync("default", "Owner", "s-old-20261001T090000000Z"));
        Assert.NotNull(await _records.FindAsync("default", "Owner", "s-new-20261020T090000000Z"));
        Assert.NotNull(await _records.FindAsync("default", "Owner", "s-named-20261024T090000000Z"));
    }

    private HistoryDesk Desk() => new(_quests, _sessions, _asks, _service);

    private async Task<Quest> Closed(string title = "Finished")
    {
        var exchange = new QuestExchange(_service, _quests, remotes: null, files: null, _sessions, _asks);
        var quest = (await exchange.PublishAsync(new QuestAsk("Asker", "Owner", title, "why"), Now.AddHours(-8))).Quest!;
        await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(-8));
        await _quests.MoveAsync(quest.Id, QuestStatus.Done, "Done.", Now.AddHours(-7));
        return (await _quests.FindAsync(quest.Id))!;
    }

    private async Task<int> Count(string table)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
