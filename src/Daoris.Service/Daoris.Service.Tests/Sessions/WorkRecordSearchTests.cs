using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KEEPSIZE1c (D195's keep-size note; the keep-size design §2.2, §7): a finished tree's record found by a word of its sessions' notes and by
/// a commit's subject, scoped by workspace; read whole by its id; its sessions' words gone with the session that said them, the record and
/// the other's words left; a record kept before the index existed indexed at its first open; a no-kinds search not crowded by records.
/// </summary>
public sealed class WorkRecordSearchTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private WorkRecordStore _records = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T10:00:00Z");
    private const string Tree = "/work/trees/games/Owner/s-1";
    private const string Folder = "s-1-20261024T090000000Z";

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _records = await WorkRecordStore.OpenAsync(_connection);
        _service = Compose(_records);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static KnowledgeService Compose(IWorkRecordSearch? records)
    {
        var store = new InMemoryKnowledgeStore();
        return new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry(), records: records);
    }

    private static WorkRecordPost Post(string workspace = "default", string folder = Folder, string subject = "Teach the pelican to land") =>
        new(workspace, "Owner", folder, Tree, Now.AddHours(-1), null, "daoris/s-1", "main", new WorkRecordHold("line", "main", "branch"),
            "tip9", "base0", null, [new WorkRecordCommit("c1", subject, [new("src/a.cs", 3, 1)])], null, new WorkRecordLeft(["scratch.md"], 1, false), Whole: false);

    private async Task<Session> Said(string note, string tree = Tree)
    {
        var session = await _sessions.CreateAsync(null, "Owner", "stub", Now.AddHours(-3), tree: tree, kind: SessionKind.Chat);
        await _sessions.SetStateAsync(session.Id, SessionState.Completed, note, null, null, Now.AddHours(-2));
        return session;
    }

    private async Task<IReadOnlyList<KnowledgeHit>> Find(string text, string? workspace = null, IReadOnlySet<EntryKind>? kinds = null) =>
        await _service.SearchAsync(new KnowledgeQuery(text) { Workspace = workspace, Kinds = kinds });

    [Fact]
    public async Task A_record_is_found_by_a_word_of_a_session_and_by_a_commits_subject_and_read_by_its_id()
    {
        await Said("The heron needed a longer runway");
        await _records.PostAsync(Post(), Now);

        var byWords = Assert.Single(await Find("heron"));
        var bySubject = Assert.Single(await Find("pelican"));
        Assert.Equal(EntryKind.Record, byWords.Entry.Kind);
        Assert.Equal(byWords.Entry.Id, bySubject.Entry.Id);
        Assert.Equal($"record:default/Owner/{Folder}", byWords.Entry.Id);

        var read = await _service.FindAsync(byWords.Entry.Id);
        Assert.NotNull(read);
        Assert.Contains("tip9", read!.Body);
        Assert.Contains("Teach the pelican to land", read.Body);
        Assert.Contains("scratch.md", read.Body);
        Assert.Contains("git worktree add", read.Body);
    }

    [Fact]
    public async Task A_record_is_found_only_within_its_workspace()
    {
        await _records.PostAsync(Post("default"), Now);
        await _records.PostAsync(Post("other", "t-2-20261024T090000000Z", "Another pelican matter"), Now);

        var inDefault = Assert.Single(await Find("pelican", "default"));
        Assert.Equal("default", inDefault.Entry.Workspace);
        Assert.Equal(2, (await Find("pelican")).Count);
    }

    [Fact]
    public async Task Clearing_one_of_two_sessions_removes_its_words_and_keeps_the_others_and_the_record()
    {
        var first = await Said("The heron needed a longer runway");
        await Said("The egret preferred the shore");
        await _records.PostAsync(Post(), Now);

        await _sessions.DeleteAsync(first.Id);

        Assert.Empty(await Find("heron"));
        Assert.Single(await Find("egret"));
        Assert.Single(await Find("pelican"));
    }

    [Fact]
    public async Task Clearing_the_last_session_takes_the_record_and_its_words_whole()
    {
        var only = await Said("The heron needed a longer runway");
        await _records.PostAsync(Post(), Now);

        await _sessions.DeleteAsync(only.Id);

        Assert.Empty(await Find("heron"));
        Assert.Empty(await Find("pelican"));
        Assert.Null(await _service.FindAsync($"record:default/Owner/{Folder}"));
    }

    [Fact]
    public async Task A_record_kept_before_the_index_existed_is_indexed_when_the_store_opens()
    {
        await Said("The heron needed a longer runway");
        await _records.PostAsync(Post(), Now);
        await using (var drop = _connection.CreateCommand())
        {
            drop.CommandText = "DROP TABLE work_records_fts; DROP TABLE work_record_fts_version;";
            await drop.ExecuteNonQueryAsync();
        }

        var reopened = await WorkRecordStore.OpenAsync(_connection);

        Assert.Single(await Compose(reopened).SearchAsync(new KnowledgeQuery("heron")));
    }

    [Fact]
    public async Task A_search_naming_no_kinds_holds_records_to_three_and_a_kind_names_them_all()
    {
        for (var each = 0; each < 5; each++)
        {
            await _records.PostAsync(Post(folder: $"t-{each}-20261024T090000000Z"), Now);
        }

        Assert.Equal(KnowledgeService.RecordsUnasked, (await Find("pelican")).Count);
        Assert.Equal(5, (await Find("pelican", kinds: new HashSet<EntryKind> { EntryKind.Record })).Count);
    }

    [Fact]
    public async Task A_service_composed_without_records_answers_none()
    {
        await _records.PostAsync(Post(), Now);

        Assert.Empty(await Compose(null).SearchAsync(new KnowledgeQuery("pelican")));
        Assert.Null(await Compose(null).FindAsync($"record:default/Owner/{Folder}"));
    }
}
