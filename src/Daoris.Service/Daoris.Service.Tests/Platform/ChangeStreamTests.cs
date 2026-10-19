using System.Diagnostics;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The service's change stream (FABRIC-EVENT3b, the fabric events design §A4.2): one sampler on its own observer connection, each
/// ledger family answered by its own revision, and the cursor's every case. The host's store and a connector write the file from
/// connections of their own, as these tests' writer does.
/// </summary>
public sealed class ChangeStreamTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-17T09:00:00Z");

    /// <summary>Long enough that a wait that answers before it was woken, never at its end.</summary>
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(10);

    private readonly string _file = Path.Combine(
        Path.GetTempPath(), "daoris-changes-" + Guid.NewGuid().ToString("N")[..8] + ".db");

    // The writer: the host's store, or a connector's, on a connection apart from the observer's.
    private SqliteConnection _writer = null!;
    private SessionStore _sessions = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private ChannelStore _channel = null!;
    private ChangeStream _stream = null!;

    public async Task InitializeAsync()
    {
        // Unpooled, so it closes its file when it goes (FLAKE3).
        _writer = new SqliteConnection($"Data Source={_file};Pooling=False");
        await _writer.OpenAsync();
        _quests = await QuestStore.OpenAsync(_writer);
        _sessions = await SessionStore.OpenAsync(_writer);
        _asks = await AskStore.OpenAsync(_writer);
        _channel = await ChannelStore.OpenAsync(_writer);
        _stream = await Open();
    }

    public async Task DisposeAsync()
    {
        await _stream.DisposeAsync();
        await _writer.DisposeAsync();
        if (File.Exists(_file)) File.Delete(_file);
    }

    private Task<ChangeStream> Open(TimeSpan? identityPace = null) =>
        ChangeStream.OpenAsync(_file, pace: TimeSpan.FromMilliseconds(20), identityPace: identityPace, reopenAfter: TimeSpan.FromMilliseconds(20));

    private async Task<string> Bootstrap() => (await _stream.WaitAsync(null, TimeSpan.Zero)).Since;

    [Fact]
    public async Task A_bootstrap_answers_every_family_at_once_with_the_current_cursor()
    {
        var clock = Stopwatch.StartNew();
        var answer = await _stream.WaitAsync(null, Long);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"{clock.Elapsed}");
        Assert.Equal(ChangeFamilies.All, answer.Moved);
        Assert.Equal(_stream.Cursor, answer.Since);
        Assert.Matches(@"^[0-9a-f]+\.0$", answer.Since);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nothing")]
    [InlineData("abc.def")]
    [InlineData("abc.1.2")]
    [InlineData("abc.-1")]
    [InlineData(".3")]
    public async Task A_malformed_cursor_answers_every_family_at_once(string since)
    {
        var answer = await _stream.WaitAsync(since, Long);

        Assert.Equal(ChangeFamilies.All, answer.Moved);
        Assert.Equal(_stream.Cursor, answer.Since);
    }

    [Fact]
    public async Task A_stale_epoch_answers_every_family_at_once()
    {
        var current = await Bootstrap();
        var answer = await _stream.WaitAsync("0123abcd." + current.Split('.')[1], Long);

        Assert.Equal(ChangeFamilies.All, answer.Moved);
        Assert.Equal(current, answer.Since);
    }

    [Fact]
    public async Task A_cursor_ahead_of_the_host_answers_every_family_at_once()
    {
        var current = await Bootstrap();
        var answer = await _stream.WaitAsync(current.Split('.')[0] + ".7", Long);

        Assert.Equal(ChangeFamilies.All, answer.Moved);
        Assert.Equal(current, answer.Since);
    }

    [Fact]
    public async Task A_current_cursor_with_nothing_moved_answers_nothing_at_its_wait()
    {
        var current = await Bootstrap();
        var clock = Stopwatch.StartNew();
        var answer = await _stream.WaitAsync(current, TimeSpan.FromMilliseconds(400));

        Assert.Empty(answer.Moved);
        Assert.Equal(current, answer.Since);
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(350), $"{clock.Elapsed}");
    }

    /// <summary>A write from another connection wakes the wait, which names its family alone, and the cursor moves on.</summary>
    [Fact]
    public async Task A_second_connection_s_write_wakes_the_wait()
    {
        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);

        var clock = Stopwatch.StartNew();
        await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        var answer = await waiting.WaitAsync(Long);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"{clock.Elapsed}");
        Assert.Equal([ChangeFamilies.Sessions], answer.Moved);
        Assert.NotEqual(since, answer.Since);

        // Asked again from where it answered, nothing has moved since.
        Assert.Empty((await _stream.WaitAsync(answer.Since, TimeSpan.FromMilliseconds(100))).Moved);
    }

    /// <summary>A quest published (an agent's, through the connector's own process) moves the quests family alone.</summary>
    [Fact]
    public async Task A_quest_published_moves_the_quests_family()
    {
        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);

        await _quests.PublishAsync("Asker", "Owner", "Do the work", "why", Now);
        var answer = await waiting.WaitAsync(Long);

        Assert.Equal([ChangeFamilies.Quests], answer.Moved);
    }

    /// <summary>
    /// PUSH1d1's choice for a rebase's rewrite in place, which the newest position does not see: a pending take that lost is
    /// rewritten as a conflict inside the integration that kept the winner's take, in the same commit, so the family moves by the
    /// operation kept and the rewrite needs no revision of its own.
    /// </summary>
    [Fact]
    public async Task A_rebase_s_rewrite_moves_the_quests_family_by_the_operation_it_kept()
    {
        var asked = new Quest("feedc0ffee01", "Asker", "Owner", "Do the thing", "Here is why.", QuestStatus.Open, null, Now, Now, Workspaces.Default);
        await _quests.IntegrateAsync(Workspaces.Default,
            [new QuestOperation(asked.Id, QuestOperationKind.Published, "m2", 1, Now, Published: asked, Number: 1)], 1);
        Assert.True((await _quests.MoveAsync(asked.Id, QuestStatus.Taken, null, Now.AddMinutes(1))).Moved);
        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);

        var integrated = await _quests.IntegrateAsync(Workspaces.Default,
            [new QuestOperation(asked.Id, QuestOperationKind.Taken, "m3", 1, Now.AddMinutes(2), Number: 2)], 2);

        Assert.Equal(QuestOperationKind.Conflict, Assert.Single(integrated.Conflicts).Kind);
        Assert.Equal([ChangeFamilies.Quests], (await waiting.WaitAsync(Long)).Moved);
    }

    private static readonly Ask Asked =new("a5c0de", Workspaces.Default, "Make the bridge faster", AskState.Open, "declarations", Now, Now);

    /// <summary>
    /// PUSH1d1 (D182 point 5; the design §A4.2): the asks family moves on every write an ask takes, each path its own (an agent's
    /// connector makes most of them from its own process): asked, saved whole again, published, closed, served by an intake, a word,
    /// each list appended to, the go-aheads decided, and deleted. Every one names the asks family alone.
    /// </summary>
    [Theory]
    [InlineData("asked")]
    [InlineData("saved again")]
    [InlineData("published")]
    [InlineData("closed")]
    [InlineData("intake")]
    [InlineData("word")]
    [InlineData("review choice")]
    [InlineData("review proposal")]
    [InlineData("workflow choice")]
    [InlineData("workflow proposal")]
    [InlineData("go-aheads")]
    [InlineData("deleted")]
    public async Task Every_write_to_an_ask_moves_the_asks_family_alone(string write)
    {
        if (write != "asked") await _asks.SaveAsync(Asked);
        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);

        var later = Now.AddMinutes(1);
        await (write switch
        {
            "asked" => _asks.SaveAsync(Asked),
            "saved again" => _asks.SaveAsync(Asked with { Updated = later, Note = "Narrowed to the bridge." }),
            "published" => _asks.RecordPublishedAsync(Asked.Id, "q1", null, later),
            "closed" => _asks.RecordClosedAsync(Asked.Id, "Not needed now.", later),
            "intake" => _asks.RecordIntakeAsync(Asked.Id, "s1", later),
            "word" => _asks.RecordWordAsync(Asked.Id, new AskWord(AskWordKind.Answered, "Yes, the bridge.", later, "s1"), later),
            "review choice" => _asks.RecordReviewChoiceAsync(Asked.Id, new AskReviewChoice("skip", later), later),
            "review proposal" => _asks.RecordReviewProposalAsync(Asked.Id, new AskReviewProposal("skip", "A one-line change.", later), later),
            "workflow choice" => _asks.RecordWorkflowChoiceAsync(Asked.Id, new AskWorkflowChoice("fix", null, later), later),
            "workflow proposal" => _asks.RecordWorkflowProposalAsync(Asked.Id, new AskWorkflowProposal("fix", "A defect.", later), later),
            "go-aheads" => _asks.DecideGoAheadsAsync<int>(Asked.Id, entries => (entries, 0), later),
            "deleted" => _asks.DeleteAsync(Asked.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(write), write, null),
        });
        var answer = await waiting.WaitAsync(Long);

        Assert.Equal([ChangeFamilies.Asks], answer.Moved);
    }

    /// <summary>
    /// PUSH1d1: the revision is kept by triggers in the database, so a connector built before it, which writes the row and knows
    /// nothing of the revision, still moves it. Here that build's write is the bare statement, on a connection that never opened the
    /// asks' store.
    /// </summary>
    [Fact]
    public async Task An_older_connector_s_write_to_an_ask_moves_the_asks_family()
    {
        await _asks.SaveAsync(Asked);
        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);

        await using (var older = new SqliteConnection($"Data Source={_file};Pooling=False"))
        {
            await older.OpenAsync();
            await using var command = older.CreateCommand();
            command.CommandText = "UPDATE asks SET state = 'Closed', note = 'An older build closed it.' WHERE id = 'a5c0de'";
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        Assert.Equal([ChangeFamilies.Asks], (await waiting.WaitAsync(Long)).Moved);
    }

    /// <summary>
    /// PUSH1d1, canary 5's half for the asks (the design §A4.2): what changes no ask moves no family. A connector's start opens the
    /// store again, a write on an id no ask has finds no row, and go-aheads decided to stay as they are write nothing.
    /// </summary>
    [Fact]
    public async Task A_write_that_changes_no_ask_moves_no_family()
    {
        await _asks.SaveAsync(Asked);
        var since = await Bootstrap();
        var seen = _stream.Seen;
        // TESTWAIT2, as below: held until the observer has seen a commit after every write here, then cancelled.
        using var release = new CancellationTokenSource();
        var waiting = _stream.WaitAsync(since, Long, release.Token);

        await using (var connector = new SqliteConnection($"Data Source={_file};Pooling=False"))
        {
            await connector.OpenAsync();
            await AskStore.OpenAsync(connector);
        }

        Assert.False(await _asks.RecordWordAsync("0ther0", new AskWord(AskWordKind.Added, "Nobody's.", Now), Now));
        await _asks.RecordClosedAsync("0ther0", "Nobody's.", Now);
        Assert.Equal((true, 0), await _asks.DecideGoAheadsAsync<int>(Asked.Id, _ => (null, 0), Now));
        await Execute("CREATE TABLE IF NOT EXISTS scratch (x INTEGER); INSERT INTO scratch VALUES (1);");

        var deadline = DateTime.UtcNow + Long;
        while (_stream.Seen <= seen && DateTime.UtcNow < deadline) await Task.Delay(20);
        var observed = _stream.Seen > seen;
        release.Cancel();
        var answer = await waiting.WaitAsync(Long);

        Assert.True(observed, "no write was seen, so the canary proves nothing");
        Assert.Empty(answer.Moved);
    }

    private static ChannelMessage Message(string id, string ask, string? session = null, string? origin = null) =>
        new(id, ask, Workspaces.Default, Now, session, session is null ? null : "Owner", session is null ? null : "stub",
            session is null ? null : "ask:" + ask, To: null, Re: null, Question: false, "Start with the bridge.", origin);

    /// <summary>
    /// PUSH1d1: the channel family moves on every change to what a channel holds: a message posted (the person's or a session's,
    /// through a connector), a teammate's mirrored by a sync pass, and an ask's channel forgotten with the ask. The forgotten one is
    /// an older message than another ask's, so the newest arrival number stays where it was and the count alone tells it.
    /// </summary>
    [Theory]
    [InlineData("posted")]
    [InlineData("mirrored")]
    [InlineData("forgotten")]
    public async Task Every_change_to_a_channel_moves_the_channel_family_alone(string write)
    {
        if (write == "forgotten")
        {
            Assert.True(await _channel.AddAsync(Message("m1", Asked.Id), 10));
            Assert.True(await _channel.AddAsync(Message("m2", "0ther0"), 10));
        }

        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);

        await (write switch
        {
            "posted" => _channel.AddAsync(Message("m1", Asked.Id, session: "s1"), 10),
            "mirrored" => _channel.MirrorAsync(Message("m1", Asked.Id, origin: "teammate@machine")),
            "forgotten" => _channel.ForgetAsync(Asked.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(write), write, null),
        });
        var answer = await waiting.WaitAsync(Long);

        Assert.Equal([ChangeFamilies.Channel], answer.Moved);
    }

    /// <summary>PUSH1d1: a connector built before the count posts with the bare statement, and still moves the family.</summary>
    [Fact]
    public async Task An_older_connector_s_post_moves_the_channel_family()
    {
        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);

        await using (var older = new SqliteConnection($"Data Source={_file};Pooling=False"))
        {
            await older.OpenAsync();
            await using var command = older.CreateCommand();
            command.CommandText = """
                INSERT INTO channel (id, ask, workspace, at, text) VALUES ('m1', 'a5c0de', 'default', '2026-10-17T09:00:00Z', 'Hello.')
                """;
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        Assert.Equal([ChangeFamilies.Channel], (await waiting.WaitAsync(Long)).Moved);
    }

    /// <summary>
    /// PUSH1d1, canary 5's half for the channel: what changes no message moves no family. A read mark is one reader's place, moved
    /// by a read and by the driver as a part starts, and a cursor is the sync's own; a teammate's message mirrored again, a post its
    /// session's cap refused and a connector's start opening the store again change nothing it holds.
    /// </summary>
    [Fact]
    public async Task A_read_mark_a_cursor_or_a_message_seen_again_moves_no_family()
    {
        var mirrored = Message("m1", Asked.Id, origin: "teammate@machine");
        await _channel.MirrorAsync(mirrored);
        Assert.True(await _channel.AddAsync(Message("m2", Asked.Id, session: "s1"), 1));
        var since = await Bootstrap();
        var seen = _stream.Seen;
        using var release = new CancellationTokenSource();
        var waiting = _stream.WaitAsync(since, Long, release.Token);

        await using (var connector = new SqliteConnection($"Data Source={_file};Pooling=False"))
        {
            await connector.OpenAsync();
            await ChannelStore.OpenAsync(connector);
        }

        await _channel.MoveMarkAsync(Asked.Id, "person", Now.AddMinutes(1));
        await _channel.AdvanceCursorAsync(Workspaces.Default, pushed: 2, fetched: 7);
        await _channel.MirrorAsync(mirrored);
        Assert.False(await _channel.AddAsync(Message("m3", Asked.Id, session: "s1"), 1));
        await Execute("CREATE TABLE IF NOT EXISTS scratch (x INTEGER); INSERT INTO scratch VALUES (1);");

        var deadline = DateTime.UtcNow + Long;
        while (_stream.Seen <= seen && DateTime.UtcNow < deadline) await Task.Delay(20);
        var observed = _stream.Seen > seen;
        release.Cancel();
        var answer = await waiting.WaitAsync(Long);

        Assert.True(observed, "no write was seen, so the canary proves nothing");
        Assert.Empty(answer.Moved);
    }

    /// <summary>A family that moved several times since the cursor is answered once; one asked after it moved answers at once.</summary>
    [Fact]
    public async Task Moves_before_the_ask_are_answered_at_once_and_once()
    {
        var since = await Bootstrap();
        await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        await _quests.PublishAsync("Asker", "Owner", "Do the work", "why", Now);

        var clock = Stopwatch.StartNew();
        var answer = await _stream.WaitAsync(since, Long);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"{clock.Elapsed}");
        Assert.Equal([ChangeFamilies.Sessions, ChangeFamilies.Quests], answer.Moved);
    }

    /// <summary>
    /// Canary 5 (the design §A4.2): a look that changes nothing wakes nothing. The driver's own sync pass records itself
    /// (<c>quest_passes</c>), its cursors and what it pushed, every one a write the observer sees, and moves no family; with the
    /// asks and the channel among the families (PUSH1d1), the channel's pass too, and an ask held through both.
    /// </summary>
    [Fact]
    public async Task A_sync_pass_moves_no_family()
    {
        var index = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            index, new LexicalKnowledgeSearch(index), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(
            new Registration("Owner", Adopted: true, "A repo.", [], [], [], 0, Root: "/trees/owner", Joined: true), Now);

        await using var remoteConnection = new SqliteConnection("Data Source=:memory:");
        await remoteConnection.OpenAsync();
        var remote = new StoreRemote(
            await QuestStore.OpenAsync(remoteConnection), await SessionStore.OpenAsync(remoteConnection), service, "person@machine",
            await ChannelStore.OpenAsync(remoteConnection));

        // What this machine made before: a quest, a session and a message on an ask's channel, all still to go up, and the ask.
        await _quests.PublishAsync("Asker", "Owner", "Do the work", "why", Now);
        await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        await _asks.SaveAsync(Asked);
        Assert.True(await _channel.AddAsync(Message("m1", Asked.Id), 10));

        var since = await Bootstrap();
        var seen = _stream.Seen;
        // TESTWAIT2: held open until the observer has seen the passes' writes, then cancelled, as in the test below.
        using var release = new CancellationTokenSource();
        var waiting = _stream.WaitAsync(since, Long, release.Token);

        // Two passes: the first pushes what this machine made, the second finds nothing to do. Each records itself.
        for (var pass = 0; pass < 2; pass++)
        {
            Assert.Null((await QuestSync.RunAsync(_quests, service, remote, Workspaces.Default)).Problem);
            Assert.Null((await SessionSync.RunAsync(_sessions, _quests, service, remote, Workspaces.Default)).Problem);
            var channel = await ChannelSync.RunAsync(_channel, _sessions, service, remote, Workspaces.Default);
            Assert.Null(channel.Problem);
            Assert.Equal(pass == 0 ? 1 : 0, channel.Pushed);
        }

        var deadline = DateTime.UtcNow + Long;
        while (_stream.Seen <= seen && DateTime.UtcNow < deadline) await Task.Delay(20);
        var observed = _stream.Seen > seen;
        release.Cancel();
        var answer = await waiting.WaitAsync(Long);
        Assert.Empty(answer.Moved);
        Assert.Equal(since, answer.Since);
        Assert.True(observed, "the passes' writes were never seen, so the canary proves nothing");
    }

    /// <summary>At most eight waits are held: a ninth is answered at once with the current cursor, and the eight still wake.</summary>
    [Fact]
    public async Task A_ninth_wait_is_answered_at_once()
    {
        var since = await Bootstrap();
        var held = Enumerable.Range(0, ChangeStream.MostWaits).Select(_ => _stream.WaitAsync(since, Long)).ToList();
        await Task.Delay(100);
        Assert.All(held, wait => Assert.False(wait.IsCompleted));

        var clock = Stopwatch.StartNew();
        var ninth = await _stream.WaitAsync(since, Long);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"{clock.Elapsed}");
        Assert.Empty(ninth.Moved);
        Assert.Equal(since, ninth.Since);

        await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        foreach (var answer in await Task.WhenAll(held).WaitAsync(Long))
        {
            Assert.Equal([ChangeFamilies.Sessions], answer.Moved);
        }
    }

    /// <summary>
    /// The observer lost (its read failed) and reconnected: a new epoch, so a wait on the old one is answered every family. The
    /// read fails for as long as a family's revision cannot be read, and the epoch is new only once one reads again.
    /// </summary>
    [Fact]
    public async Task A_reconnected_observer_begins_a_new_epoch()
    {
        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);

        await Execute("DROP TABLE session_revision;");
        await Task.Delay(300);
        Assert.False(waiting.IsCompleted, "a lost observer answered before it read again");

        await Execute("CREATE TABLE session_revision (one INTEGER PRIMARY KEY CHECK (one = 1), revision INTEGER NOT NULL); "
            + "INSERT INTO session_revision VALUES (1, 0);");
        var answer = await waiting.WaitAsync(Long);

        Assert.Equal(ChangeFamilies.All, answer.Moved);
        Assert.NotEqual(since.Split('.')[0], answer.Since.Split('.')[0]);
        Assert.EndsWith(".0", answer.Since);
    }

    /// <summary>
    /// The database at the path is another one (its identity changed): a new epoch. Here the identity is changed in place, as the
    /// file's own handle keeps a replacement out on Windows; the path is read for it each sample.
    /// </summary>
    [Fact]
    public async Task A_replaced_database_begins_a_new_epoch()
    {
        await _stream.DisposeAsync();
        _stream = await Open(identityPace: TimeSpan.Zero);
        var since = await Bootstrap();
        var waiting = _stream.WaitAsync(since, Long);
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);

        await Execute("UPDATE store_identity SET id = 'another' WHERE one = 1;");
        var answer = await waiting.WaitAsync(Long);

        Assert.Equal(ChangeFamilies.All, answer.Moved);
        Assert.NotEqual(since.Split('.')[0], answer.Since.Split('.')[0]);
    }

    /// <summary>
    /// Review of FABRIC-EVENT3b: an observer that could not open at the start still issues cursors; once it opens, the epoch is new,
    /// so what moved meanwhile is answered every family and never taken as the baseline.
    /// </summary>
    [Fact]
    public async Task A_first_open_that_failed_begins_a_new_epoch_once_it_opens()
    {
        var later = Path.Combine(Path.GetTempPath(), "daoris-changes-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        await using var stream = await ChangeStream.OpenAsync(
            later, pace: TimeSpan.FromMilliseconds(20), reopenAfter: TimeSpan.FromMilliseconds(20));
        try
        {
            var since = (await stream.WaitAsync(null, TimeSpan.Zero)).Since;

            // The store comes up, and a session is recorded, while the observer has not yet read anything.
            await using (var store = new SqliteConnection($"Data Source={later};Pooling=False"))
            {
                await store.OpenAsync();
                await QuestStore.OpenAsync(store);
                await AskStore.OpenAsync(store);
                await ChannelStore.OpenAsync(store);
                var sessions = await SessionStore.OpenAsync(store);
                await sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
            }

            var answer = await stream.WaitAsync(since, Long);

            Assert.Equal(ChangeFamilies.All, answer.Moved);
            Assert.NotEqual(since.Split('.')[0], answer.Since.Split('.')[0]);
        }
        finally
        {
            await stream.DisposeAsync();
            if (File.Exists(later)) File.Delete(later);
        }
    }

    /// <summary>
    /// Review of FABRIC-EVENT3b: a wait refused a slot is answered at once, never queued behind a sample a writer's lock holds.
    /// </summary>
    [Fact]
    public async Task A_ninth_wait_never_queues_behind_the_sampler()
    {
        var since = await Bootstrap();
        var held = Enumerable.Range(0, ChangeStream.MostWaits).Select(_ => _stream.WaitAsync(since, Long)).ToList();
        await Task.Delay(100);

        // A writer holds the file: every read the sampler makes waits for it, up to its own timeout.
        await Execute("BEGIN EXCLUSIVE;");
        try
        {
            await Task.Delay(100);
            var clock = Stopwatch.StartNew();
            var ninth = await _stream.WaitAsync(since, Long);

            Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(500), $"{clock.Elapsed}");
            Assert.Empty(ninth.Moved);
        }
        finally
        {
            await Execute("ROLLBACK;");
        }

        await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        Assert.All(await Task.WhenAll(held).WaitAsync(Long), answer => Assert.Equal([ChangeFamilies.Sessions], answer.Moved));
    }

    /// <summary>
    /// Review of FABRIC-EVENT3b: where a replacement can happen under an open handle, a copy carries the stamp, so the path is
    /// another database also when its revisions differ from the observer's while the observer's held still around the read.
    /// </summary>
    [Theory]
    // The same file: the stamp and the revisions agree.
    [InlineData("a", "1,5", "a", "1,5", "1,5", false)]
    // A write between the reads: the observer moved, so nothing can be told this time.
    [InlineData("a", "1,5", "a", "1,6", "1,6", false)]
    // Another stamp: another database.
    [InlineData("a", "1,5", "b", "1,5", "1,5", true)]
    // No stamp there: not the database observed.
    [InlineData("a", "1,5", null, null, "1,5", true)]
    // A copy of an earlier state: the stamp agrees, the revisions do not, and the observer held still.
    [InlineData("a", "1,5", "a", "1,3", "1,5", true)]
    public void A_copy_at_the_path_is_another_database(
        string observed, string before, string? stamp, string? atPath, string after, bool another)
    {
        static long[] Of(string revisions) => [.. revisions.Split(',').Select(long.Parse)];

        Assert.Equal(another, ChangeStream.IsAnother(observed, Of(before), stamp, atPath is null ? null : Of(atPath), Of(after)));
    }

    /// <summary>A write that moves no family's revision wakes nobody, though the observer sees it.</summary>
    [Fact]
    public async Task A_write_outside_every_family_wakes_nobody()
    {
        var since = await Bootstrap();
        var seen = _stream.Seen;
        // TESTWAIT2: the wait is held open until the observer has seen the write (its count moving, bounded by Long), then
        // cancelled: a cancelled wait answers what it would have answered then, so the negative is read at the moment of the
        // positive, not after a clock the load can outrun.
        using var release = new CancellationTokenSource();
        var waiting = _stream.WaitAsync(since, Long, release.Token);

        await Execute("CREATE TABLE IF NOT EXISTS scratch (x INTEGER); INSERT INTO scratch VALUES (1);");
        var deadline = DateTime.UtcNow + Long;
        while (_stream.Seen <= seen && DateTime.UtcNow < deadline) await Task.Delay(20);
        var observed = _stream.Seen > seen;
        release.Cancel();
        var answer = await waiting.WaitAsync(Long);

        Assert.True(observed);
        Assert.Empty(answer.Moved);
    }

    private async Task Execute(string sql)
    {
        await using var command = _writer.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
