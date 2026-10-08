using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A host holds one connection for all its stores and answers requests at once, and a connection is not safe to use from
/// two threads. So every unit of work on it goes through <c>ConnectionGate</c>, commands as well as transactions
/// (SQLITETX1): SYNC1 gated only the transactions, and on the install a say met another request's open transaction
/// (answered 500), and an exception left the database inside a transaction nobody owned, failing every later one (KNOW500).
/// </summary>
public sealed class ConnectionGateTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-08T09:00:00Z");

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static Session Teammates(string id) => new(
        id, "abc123", "Owner", "claude-code", SessionState.Completed, "landed.", null, null, Now, Now) { Origin = "alice-laptop" };

    /// <summary>
    /// The class, not the instance: only the gate makes a command or opens a transaction on a connection, so no store's
    /// command can run beside another request's work. A source scan, because the next store written will not know to.
    /// </summary>
    [Fact]
    public void Only_the_gate_makes_a_command_or_opens_a_transaction_on_a_connection()
    {
        var core = SourceRoot();
        var stores = 0;
        var outside = new List<string>();

        foreach (var file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || Path.GetFileName(file) == "ConnectionGate.cs")
            {
                continue;
            }

            var source = File.ReadAllText(file);
            if (source.Contains("ConnectionGate.For(", StringComparison.Ordinal)) stores++;
            foreach (var reach in new[] { ".CreateCommand(", ".BeginTransaction(", ".BeginTransactionAsync(", ".ExecuteNonQuery(\"" })
            {
                if (source.Contains(reach, StringComparison.Ordinal)) outside.Add($"{Path.GetRelativePath(core, file)}: {reach}");
            }
        }

        Assert.True(stores >= 6, $"expected the six stores over the one connection to take its gate, found {stores}");
        Assert.True(
            outside.Count == 0,
            "a command or a transaction outside the connection's gate meets whatever another request holds open:\n"
            + string.Join('\n', outside));
    }

    /// <summary>A command made outside the gate is refused before it can run, naming why.</summary>
    [Fact]
    public void A_command_made_outside_the_gate_is_refused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => ConnectionGate.For(_connection).Command());

        Assert.Contains("outside the connection's gate", refused.Message);
    }

    /// <summary>
    /// A write arriving while another request's transaction is open waits for it to end, and lands after it, whatever
    /// that transaction does. It used to join the transaction, and a rollback took the write with it, though its request
    /// had answered that it was kept.
    /// </summary>
    [Fact]
    public async Task A_write_beside_an_open_transaction_waits_for_it_and_outlives_its_rollback()
    {
        var opened = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var failing = Task.Run(() => _sessions.ExclusiveAsync<bool>(async inside =>
        {
            await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, tree: "/trees/owner", ct: inside);
            opened.SetResult();
            await release.Task;
            throw new InvalidOperationException("the transaction's work failed");
        }));
        await opened.Task;

        var mirrored = Task.Run(() => _sessions.MirrorAsync(Teammates("cd34ef56")));
        await Task.Delay(200);
        var waited = !mirrored.IsCompleted;
        release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing);
        await mirrored;
        Assert.True(waited, "the write ran inside another request's open transaction");
        Assert.NotNull(await _sessions.FindAsync("cd34ef56"));
        Assert.Single(await _sessions.ListAsync(includeClosed: true));
    }

    /// <summary>A transaction an exception interrupts is rolled back whole, and the store answers the next request.</summary>
    [Fact]
    public async Task A_transaction_an_exception_interrupts_leaves_the_store_usable()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sessions.ExclusiveAsync<bool>(async inside =>
        {
            await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, tree: "/trees/one", ct: inside);
            throw new InvalidOperationException("the transaction's work failed");
        }));

        var next = await _sessions.ExclusiveAsync(inside =>
            _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, tree: "/trees/two", ct: inside));

        Assert.Equal([next.Id], (await _sessions.ListAsync(includeClosed: true)).Select(session => session.Id));
    }

    /// <summary>
    /// KNOW500's shape: the database left inside a transaction no object owns (a BEGIN ran and the object meant to own it
    /// was never made). Nothing can commit it, so the next unit of work rolls it back rather than failing every BEGIN
    /// after it until the host restarts.
    /// </summary>
    [Fact]
    public async Task A_transaction_nobody_owns_is_rolled_back_before_the_next_request()
    {
        await using (var begin = _connection.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE";
            await begin.ExecuteNonQueryAsync();
        }

        var opened = await _sessions.ExclusiveAsync(inside =>
            _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, tree: "/trees/owner", ct: inside));

        Assert.Equal(opened.Id, (await _sessions.FindAsync(opened.Id))!.Id);
    }

    /// <summary>
    /// A unit of work that ends with a transaction still open (a path that began one and never ended it) is rolled back
    /// before the next unit begins, and the unit itself fails, because nothing it wrote there was kept. One that threw
    /// answers with its own exception.
    /// </summary>
    [Fact]
    public async Task A_unit_that_leaves_a_transaction_open_fails_and_the_next_unit_begins_clean()
    {
        var gate = ConnectionGate.For(_connection);

        var left = await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync(async () =>
        {
            await using var begin = gate.Command();
            begin.CommandText = "BEGIN IMMEDIATE; INSERT INTO session_cursor (workspace, pushed, fetched) VALUES ('lost', 1, 1);";
            await begin.ExecuteNonQueryAsync();
        }, CancellationToken.None));
        var threw = await Assert.ThrowsAsync<TimeoutException>(() => gate.RunAsync(() =>
        {
            gate.Command().Dispose();
            _connection.BeginTransaction(deferred: false);
            throw new TimeoutException("its own failure");
        }, CancellationToken.None));

        Assert.Contains("still open", left.Message);
        Assert.Equal("its own failure", threw.Message);
        Assert.Equal((0L, 0L), await _sessions.CursorAsync("lost"));
        var next = await _sessions.ExclusiveAsync(inside =>
            _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, tree: "/trees/owner", ct: inside));
        Assert.NotNull(await _sessions.FindAsync(next.Id));
    }

    /// <summary>
    /// The gate is the flow's own: a transaction's work calls the stores' methods, which run at once inside it and join
    /// its transaction. A wait the caller cancels gives the gate up, and the next request has it.
    /// </summary>
    [Fact]
    public async Task Work_inside_the_gate_reenters_it_and_a_cancelled_wait_gives_it_up()
    {
        var gate = ConnectionGate.For(_connection);
        var opened = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var holding = Task.Run(() => _sessions.ExclusiveAsync(async inside =>
        {
            var session = await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, ct: inside);
            var found = await _sessions.FindAsync(session.Id, inside);
            opened.SetResult();
            await release.Task;
            return found;
        }));
        await opened.Task;
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.RunAsync(() => Task.CompletedTask, cancel.Token));
        release.SetResult();

        Assert.NotNull(await holding);
        Assert.False(gate.Held);
        Assert.Single(await _sessions.ListAsync(includeClosed: true));
    }

    private static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Service.Core")))
        {
            folder = folder.Parent;
        }

        return folder is null
            ? throw new InvalidOperationException("the service source tree was not found above the test binary")
            : Path.Combine(folder.FullName, "Daoris.Service.Core");
    }
}
