using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through the loop: the watch loop holding the home's lease and a hook set, a stand-in
/// performer installed. Where a quest's run is bound to a graph naming the stand-in's step, the loop's runtime awaits it on the branch the
/// run's session worked, the look's supervision starts the process, the step is sent once and its completion heard and kept. Where nothing names a
/// performed step (the owner's install today), the same loop starts no plugin process and writes no unit record.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class NodeRuntimeLoopProcessTests : IDisposable
{
    private const string Plugin = "acme.deploy";

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

    /// <summary>
    /// Quest <c>q1</c>'s run bound to a graph whose entry is the plugin's performed deploy, ahead of the landing, and the branch its session
    /// worked in the repository's checkout: a run's plugin node before its landing observes the run's branch at its tip (the cutover design
    /// §2.2), so a run with no branch withholds it and sends nothing.
    /// </summary>
    private async Task BindDeployAsync()
    {
        var root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(root);
        await GitFixture.GitAsync(root, "init", "-q");
        await GitFixture.GitAsync(root, "symbolic-ref", "HEAD", "refs/heads/main");
        await GitFixture.GitAsync(root, "-c", "user.name=Driver Tests", "-c", "user.email=tests@example.invalid", "commit", "-q", "--allow-empty", "-m", "base");
        await GitFixture.GitAsync(root, "branch", "daoris/s1");
        var tip = (await GitFixture.GitAsync(root, "rev-parse", "daoris/s1")).Trim();
        new SessionBranches(_home).Record(new GrownBranch("engine", "default", "daoris/s1", "main", tip, null, DateTimeOffset.UtcNow) { Unit = "unit-b1" });
        _ledger.Driven("s1", "q1", DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s1"), state: "completed");
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["deploy"],"end":"land",
             "nodes":[{"id":"deploy","kind":"acme.deploy/deploy","version":1,"reach":"performed"},{"id":"land","kind":"land","version":1,"reach":"performed"}],
             "wires":[{"id":"a","kind":"after","from":"deploy","to":"land"}]}
            """), DateTimeOffset.UtcNow);
    }

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-runtimeloop-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

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

        // A process lets go of its folder a moment after it is told to stop, as WorkProcessTests waits for.
        for (var attempt = 0; Directory.Exists(_home); attempt++)
        {
            try
            {
                // Git leaves its objects read-only on Windows.
                foreach (var file in Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
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
    private async Task<int> RunAsync(string cycle, Func<List<TickReport>, bool> until, int pollSeconds = 1)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, $$"""{ {{cycle}} "pollSeconds": {{pollSeconds}} }""");
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _) ?? throw new InvalidOperationException("the home's lease was not taken");
        await using var hooks = new HookSet(_home, say: (_, _) => { });
        var watch = new DriverWatch(_ledger.Client(), config, _home, new SessionProcesses(), sync: null, hooks: hooks,
            harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));
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
            }, () =>
            {
                var states = Record()?.Statuses.Select(each => $"{each.Key}={each.Value.State}") ?? ["no record"];
                lock (reports) return $"the loop never got there in {reports.Count} looks ({string.Join(", ", states)}): " + string.Join(" | ", reports.SelectMany(report => report.Events));
            }, Poll.Patience);
            lock (reports) return reports.Count;
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Poll.Patience); }
            catch (OperationCanceledException) { }
        }
    }

    private NodeRecord? Record() => new NodeRecordFiles(_home).Read(RunUnits.IdOf("q1"));

    [Fact]
    public async Task A_governed_quests_performed_node_is_sent_once_by_the_loop_and_its_completion_kept()
    {
        await BindDeployAsync();
        await RunAsync("", _ => Record()?.Statuses.GetValueOrDefault("deploy")?.State == NodeStates.Reached);

        var record = Record()!;
        var attempt = Assert.Single(record.Attempts);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (attempt.State, attempt.Outcome));
        Assert.Equal([attempt.Call.IdempotencyKey], Lines("effects.log"));
    }

    /// <summary>
    /// With the look's deadline ten minutes off, the loop's start is its only look. The awaited set's own wake starts the
    /// stand-in's process and sends the step with no plan, and the completion the stand-in sends at once is heard by link 8 alone and kept
    /// within seconds of the attempt's record, sometimes before the start's look has reported. A run's answer is judged at its branch's
    /// tip, read from the checkout's ref files, so the bound of three seconds is the process's start and its answer.
    /// The stand-in writes nothing into the plugins folder here, whose change wakes a plan.
    /// </summary>
    [Fact]
    public async Task A_completion_heard_is_kept_within_seconds_with_no_look_after_the_loops_start()
    {
        File.WriteAllText(Path.Combine(Folder, "work.mjs"), Script.Replace("appendFileSync(", "void (", StringComparison.Ordinal));
        DateTimeOffset? reached = null;
        await BindDeployAsync();
        var looks = await RunAsync("", _ =>
        {
            if (Record()?.Statuses.GetValueOrDefault("deploy")?.State != NodeStates.Reached) return false;
            reached ??= DateTimeOffset.UtcNow;
            return true;
        }, pollSeconds: 600);

        Assert.InRange(looks, 0, 1);
        var attempt = Assert.Single(Record()!.Attempts);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (attempt.State, attempt.Outcome));
        Assert.InRange(reached!.Value - attempt.RecordedAt, TimeSpan.Zero, TimeSpan.FromSeconds(3));
    }

    /// <summary>
    /// A run bound to a graph version arms the loop's runtime though its choices name no cycle, and the
    /// loop governs it on its own record, its quest's claim observed there under that quest; no plugin process starts for it, and the quest's own
    /// record is never written.
    /// </summary>
    [Fact]
    public async Task A_run_bound_to_a_graph_version_is_governed_by_the_loop_on_its_own_record()
    {
        const string Graph = """
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """;
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, Graph), DateTimeOffset.UtcNow);
        NodeStatus? Claimed() => new NodeRecordFiles(_home).Read(RunUnits.IdOf("q1"))?.Statuses.GetValueOrDefault("claimed");

        await RunAsync("", _ => Claimed()?.State == NodeStates.Active);

        Assert.Equal(QuestUnits.IdOf("q1"), Claimed()!.Binding!.Subject);
        Assert.False(File.Exists(NodeRecordFiles.PathOf(_home, QuestUnits.IdOf("q1"))));
        Assert.Empty(Lines("started.log"));
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
