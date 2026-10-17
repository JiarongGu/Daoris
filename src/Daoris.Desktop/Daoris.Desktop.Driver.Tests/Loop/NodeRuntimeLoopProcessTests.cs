using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RUN1 through the loop (the fabric events design §D6, §E7, §A3): the watch loop holding the home's lease and a hook set, a stand-in
/// performer installed. Where a repository's quests are governed by a work cycle naming the stand-in's step, the loop's runtime awaits the
/// quest's unit, the look's supervision starts the process, the step is sent once and its completion heard and kept. Where nothing names a
/// performed step (the owner's install today), the same loop starts no plugin process and writes no unit record.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class NodeRuntimeLoopProcessTests : IDisposable
{
    private const string Plugin = "acme.deploy";

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(45);

    /// <summary>The stand-in: it says it started, makes its effect once per key, acknowledges, then reports the step reached.</summary>
    private const string Script = """
        import { createInterface } from 'node:readline';
        import { appendFileSync } from 'node:fs';
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        const seen = new Set();
        for await (const line of createInterface({ input: process.stdin })) {
          const frame = JSON.parse(line);
          if (frame.method === 'initialize') {
            appendFileSync(new URL('./started.log', import.meta.url), 'started\n');
            send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points, capabilities: ['work.perform@1'] } });
          } else if (frame.method === 'hook/work/watch') {
            send({ jsonrpc: '2.0', id: frame.id, result: {} });
          } else if (frame.method === 'hook/work/perform') {
            const { idempotencyKey: key, invocation } = frame.params;
            if (!seen.has(key)) { seen.add(key); appendFileSync(new URL('./effects.log', import.meta.url), key + '\n'); }
            send({ jsonrpc: '2.0', id: frame.id, result: { invocation } });
            send({ jsonrpc: '2.0', method: 'work/step', params: { step: 'deploy', state: 'reached', at: new Date().toISOString(), id: 'e-' + invocation,
              invocation, evidence: { ref: 'acme.deploy/receipt@1:' + invocation, message: 'deployed' } } });
          } else if (frame.method === 'hook/work/reconcile') {
            send({ jsonrpc: '2.0', id: frame.id, result: { state: 'running' } });
          } else if (frame.method === 'shutdown') {
            process.exit(0);
          }
        }
        """;

    private const string Cycle = """
        "workCycles": { "engine": { "quest": {
            "schemaVersion": 2, "entries": ["deploy"], "end": "deploy",
            "nodes": [{ "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" }],
            "wires": [] } } },
        """;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-runtimeloop-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(90));

    public NodeRuntimeLoopProcessTests()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "plugin.json"), """
            { "id": "acme.deploy",
              "hooks": { "command": ["node", "${plugin}/work.mjs"], "points": ["work/watch", "work/step", "work/perform"] },
              "steps": [{ "id": "deploy", "reach": "performed", "perform": { "idempotent": true, "reconcile": "work/reconcile" } }] }
            """);
        File.WriteAllText(Path.Combine(Folder, "work.mjs"), Script);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();

        // FLAKE1: a process lets go of its folder a moment after it is told to stop, as WorkProcessTests waits for.
        for (var attempt = 0; Directory.Exists(_home); attempt++)
        {
            try
            {
                Directory.Delete(_home, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return;
                Thread.Sleep(250);
            }
        }
    }

    private string Folder => DaorisHome.Entry(_home, HomeEntry.Plugins, Plugin);

    private string[] Lines(string file) => File.Exists(Path.Combine(Folder, file)) ? File.ReadAllLines(Path.Combine(Folder, file)) : [];

    /// <summary>The loop as the shell runs it: the home's lease held, a hook set of its own, its choices in <c>driver.json</c>.</summary>
    private async Task<int> RunAsync(string cycle, Func<List<TickReport>, bool> until)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, $$"""{ {{cycle}} "pollSeconds": 1 }""");
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _) ?? throw new InvalidOperationException("the home's lease was not taken");
        await using var hooks = new HookSet(_home, say: (_, _) => { });
        var watch = new DriverWatch(_ledger.Client(), config, _home, new SessionProcesses(), sync: null, hooks: hooks,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var reports = new List<TickReport>();
        var watching = watch.RunAsync((report, _) =>
        {
            lock (reports) reports.Add(report);
            return Task.CompletedTask;
        }, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() =>
            {
                lock (reports) return until(reports);
            }, () => "the loop never got there", Bound);
            lock (reports) return reports.Count;
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Bound); }
            catch (OperationCanceledException) { }
        }
    }

    private NodeRecord? Record() => new NodeRecordFiles(_home).Read(QuestUnits.IdOf("q1"));

    [Fact]
    public async Task A_governed_quests_performed_node_is_sent_once_by_the_loop_and_its_completion_kept()
    {
        await RunAsync(Cycle, _ => Record()?.Statuses.GetValueOrDefault("deploy")?.State == NodeStates.Reached);

        var record = Record()!;
        var attempt = Assert.Single(record.Attempts);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (attempt.State, attempt.Outcome));
        Assert.Equal([attempt.Call.IdempotencyKey], Lines("effects.log"));
    }

    [Fact]
    public async Task An_install_whose_choices_name_no_performed_step_starts_no_plugin_and_writes_no_unit_record()
    {
        var looks = await RunAsync("", reports => reports.Count >= 4);

        Assert.True(looks >= 4);
        Assert.Empty(Lines("started.log"));
        Assert.False(Directory.Exists(NodeRecordFiles.FolderOf(_home)));
    }
}
