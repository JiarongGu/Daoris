using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// QUESTOPENLOCK1: a write with a <c>RETURNING</c> clause, run on its own, writes at its first step and commits when its reader ends,
/// and the driver waits out a busy lock only at a statement's first step. The file keeps a rollback journal, so that commit needs every
/// other host's read to have ended, and a read under way answered it at once as busy: eight sessions started at once on the install,
/// and two of their connectors exited with <i>database is locked</i> at the quest store's version stamp. Each such write now runs in a
/// write transaction of its own, taken at once, whose <c>COMMIT</c> the driver waits on as it does a first step.
/// </summary>
public sealed class ReturningLockTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T09:00:00Z");

    /// <summary>How long another host's read is held: past the driver's first retry, far short of its command timeout.</summary>
    private static readonly TimeSpan Held = TimeSpan.FromMilliseconds(500);

    /// <summary>The quest store's version stamp, as it was written on its own.</summary>
    private const string Stamp = """
        INSERT INTO stamp (one, version) VALUES (1, 1)
        ON CONFLICT(one) DO UPDATE SET version = MAX(version, excluded.version)
        RETURNING version
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-returning-" + Guid.NewGuid().ToString("N")[..8]);

    public ReturningLockTests() => Directory.CreateDirectory(_root);

    private string Database => Path.Combine(_root, "knowledge.db");

    public void Dispose()
    {
        SqlitePools.Release(Database);
        for (var attempt = 0; attempt < 20 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>
    /// Pins what the fix rests on, the driver's own behaviour: a <c>RETURNING</c> write on its own has written by its first row, and
    /// its commit, at the reader's end, is answered busy at once while another host reads; the same write in a transaction taken at
    /// once waits for that read at its <c>COMMIT</c> and is kept. A driver that changed either would change what this fix is for.
    /// </summary>
    [Fact]
    public async Task A_returning_write_on_its_own_is_busy_at_its_end_and_in_a_transaction_waits()
    {
        await using var mine = await HostAsync();
        Execute(mine, "CREATE TABLE stamp (one INTEGER PRIMARY KEY CHECK (one = 1), version INTEGER NOT NULL); INSERT INTO stamp VALUES (1, 1);");
        Assert.Equal("delete", Scalar(mine, "PRAGMA journal_mode"));
        await using var theirs = await HostAsync();
        using var reading = Reading(theirs, "SELECT version FROM stamp");

        using (var alone = mine.CreateCommand())
        {
            alone.CommandText = Stamp;
            var reader = alone.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(1L, reader.GetInt64(0));
            var busy = Assert.Throws<SqliteException>(reader.Dispose);
            Assert.Equal(5, busy.SqliteErrorCode);
        }

        // On a thread of its own: the driver's calls block while they wait for the lock, and the read below must end meanwhile.
        var stamping = Task.Run(() =>
        {
            using var transaction = mine.BeginTransaction(deferred: false);
            using var inside = mine.CreateCommand();
            inside.CommandText = Stamp;
            var stamped = Convert.ToInt64(inside.ExecuteScalar());
            transaction.Commit();
            return stamped;
        });
        await Task.Delay(Held);
        var waited = !stamping.IsCompleted;
        reading.Rollback();

        Assert.Equal(1L, await stamping.WaitAsync(Poll.Patience));
        Assert.True(waited, "the commit did not wait for the other host's read");
    }

    /// <summary>
    /// 🔴 The install's failure: a connector opening the quest store while another host reads. Its version stamp, its first write once
    /// an earlier start has made the tables, waits for the read and is kept, where it threw and the connector exited before it served.
    /// </summary>
    [Fact]
    public async Task A_quest_store_opening_while_another_host_reads_waits_and_stamps()
    {
        await using var mine = await SqliteKnowledgeStore.OpenAsync(Database);
        await QuestStore.OpenAsync(mine.Connection);
        await using var theirs = await HostAsync();
        using var reading = Reading(theirs, "SELECT version FROM quest_store_version");

        var opening = Task.Run(() => QuestStore.OpenAsync(mine.Connection));
        await Task.Delay(Held);
        var waited = !opening.IsCompleted;
        reading.Rollback();

        var opened = await opening.WaitAsync(Poll.Patience);
        Assert.True(waited, "the stamp did not wait for the other host's read");
        Assert.Null(opened.NewerVersion);
        Assert.Equal((long)QuestStore.StoreVersion, Scalar(theirs, "SELECT version FROM quest_store_version"));
    }

    /// <summary>🔴 Several connectors starting at once, as the install's eight sessions did, while another host reads: every one opens.</summary>
    [Fact]
    public async Task Several_hosts_opening_the_quest_store_at_once_while_another_reads_all_open()
    {
        var hosts = new List<SqliteKnowledgeStore>();
        try
        {
            for (var host = 0; host < 4; host++) hosts.Add(await SqliteKnowledgeStore.OpenAsync(Database));
            var first = await QuestStore.OpenAsync(hosts[0].Connection);
            await using var theirs = await HostAsync();
            using var reading = Reading(theirs, "SELECT version FROM quest_store_version");

            var opening = hosts.Select(host => Task.Run(() => QuestStore.OpenAsync(host.Connection))).ToList();
            await Task.Delay(Held);
            var waited = opening.Count(open => !open.IsCompleted);
            reading.Rollback();

            var opened = await Task.WhenAll(opening).WaitAsync(Poll.Patience);
            Assert.Equal(hosts.Count, waited);
            Assert.All(opened, store =>
            {
                Assert.Equal(first.Machine, store.Machine);
                Assert.Null(store.NewerVersion);
            });
        }
        finally
        {
            foreach (var host in hosts) await host.DisposeAsync();
        }
    }

    /// <summary>🔴 A session's post on its ask's channel (<c>RETURNING urgent</c>) while another host reads: it waits and is kept.</summary>
    [Fact]
    public async Task A_channel_post_while_another_host_reads_waits_and_is_kept()
    {
        await using var mine = await SqliteKnowledgeStore.OpenAsync(Database);
        var channel = await ChannelStore.OpenAsync(mine.Connection);
        await using var theirs = await HostAsync();
        using var reading = Reading(theirs, "SELECT COUNT(*) FROM channel");
        var message = new ChannelMessage(
            "msg-waits", "ask-waits", "default", Now, "ses-waits", "reports", "stub", "ask:ask-waits/reports", null, null, false,
            "the bridge wants a column");

        var posting = Task.Run(() => channel.KeepAsync(message, perPart: 10));
        await Task.Delay(Held);
        var waited = !posting.IsCompleted;
        reading.Rollback();

        Assert.NotNull(await posting.WaitAsync(Poll.Patience));
        Assert.True(waited, "the post did not wait for the other host's read");
        Assert.Equal(["msg-waits"], (await channel.OfAsync("ask-waits")).Select(kept => kept.Id));
    }

    /// <summary>
    /// 🔴 A repository's registration (<c>RETURNING workspace, default_branch, lanes</c>), which <c>daoris connect</c> sends on every
    /// sync tick, while another host reads: it waits and is kept.
    /// </summary>
    [Fact]
    public async Task A_registration_while_another_host_reads_waits_and_is_kept()
    {
        await using var mine = await SqliteKnowledgeStore.OpenAsync(Database);
        var registrations = await RegistrationStore.OpenAsync(mine.Connection);
        await using var theirs = await HostAsync();
        using var reading = Reading(theirs, "SELECT COUNT(*) FROM registrations");

        var registering = Task.Run(() => registrations.UpsertAsync(
            new Registration("reports", Adopted: true, "The reports.", [], [], [], 0), Now));
        await Task.Delay(Held);
        var waited = !registering.IsCompleted;
        reading.Rollback();

        var registered = await registering.WaitAsync(Poll.Patience);
        Assert.True(waited, "the registration did not wait for the other host's read");
        Assert.Equal(Workspaces.Default, registered.Workspace);
        Assert.NotNull((await registrations.AllAsync()).Named("reports"));
    }

    /// <summary>
    /// A quest's operations (<c>RETURNING sequence</c>) were already written inside the write's own transaction, taken at once, so a
    /// publish while another host reads waited and was kept before this fix too; held here so a later change cannot lose it.
    /// </summary>
    [Fact]
    public async Task A_quest_published_while_another_host_reads_waits_and_is_kept()
    {
        await using var mine = await SqliteKnowledgeStore.OpenAsync(Database);
        var quests = await QuestStore.OpenAsync(mine.Connection);
        await using var theirs = await HostAsync();
        using var reading = Reading(theirs, "SELECT COUNT(*) FROM quest_log");

        var publishing = Task.Run(() => quests.PublishAsync("Asker", "Owner", "Wait for the read", "Another host is reading.", Now));
        await Task.Delay(Held);
        var waited = !publishing.IsCompleted;
        reading.Rollback();

        var published = await publishing.WaitAsync(Poll.Patience);
        Assert.True(waited, "the publish did not wait for the other host's read");
        Assert.Single(await quests.HistoryAsync(published.Id));
    }

    /// <summary>Another host's connection to the same file, unpooled so it lets go of the file when it goes (FLAKE3).</summary>
    private async Task<SqliteConnection> HostAsync()
    {
        var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>Another host mid-read: a transaction that has read, so it holds the file's shared lock until it ends.</summary>
    private static SqliteTransaction Reading(SqliteConnection theirs, string query)
    {
        var transaction = theirs.BeginTransaction(deferred: true);
        using var read = theirs.CreateCommand();
        read.CommandText = query;
        read.ExecuteScalar();
        return transaction;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
