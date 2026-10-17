using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The work wire against a real plugin process (FABRIC-PERFORM1a; the fabric events design §C4): a stand-in started by the lease's loop as the
/// driver starts any plugin, told what is awaited, heard, asked to perform, crashed, restarted after its wait with a new generation, and the
/// events it sent before it crashed refused as stale. Also the 64 KB bound on a real pipe, which a fake's lines cannot prove.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class WorkProcessTests : IDisposable
{
    private const string Plugin = "acme.deploy";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workproc-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly MachineLog _log;

    private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The stand-in: answers the handshake naming the capability, sends one reached event per awaited unit at each watch frame, a frame
    /// over 64 KB for a unit on <c>quest/huge</c>, and exits (code 3, unasked) once its events for a unit on <c>quest/crash</c> are written.
    /// </summary>
    private const string Script = """
        import { createInterface } from 'node:readline';
        const send = (frame, then) => process.stdout.write(JSON.stringify(frame) + '\n', then);
        let sent = 0;
        for await (const line of createInterface({ input: process.stdin })) {
          const frame = JSON.parse(line);
          if (frame.method === 'initialize') {
            send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points, capabilities: ['work.perform@1'] } });
          } else if (frame.method === 'hook/work/watch') {
            send({ jsonrpc: '2.0', id: frame.id, result: {} });
            for (const unit of frame.params.units) {
              if (unit.branch === 'quest/huge') send({ jsonrpc: '2.0', method: 'work/step', params: { id: 'x'.repeat(70000) } });
              sent += 1;
              const step = { jsonrpc: '2.0', method: 'work/step', params: { step: 'deploy', state: 'reached', at: new Date().toISOString(),
                id: `g${frame.params.generation}-${sent}`, repository: unit.repository, branch: unit.branch, commit: unit.tip } };
              send(step, unit.branch === 'quest/crash' ? () => process.exit(3) : undefined);
            }
          } else if (frame.method === 'hook/work/perform') {
            send({ jsonrpc: '2.0', id: frame.id, result: { invocation: frame.params.invocation } });
          } else if (frame.method === 'shutdown') {
            process.exit(0);
          }
        }
        """;

    public WorkProcessTests()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, Plugin);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "acme.deploy",
              "hooks": { "command": ["node", "${plugin}/work.mjs"], "points": ["work/watch", "work/step", "work/perform"] },
              "steps": [{ "id": "deploy", "reach": "performed" }] }
            """);
        File.WriteAllText(Path.Combine(folder, "work.mjs"), Script);
        _log = new MachineLog(_home, "desktop", () => _now);
    }

    public void Dispose()
    {
        _log.Dispose();
        // FLAKE1: a process lets go of its folder a moment after it is told to stop, as HookTests waits for.
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

    private HookSet Set() => new(_home, say: (_, _) => { }, log: _log, holdsHome: () => true, clock: () => _now);

    private static WatchedUnit Unit(string branch) =>
        new("engine", "game", "C:/fam/engine", branch, "0123456789abcdef0123456789abcdef01234567", "main", null, ["deploy"]);

    private static Dictionary<string, IReadOnlyList<WatchedUnit>> Awaiting(params string[] branches) =>
        new() { [Plugin] = [.. branches.Select(Unit)] };

    private List<LogLine> Lines(string @event) =>
        [.. MachineLogReader.Read(DaorisHome.Entry(_home, HomeEntry.Logs), new LogFilter()).Lines.Where(line => line.Event == @event)];

    private static string? Code(LogLine line) =>
        line.Data.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String ? code.GetString() : null;

    /// <summary>Takes what is heard until <paramref name="enough"/> holds, within a bound generous for a loaded machine.</summary>
    private static async Task<List<WorkStepHeard>> Heard(HookSet set, Func<List<WorkStepHeard>, bool> enough)
    {
        var heard = new List<WorkStepHeard>();
        for (var wait = 0; wait < 600 && !enough(heard); wait++)
        {
            heard.AddRange(set.TakeSteps());
            if (!enough(heard)) await Task.Delay(50);
        }

        Assert.True(enough(heard), $"heard {string.Join(", ", heard.Select(each => each.Step.Id))}");
        return heard;
    }

    [Fact]
    public async Task A_crashed_process_is_restarted_after_its_wait_with_a_new_generation_and_its_stale_events_are_refused()
    {
        await using var set = Set();
        set.Watch(Awaiting("quest/a"));
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        // Generation 1 is told the set, heard, and acknowledges an attempt.
        var first = Assert.Single(await Heard(set, heard => heard.Count >= 1));
        Assert.Equal((Plugin, 1, "g1-1", "quest/a"), (first.Plugin, first.Generation, first.Step.Id, first.Step.Branch));
        Assert.Equal(1, await set.PerformAsync(Plugin,
            new WorkPerformCall("u1", "acme.deploy/deploy", 1, "i1", "k1", "r1", [], _now.AddMinutes(10)), CancellationToken.None));

        // It is told a unit that crashes it: it sends its events and exits unasked, before anything takes them.
        set.Watch(Awaiting("quest/a", "quest/crash"));
        IReadOnlyList<string> lines = [];
        for (var wait = 0; wait < 600 && set.Running.Count > 0; wait++)
        {
            lines = await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
            if (set.Running.Count > 0) await Task.Delay(50);
        }

        Assert.Empty(set.Running);
        Assert.Contains(lines, line => line.Contains("1 minute", StringComparison.Ordinal));
        set.Watch(Awaiting("quest/a"));

        // Not before its wait; then generation 2, told to resync, sends what it knows again.
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Empty(set.Running);
        _now = _now.AddMinutes(1);
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);
        Assert.Equal(2, set.GenerationOf(Plugin));

        var heard = await Heard(set, each => each.Any(step => step.Generation == 2));
        Assert.All(heard, step => Assert.Equal(2, step.Generation));
        Assert.DoesNotContain(heard, step => step.Step.Id.StartsWith("g1-", StringComparison.Ordinal));
        Assert.Contains(Lines(PluginEvents.Step), line => Code(line) == WorkRefusals.NotSender);
        Assert.Contains(Lines(PluginEvents.Failed), line =>
            line.Data.GetProperty("kind").GetString() == PluginEvents.Exited && line.Data.GetProperty("code").GetInt32() == 3);
    }

    [Fact]
    public async Task A_frame_over_64_KB_from_a_real_process_is_dropped_unread_and_the_next_is_heard()
    {
        await using var set = Set();
        set.Watch(Awaiting("quest/huge"));
        await set.ReconcileAsync(PluginCatalog.Load(_home), CancellationToken.None);

        var heard = Assert.Single(await Heard(set, each => each.Count >= 1));

        Assert.Equal("quest/huge", heard.Step.Branch);
        Assert.Contains(Lines(PluginEvents.Step), line => Code(line) == WorkRefusals.TooLong);
    }

    /// <summary>A plugin of its own beside the stand-in, its program <paramref name="script"/>, as installed.</summary>
    private PluginEntry Install(string id, string[] points, string script)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(new { id, hooks = new { command = new[] { "node", "${plugin}/hooks.mjs" }, points } }));
        File.WriteAllText(Path.Combine(folder, "hooks.mjs"), script);
        return PluginCatalog.Load(_home).Contributing.Single(plugin => plugin.Manifest.Id == id);
    }

    /// <summary>
    /// FABRIC-PERFORM1a, round 2: the 64 KB bound is the work wire's. A landing plugin, speaking no work point, that answers with a 70 KB
    /// diagnostic is heard as it was before the bound — it had been dropped, and the landing waited out its patience.
    /// </summary>
    [Fact]
    public async Task A_landing_answer_over_64_KB_from_a_plugin_that_speaks_no_work_point_is_delivered()
    {
        var plugin = Install("acme.lands", [HookPoints.Land], """
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points } });
              else if (frame.method === 'hook/work/land') send({ jsonrpc: '2.0', id: frame.id, result: { pushed: false, message: 'pushed nothing', diagnostic: 'd'.repeat(70000) } });
              else if (frame.method === 'shutdown') process.exit(0);
            }
            """);

        await using var channel = await HookChannels.StartAsync(plugin, _home, _ => { }, CancellationToken.None, TimeSpan.FromSeconds(10));
        var landed = await channel.LandAsync(new { }, CancellationToken.None);

        Assert.Equal((false, "pushed nothing"), (landed.Pushed, landed.Message));
    }

    /// <summary>
    /// FABRIC-PERFORM1a, round 2: a plugin that stops reading its stdin is ended within its grace. Its pipe full, a call's write held the wire,
    /// and the disposal's `shutdown` waited on it with no bound: the process outlived the lease and the loop that owned it.
    /// </summary>
    [Fact]
    public async Task A_plugin_that_stops_reading_is_ended_within_its_grace()
    {
        var plugin = Install("acme.deaf", [HookPoints.Watch], """
            import { createInterface } from 'node:readline';
            import { writeFileSync } from 'node:fs';
            import { join } from 'node:path';
            writeFileSync(join(process.env.DAORIS_PLUGIN_DATA, 'pid'), String(process.pid));
            const lines = createInterface({ input: process.stdin });
            lines.once('line', (line) => {
              const frame = JSON.parse(line);
              process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, points: frame.params.points } }) + '\n');
              // It stops reading: what Daoris writes next fills the pipe and waits there.
              lines.close();
              process.stdin.pause();
            });
            // Never past the test, whatever the test does.
            setTimeout(() => process.exit(0), 90000);
            """);

        var channel = await HookChannels.StartAsync(plugin, _home, _ => { }, CancellationToken.None, TimeSpan.FromSeconds(60));
        var pid = int.Parse(File.ReadAllText(Path.Combine(plugin.Data, "pid")), System.Globalization.CultureInfo.InvariantCulture);
        var units = Enumerable.Range(0, 5000).Select(n => Unit($"quest/{n}")).ToList();
        var stuck = channel.WatchAsync(new WorkWatchFrame(1, true, units), CancellationToken.None);
        await Task.Delay(500);
        Assert.False(stuck.IsCompleted);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        await channel.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(clock.Elapsed < HookProcess.Grace + TimeSpan.FromSeconds(5), $"the disposal took {clock.Elapsed.TotalSeconds:0.#}s");
        for (var wait = 0; wait < 50 && Running(pid); wait++) await Task.Delay(100);
        Assert.False(Running(pid));
    }

    private static bool Running(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
