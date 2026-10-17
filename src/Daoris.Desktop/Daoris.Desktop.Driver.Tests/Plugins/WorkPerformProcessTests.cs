using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1b against a real plugin process (the fabric events design §D6): a stand-in performer, its step declared in its manifest,
/// asked to perform by the lease's loop, which ends before the completion comes. A new loop over the same home reads the attempt from the
/// unit's record, offers no new attempt, asks <c>work/reconcile</c> of the plugin's new process with the same key, and records the outcome:
/// one delivery and one external effect across the restart.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class WorkPerformProcessTests : IDisposable
{
    private const string Plugin = "acme.deploy";
    private const string Unit = "unit-a";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-performproc-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly MachineLog _log;

    private readonly DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The stand-in: a performer that keeps each key it was given in its own folder, durably, and makes its effect once per key; it
    /// acknowledges an attempt and says nothing more, and answers <c>work/reconcile</c> from what it kept.
    /// </summary>
    private const string Script = """
        import { createInterface } from 'node:readline';
        import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
        const store = new URL('./keys.json', import.meta.url);
        const keys = existsSync(store) ? JSON.parse(readFileSync(store, 'utf8')) : {};
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const frame = JSON.parse(line);
          if (frame.method === 'initialize') {
            send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points, capabilities: ['work.perform@1'] } });
          } else if (frame.method === 'hook/work/watch') {
            send({ jsonrpc: '2.0', id: frame.id, result: {} });
          } else if (frame.method === 'hook/work/perform') {
            const { idempotencyKey: key, invocation } = frame.params;
            appendFileSync(new URL('./performs.log', import.meta.url), key + '\n');
            if (!keys[key]) {
              keys[key] = { invocation };
              writeFileSync(store, JSON.stringify(keys));
              appendFileSync(new URL('./effects.log', import.meta.url), key + '\n');
            }
            send({ jsonrpc: '2.0', id: frame.id, result: { invocation } });
          } else if (frame.method === 'hook/work/reconcile') {
            const kept = keys[frame.params.idempotencyKey];
            send({ jsonrpc: '2.0', id: frame.id, result: kept
              ? { state: 'done', outcome: { state: 'reached', evidence: { ref: 'acme.deploy/receipt@1:' + frame.params.invocation, message: 'deployed' } } }
              : { state: 'not-started' } });
          } else if (frame.method === 'shutdown') {
            process.exit(0);
          }
        }
        """;

    public WorkPerformProcessTests()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, Plugin);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "acme.deploy",
              "hooks": { "command": ["node", "${plugin}/work.mjs"], "points": ["work/watch", "work/step", "work/perform"] },
              "steps": [{ "id": "deploy", "reach": "performed", "perform": { "idempotent": true, "reconcile": "work/reconcile" } }] }
            """);
        File.WriteAllText(Path.Combine(folder, "work.mjs"), Script);
        _log = new MachineLog(_home, "desktop", () => _now);
    }

    public void Dispose()
    {
        _log.Dispose();
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

    /// <summary>A loop's hook set holding the lease, its stand-in started for the unit that awaits its step.</summary>
    private async Task<HookSet> LoopAsync()
    {
        var set = new HookSet(_home, say: (_, _) => { }, log: _log, holdsHome: () => true, clock: () => _now);
        set.Watch(new Dictionary<string, IReadOnlyList<WatchedUnit>>
        {
            [Plugin] = [new("engine", "game", "C:/fam/engine", "quest/a", Tip, "main", null, ["deploy"])],
        });
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Equal(1, set.GenerationOf(Plugin));
        return set;
    }

    /// <summary>The evaluation as the loop runs it, over the unit's record, its moves kept.</summary>
    private WireEvaluated Evaluate(WorkPerformer performer, NodeDeclaration step)
    {
        var record = new NodeRecordFiles(_home).Read(Unit)!;
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
        {
            [step.Id] = record.Statuses.GetValueOrDefault(step.Id) ?? NodeLifecycle.Start(Unit, step.Id),
        };
        var evaluated = WireEvaluation.Evaluate([step], [], before,
            new WireRun("graph-1", "run-1") { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = record.Fired.ToHashSet(StringComparer.Ordinal) });
        performer.Keep(before, evaluated);
        return evaluated;
    }

    [Fact]
    public async Task An_attempt_sent_by_a_loop_that_ended_is_reconciled_by_the_next_and_its_effect_made_once()
    {
        var step = Assert.Single(PluginCatalog.Load(_home).Contributing.Single(plugin => plugin.Manifest.Id == Plugin).Manifest.Steps);
        NodeDeclaration? Declared(string unit, string node) => unit == Unit && node == step.Id ? step : null;

        // The first loop sends the attempt and ends before any completion comes.
        var first = await LoopAsync();
        var before = new WorkPerformer(new NodeRecordFiles(_home), first, () => _now);
        Assert.Equal(PerformedWhat.Sent, (await before.StartAsync(step, Assert.Single(Evaluate(before, step).Starts), CancellationToken.None)).What);
        await first.DisposeAsync();
        var key = Assert.Single(Lines("effects.log"));

        // The next loop: no new attempt is offered, and the attempt is asked after with its key; the plugin's answer is recorded.
        await using var second = await LoopAsync();
        var after = new WorkPerformer(new NodeRecordFiles(_home), second, () => _now);
        Assert.Empty(Evaluate(after, step).Starts);
        var reconciled = Assert.Single(await after.ReconcileAsync(Declared, CancellationToken.None));

        Assert.Equal((PerformedWhat.Applied, WorkStates.Reached), (reconciled.What, reconciled.Code));
        var record = new NodeRecordFiles(_home).Read(Unit)!;
        Assert.Equal(NodeStates.Reached, record.Statuses[step.Id].State);
        var attempt = Assert.Single(record.Attempts);
        Assert.Equal((AttemptStates.Settled, key), (attempt.State, attempt.Call.IdempotencyKey));
        Assert.Equal([key], Lines("performs.log"));
        Assert.Equal([key], Lines("effects.log"));
        Assert.Empty(Evaluate(after, step).Starts);
    }
}
