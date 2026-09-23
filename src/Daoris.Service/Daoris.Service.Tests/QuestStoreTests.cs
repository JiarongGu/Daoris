using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

public sealed class QuestStoreTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-05T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task<Quest> Publish(string title = "Adopt the canon") =>
        _quests.PublishAsync("Asker", "Owner", title, "Four rules collide.", Now);

    /// <summary>
    /// A quest lives HERE, not in the receiving repository's files. That is the correction the first
    /// version needed: writing into a sibling's tree is an outside edit however small, and it is what
    /// `repository-owns-its-work` exists to prevent.
    /// </summary>
    [Fact]
    public async Task A_published_quest_is_open_and_addressed()
    {
        var quest = await Publish();

        Assert.Equal(QuestStatus.Open, quest.Status);
        Assert.Equal("Asker", quest.From);
        Assert.Equal("Owner", quest.To);
        Assert.Contains("Four rules collide", quest.Body);
    }

    /// <summary>An agent that retries should not produce a second copy of the same ask.</summary>
    [Fact]
    public async Task Publishing_the_same_quest_twice_returns_the_first()
    {
        var first = await Publish();
        var second = await Publish();

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await _quests.ListAsync());
    }

    [Fact]
    public async Task Taking_moves_the_status_without_closing_it()
    {
        var quest = await Publish();

        var taken = await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddDays(1));

        Assert.True(taken.Moved);
        Assert.Equal(QuestStatus.Taken, taken.Quest!.Status);
        Assert.Single(await _quests.ListAsync());
    }

    /// <summary>
    /// "The quest state machine is the only lock" (D46) — which means the SECOND take must lose in
    /// the store itself, not in a check the caller ran a moment earlier. Two machines' drivers watching
    /// one quest is exactly this call arriving twice (D47).
    /// </summary>
    [Fact]
    public async Task A_second_take_loses_and_changes_nothing()
    {
        var quest = await Publish();

        var first = await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
        var second = await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(2));

        Assert.True(first.Moved);
        Assert.False(second.Moved);
        Assert.Equal(QuestStatus.Taken, second.Quest!.Status);
        Assert.Equal(Now.AddHours(1), second.Quest.Updated);
    }

    /// <summary>One title is one quest forever (D46 §3) — reopening a closed quest would break that.</summary>
    [Fact]
    public async Task A_closed_quest_does_not_move()
    {
        var quest = await Publish();
        await _quests.MoveAsync(quest.Id, QuestStatus.Done, "landed", Now);

        var retaken = await _quests.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddDays(1));
        var redeclined = await _quests.MoveAsync(quest.Id, QuestStatus.Declined, "no", Now.AddDays(1));

        Assert.False(retaken.Moved);
        Assert.False(redeclined.Moved);
        Assert.Equal(QuestStatus.Done, retaken.Quest!.Status);
        Assert.Equal("landed", retaken.Quest.Note);
    }

    /// <summary>
    /// The remote's arbitration in miniature: two hosts are two connections over one file, and they
    /// must agree on one taker because the file's own write serialization plus the guarded UPDATE
    /// decide — not because either host checked first (D47 §5).
    /// </summary>
    [Fact]
    public async Task Two_connections_over_one_file_agree_on_one_taker()
    {
        var path = Path.Combine(Path.GetTempPath(), "daoris-race-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        try
        {
            await using var one = new SqliteConnection($"Data Source={path};Pooling=False");
            await using var two = new SqliteConnection($"Data Source={path};Pooling=False");
            await one.OpenAsync();
            await two.OpenAsync();
            var storeOne = await QuestStore.OpenAsync(one);
            var storeTwo = await QuestStore.OpenAsync(two);
            var quest = await storeOne.PublishAsync("Asker", "Owner", "Race me", "why", Now);

            var mine = await storeOne.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));
            var theirs = await storeTwo.MoveAsync(quest.Id, QuestStatus.Taken, null, Now.AddHours(1));

            Assert.True(mine.Moved);
            Assert.False(theirs.Moved);
            Assert.Equal(QuestStatus.Taken, theirs.Quest!.Status);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>The reason is the part the asker can act on; a bare refusal tells them nothing.</summary>
    [Fact]
    public async Task Declining_records_its_reason_and_closes_the_quest()
    {
        var quest = await Publish();

        var declined = await _quests.MoveAsync(
            quest.Id, QuestStatus.Declined, "That rule is deliberately local here.", Now.AddDays(1));

        Assert.Equal(QuestStatus.Declined, declined.Quest!.Status);
        Assert.Contains("deliberately local", declined.Quest.Note);
        Assert.Empty(await _quests.ListAsync());
        Assert.Single(await _quests.ListAsync(includeClosed: true));
    }

    [Fact]
    public async Task Quests_can_be_read_by_who_owes_them()
    {
        await Publish();
        await _quests.PublishAsync("Asker", "Someone", "Different ask", "why", Now);

        Assert.Single(await _quests.ListAsync("Owner"));
        Assert.Empty(await _quests.ListAsync("Nobody"));
    }

    /// <summary>Outstanding work first: a finished queue pushing live work off the end stops being read.</summary>
    [Fact]
    public async Task Open_and_taken_sort_before_closed()
    {
        var done = await _quests.PublishAsync("Asker", "Owner", "Finished", "b", Now);
        await _quests.MoveAsync(done.Id, QuestStatus.Done, null, Now);
        await Publish("Still open");

        var listed = await _quests.ListAsync("Owner", includeClosed: true);

        Assert.Equal("Still open", listed[0].Title);
    }

    /// <summary>
    /// A mirror row copies its home's state (D47 §5): one home per quest means local verbs cannot move
    /// it — only the next mirror can, because only the home decided anything. This is what keeps two
    /// stores from ever holding two opinions about one quest.
    /// </summary>
    [Fact]
    public async Task A_mirrored_quest_is_immovable_locally_and_updated_by_the_next_mirror()
    {
        var remote = new Quest(
            "abc123", "Asker", "Owner", "Do it", "why", QuestStatus.Open, null, Now, Now, Home: "remote");
        await _quests.MirrorAsync(remote);

        var moved = await _quests.MoveAsync("abc123", QuestStatus.Taken, null, Now.AddHours(1));

        Assert.False(moved.Moved);
        Assert.Equal(QuestStatus.Open, moved.Quest!.Status);
        Assert.Equal("remote", moved.Quest.Home);

        await _quests.MirrorAsync(remote with { Status = QuestStatus.Taken, Updated = Now.AddHours(2) });

        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync("abc123"))!.Status);
        Assert.Single(await _quests.ListAsync());
    }

    /// <summary>A store created before the remote existed has no home column and must survive the upgrade.</summary>
    [Fact]
    public async Task An_existing_store_without_the_home_column_is_migrated_in_place()
    {
        await using var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE quests (
                  id TEXT PRIMARY KEY, sender TEXT NOT NULL, receiver TEXT NOT NULL, title TEXT NOT NULL,
                  body TEXT NOT NULL, status TEXT NOT NULL, note TEXT NULL, filed TEXT NOT NULL, updated TEXT NOT NULL
                );
                INSERT INTO quests VALUES ('e1de11', 'A', 'B', 'Old ask', 'why', 'Open', NULL, '2026-01-01', '2026-01-01');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = await QuestStore.OpenAsync(old);
        var elder = (await store.ListAsync()).Single();

        Assert.Null(elder.Home);
        Assert.True((await store.MoveAsync("e1de11", QuestStatus.Taken, null, Now)).Moved);
    }

    /// <summary>
    /// A quest carries addresses and files beside its words (D65 §2) — in the order they were given,
    /// because "the ticket, then the screenshot of it" is an order a person chose.
    /// </summary>
    [Fact]
    public async Task A_quest_keeps_its_links_and_attachments_in_order()
    {
        var published = await _quests.PublishAsync(
            "Asker", "Owner", "Use the media config", "The field names are hard-coded.", Now,
            links: ["https://tickets.example/T-1", "https://docs.example/media"],
            attachments: [new("before.png", "ab12cd34ef56" + new string('0', 52), 2048), new("notes.txt", new string('1', 64), 12)]);

        var found = (await _quests.FindAsync(published.Id))!;

        Assert.Equal(["https://tickets.example/T-1", "https://docs.example/media"], found.Links);
        Assert.Equal(["before.png", "notes.txt"], found.Attachments.Select(a => a.Name));
        Assert.Equal(2048, found.Attachments[0].Bytes);
        Assert.Equal(new string('1', 64), found.Attachments[1].Sha256);
    }

    /// <summary>A quest that carries nothing carries empty lists — never null, so no reader has to ask.</summary>
    [Fact]
    public async Task A_quest_that_carries_nothing_carries_empty_lists()
    {
        var found = (await _quests.FindAsync((await Publish()).Id))!;

        Assert.Empty(found.Links);
        Assert.Empty(found.Attachments);
    }

    /// <summary>
    /// A mirror keeps what its home's quest carries — the NAMES of its attachments, never their bytes
    /// (D65 §2): the remote's record is where another machine learns a file exists at all.
    /// </summary>
    [Fact]
    public async Task A_mirror_carries_the_links_and_attachment_names()
    {
        var remote = new Quest(
            "abc124", "Asker", "Owner", "Do it", "why", QuestStatus.Open, null, Now, Now, Home: "remote")
        {
            Links = ["https://tickets.example/T-2"],
            Attachments = [new("trace.log", new string('2', 64), 300)],
        };
        await _quests.MirrorAsync(remote);

        var found = (await _quests.FindAsync("abc124"))!;

        Assert.Equal(["https://tickets.example/T-2"], found.Links);
        Assert.Equal("trace.log", Assert.Single(found.Attachments).Name);
    }

    /// <summary>A store from before quests carried anything must survive the upgrade, carrying nothing.</summary>
    [Fact]
    public async Task An_existing_store_without_the_carry_columns_is_migrated_in_place()
    {
        await using var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE quests (
                  id TEXT PRIMARY KEY, sender TEXT NOT NULL, receiver TEXT NOT NULL, title TEXT NOT NULL,
                  body TEXT NOT NULL, status TEXT NOT NULL, note TEXT NULL, filed TEXT NOT NULL, updated TEXT NOT NULL,
                  home TEXT NULL, workspace TEXT NOT NULL DEFAULT 'default'
                );
                INSERT INTO quests VALUES ('e1de12', 'A', 'B', 'Old ask', 'why', 'Open', NULL, '2026-01-01', '2026-01-01', NULL, 'default');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var elder = (await (await QuestStore.OpenAsync(old)).ListAsync()).Single();

        Assert.Empty(elder.Links);
        Assert.Empty(elder.Attachments);
    }

    [Fact]
    public async Task An_unknown_id_yields_nothing_rather_than_throwing()
    {
        var move = await _quests.MoveAsync("zzzzzz", QuestStatus.Taken, null, Now);

        Assert.Null(move.Quest);
        Assert.False(move.Moved);
        Assert.Null(await _quests.FindAsync("zzzzzz"));
    }
}
