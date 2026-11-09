using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The chat runner's ask. What the runner alone knows is refused before a box is kept: a conversation another
/// Daoris process runs, which this one cannot end, and an agent its roster does not hold. The real client over an in-process ledger,
/// nothing spawned, so the fast half; the runs that end a conversation and start another are the <c>Process</c> half's.
/// </summary>
public sealed class HandOverRunnerTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-handover-run-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public HandOverRunnerTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A conversation's adapter on the protocol door with no toolchain to ask, so nothing is probed.</summary>
    private static Provider Talk(string name) => TestDoors.Door(name, transport: Transport.Protocol, interactive: true, spawn: new TestDoors.RefusingSpawn("a judgement test spawns nothing"));

    private ChatRunner Runner(ChatLedger ledger)
    {
        var adapters = TestDoors.Registry([Talk("talk"), Talk("other")]);
        return new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private Task<HandedOver> HandAsync(ChatRunner runner, string id, string agent) =>
        runner.HandOverAsync(id, agent, null, HandOverWhy.Press, HandOverBy.Person, DriverConfig.Empty);

    /// <summary>A conversation the ledger says is working that this runner does not hold runs in another process: refused, and no box kept.</summary>
    [Fact]
    public async Task A_conversation_another_process_runs_is_refused_and_no_box_is_kept()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c0nv0001", "working", "talk", _root);
        using var runner = Runner(ledger);

        var handed = await HandAsync(runner, "c0nv0001", "other");

        Assert.Equal((null, false, HandOver.Running), (handed.SessionId, handed.Kept, handed.Why));
        Assert.Equal(
            "c0nv0001 is working, running in another Daoris process on this machine: hand it over there, or stop it first.", handed.Message);
        Assert.Empty(new HandOverBox(_home).Pending());
    }

    /// <summary>An agent the roster does not hold is refused by its code, and an agent that is the one it runs on, too.</summary>
    [Fact]
    public async Task An_agent_the_roster_does_not_hold_or_the_one_it_runs_on_is_refused()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c0nv0001", "completed", "talk", _root);
        using var runner = Runner(ledger);

        Assert.Equal(HandOver.UnknownAgent, (await HandAsync(runner, "c0nv0001", "nope")).Why);
        Assert.Equal(HandOver.SameAgent, (await HandAsync(runner, "c0nv0001", "talk")).Why);
        Assert.Empty(new HandOverBox(_home).Pending());
    }

    /// <summary>
    /// A take's end is told, however it came out, with its move ended in the store by then, so the words the shell held
    /// while it is taken go on to the conversation that continues it. Here the new one's adapter refuses to prepare, so it ends failed.
    /// </summary>
    [Fact]
    public async Task A_takes_end_is_told_once_its_move_has_ended()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c0nv0001", "completed", "talk", _root);
        using var runner = Runner(ledger);
        var ended = new List<(string Session, bool Standing)>();
        runner.TakeEnded += id => { lock (ended) ended.Add((id, new MoveStore(_home).Standing(id) is not null)); };

        await HandAsync(runner, "c0nv0001", "other");

        lock (ended) Assert.Equal([("c0nv0001", false)], ended);
        Assert.Empty(new HandOverBox(_home).Pending());
    }

    /// <summary>
    /// A conversation started to go on from another opens its record naming that one, why and who
    /// asked, read back as the record answers it; one started plainly names none. Nothing is spawned: the stand-in adapter refuses
    /// to prepare, after the record is open, so each record ends failed.
    /// </summary>
    [Fact]
    public async Task A_conversation_started_from_another_opens_its_record_naming_it()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        using var runner = Runner(ledger);
        using var client = ledger.Client();

        await runner.StartAsync("engine", "other", DriverConfig.Empty, tree: _root, from: new ContinuedFrom("c0nv0001", HandOverWhy.Near, HandOverBy.Daoris));
        await runner.StartAsync("engine", "other", DriverConfig.Empty, tree: _root);

        var handed = (await client.RecordAsync("c1"))!;
        Assert.Equal(("c0nv0001", "near", "daoris"), (handed.From, handed.FromWhy, handed.FromBy));
        var plain = (await client.RecordAsync("c2"))!;
        Assert.Equal(((string?)null, (string?)null, (string?)null), (plain.From, plain.FromWhy, plain.FromBy));
    }

    /// <summary>A start cut off between its record's open and its run concludes the record failed, never leaving it queued.</summary>
    [Fact]
    public async Task A_start_cancelled_after_its_record_opened_concludes_it_failed()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        using var runner = Runner(ledger);
        using var cancel = new CancellationTokenSource();
        ledger.BeforeRequest = (_, _) =>
        {
            if (ledger.Count > 0 && !cancel.IsCancellationRequested)
            {
                cancel.Cancel();
                cancel.Token.ThrowIfCancellationRequested();
            }

            return Task.CompletedTask;
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.StartAsync(
            "engine", "other", DriverConfig.Empty, ct: cancel.Token, tree: _root, from: new ContinuedFrom("c0nv0001", HandOverWhy.Near, HandOverBy.Daoris)));

        Assert.Equal("failed", ledger.State("c1"));
    }
}
