using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1a-i (D163 §2): the chat runner's ask. What the runner alone knows is refused before a box is kept: a conversation another
/// Daoris process runs, which this one cannot end, and an agent its roster does not hold. The real client over an in-process ledger,
/// nothing spawned, so the fast half; the runs that end a conversation and start another are the <c>Process</c> half's (named in the
/// hand-back, as <c>ChatGoesOnProcessTests</c>' are for MSG1c).
/// </summary>
public sealed class HandOverRunnerTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-handover-run-" + Guid.NewGuid().ToString("N")[..8]);

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
    private sealed class Talk(string name) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");
    }

    private ChatRunner Runner(ChatLedger ledger)
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["talk"] = new Talk("talk"),
            ["other"] = new Talk("other"),
        });
        return new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
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
}
