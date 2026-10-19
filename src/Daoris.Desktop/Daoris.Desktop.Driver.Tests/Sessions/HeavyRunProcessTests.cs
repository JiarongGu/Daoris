using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1a's proof (D180's HEAVYRUN1 note), on a fixture repository: two runs of one repository's heavy gates never overlap,
/// whichever tree each runs in; a gate's failure and a cancel end everything the run started before its slot is let go; and a
/// wrapper killed outright admits no second run beside what it left. Each gate is a node script that marks its start and end in one
/// log and starts a detached child beating a heartbeat, the shape of a build's lingering workers.
/// </summary>
/// <remarks>Real processes, and the host as built for the wrapper killed outright: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HeavyRunProcessTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("heavy-run");
    private readonly List<Process> _hosts = [];

    public HeavyRunProcessTests()
    {
        // Marks its start once its child beats, so the child is running from the gate's first mark until it is ended.
        File.WriteAllText(_fixture.At("hold.mjs"), """
            import { appendFileSync, existsSync } from 'node:fs';
            import { spawn } from 'node:child_process';
            import { join } from 'node:path';
            const [tag, folder] = process.argv.slice(2);
            const beat = join(folder, `beat-${tag}.txt`);
            spawn(process.execPath, [join(folder, 'beat.mjs'), beat], { detached: true, stdio: 'ignore' }).unref();
            const go = join(folder, `go-${tag}`);
            const begin = setInterval(() => {
              if (!existsSync(beat)) return;
              clearInterval(begin);
              appendFileSync(join(folder, 'intervals.log'), `start ${tag} ${Date.now()}\n`);
              console.log(`begun ${tag}`);
              const end = setInterval(() => {
                if (!existsSync(go)) return;
                clearInterval(end);
                appendFileSync(join(folder, 'intervals.log'), `end ${tag} ${Date.now()}\n`);
                process.exit(0);
              }, 20);
            }, 20);
            setTimeout(() => process.exit(9), 120000);
            """);
        File.WriteAllText(_fixture.At("beat.mjs"), """
            import { writeFileSync } from 'node:fs';
            const at = process.argv[2];
            const beat = () => writeFileSync(at, String(Date.now()));
            beat();
            setInterval(beat, 20);
            setTimeout(() => process.exit(0), 120000);
            """);
        // Passes once its child beats, so the child is running when the gate ends, never ended before it began.
        File.WriteAllText(_fixture.At("leave.mjs"), """
            import { spawn } from 'node:child_process';
            import { existsSync } from 'node:fs';
            import { join } from 'node:path';
            const [folder] = process.argv.slice(2);
            const beat = join(folder, 'beat-left.txt');
            spawn(process.execPath, [join(folder, 'beat.mjs'), beat], { detached: true, stdio: 'ignore' }).unref();
            const timer = setInterval(() => { if (existsSync(beat)) { clearInterval(timer); process.exit(0); } }, 20);
            setTimeout(() => process.exit(9), 120000);
            """);
        File.WriteAllText(_fixture.At("exit.mjs"), "process.exit(Number(process.argv[2]));\n");
        File.WriteAllText(_fixture.At("touch.mjs"), "import { writeFileSync } from 'node:fs';\nwriteFileSync(process.argv[2], process.env.DAORIS_HEAVY_RUN ?? '');\n");
    }

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            try { if (!host.HasExited) host.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            host.Dispose();
        }

        _fixture.Dispose();
    }

    /// <summary>
    /// 🔴 The measured shape: two sessions of one repository, one in its checkout and one in a tree of it, each run the full heavy
    /// sequence. The second waits, says behind whom, and starts only once the first has ended, its lingering child with it: the
    /// instrumented intervals do not overlap, by order and by time.
    /// </summary>
    [Fact]
    public async Task Two_runs_of_one_repository_from_two_trees_never_overlap_and_the_second_says_whom_it_waits_behind()
    {
        if (!OperatingSystem.IsWindows()) return; // The job object is Windows'; elsewhere the slot holds and the descendants are best effort.

        _fixture.Gates(_fixture.Checkout, ("hold", Hold("first"), true));
        _fixture.Gates(_fixture.Tree, ("hold", Hold("second"), true));
        var firstSaid = new SaidWriter();
        var secondSaid = new SaidWriter();

        var first = HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Checkout), firstSaid, CancellationToken.None);
        await Poll.Until(() => Started("first") is not null, () => Log() + firstSaid.Text);
        var second = HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Tree), secondSaid, CancellationToken.None);
        await Poll.Until(() => secondSaid.Text.Contains("waits for it", StringComparison.Ordinal), () => secondSaid.Text);
        Assert.Null(Started("second"));

        Go("first");
        Assert.Equal(0, await first.WaitAsync(Poll.Patience));
        await Poll.Until(() => Started("second") is not null, () => Log() + secondSaid.Text);
        Go("second");
        Assert.Equal(0, await second.WaitAsync(Poll.Patience));

        var marks = Marks();
        Assert.Equal(["start first", "end first", "start second", "end second"], marks.Select(mark => $"{mark.What} {mark.Tag}"));
        Assert.True(marks[1].At <= marks[2].At, Log());
        var lastBeat = await LastBeatAsync("first");
        Assert.True(lastBeat > 0 && lastBeat <= marks[2].At, $"the first run's child beat at {lastBeat}, after the second began at {marks[2].At}");

        var waited = secondSaid.Lines[0];
        Assert.Contains($"`engine`'s heavy-run slot is held by process {Environment.ProcessId}, running hold for", waited);
        Assert.Contains(_fixture.Checkout, waited);
        Assert.Contains("run-heavy: 1 heavy gate passed", secondSaid.Text);
    }

    /// <summary>
    /// HEAVYRUN1b2a's proof: a repository with no gates file runs this machine's mapping for it, from its home's driver file, one gate
    /// after another under its slot, and says whose gates they are.
    /// </summary>
    [Fact]
    public async Task A_repository_with_no_gates_file_runs_this_machine_s_mapping()
    {
        if (!OperatingSystem.IsWindows()) return;

        File.WriteAllText(DaorisHome.Entry(_fixture.Home, HomeEntry.DriverConfig), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["heavyGates"] = new Dictionary<string, object>
            {
                ["engine"] = new[]
                {
                    new { name = "first", run = $"node \"{_fixture.At("touch.mjs")}\" \"{_fixture.At("first.txt")}\"" },
                    new { name = "second", run = $"node \"{_fixture.At("touch.mjs")}\" \"{_fixture.At("second.txt")}\"" },
                },
            },
        }));
        var said = new SaidWriter();

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Checkout), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(_fixture.At("first.txt")) && File.Exists(_fixture.At("second.txt")), said.Text);
        Assert.Contains("run-heavy: `engine` marks no heavy gate of its own, so this machine's run for it: first, second.", said.Lines);
        Assert.Contains("run-heavy: 2 heavy gates passed", said.Text);
    }

    /// <summary>The first gate that fails ends the sequence: the gates after it are not run, and the run says so and exits 1.</summary>
    [Fact]
    public async Task A_gate_that_fails_stops_the_sequence_and_the_run_exits_1()
    {
        if (!OperatingSystem.IsWindows()) return;

        _fixture.Gates(
            _fixture.Checkout,
            ("fails", $"node \"{_fixture.At("exit.mjs")}\" 3", true),
            ("after", $"node \"{_fixture.At("touch.mjs")}\" \"{_fixture.At("after.txt")}\"", true));
        var said = new SaidWriter();

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Checkout), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(1, exit);
        Assert.False(File.Exists(_fixture.At("after.txt")));
        Assert.Contains("run-heavy: `fails` failed (exit 3); not run after it: after.", said.Lines);
        using var free = HeavySlot.TryTake(_fixture.Home, HeavySlot.KeyOf(_fixture.Checkout)!);
        Assert.NotNull(free);
    }

    /// <summary>
    /// A cancel ends the run's job and lets go of the slot only once it is empty: at the first moment the slot is free, the job the
    /// run held its gate in is gone, and the child its gate left beating has stopped.
    /// </summary>
    [Fact]
    public async Task A_cancel_ends_everything_the_gate_started_before_the_slot_is_let_go()
    {
        if (!OperatingSystem.IsWindows()) return;

        _fixture.Gates(_fixture.Checkout, ("hold", Hold("first"), true));
        var key = HeavySlot.KeyOf(_fixture.Checkout)!;
        using var stop = new CancellationTokenSource();
        var said = new SaidWriter();

        var run = HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Checkout), said, stop.Token);
        await Poll.Until(() => Started("first") is not null && File.Exists(_fixture.At("beat-first.txt")), () => Log() + said.Text);
        var job = HeavySlot.Holder(_fixture.Home, key)?.Job;
        Assert.NotNull(job);
        Assert.True(ProcessJob.Exists(job!));
        Assert.True(await Heartbeat.Beating(_fixture.At("beat-first.txt")), "the gate's child should be beating while it runs");

        stop.Cancel();
        HeavySlot? free = null;
        await Poll.Until(() => (free = HeavySlot.TryTake(_fixture.Home, key)) is not null, () => said.Text);
        var freedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var jobLeft = ProcessJob.Exists(job!);
        free!.Dispose();

        Assert.False(jobLeft, "the slot was free while the run's job still held processes");
        Assert.Equal(1, await run.WaitAsync(Poll.Patience));
        var lastBeat = await LastBeatAsync("first");
        Assert.True(lastBeat > 0 && lastBeat <= freedAt, $"the gate's child beat at {lastBeat}, after the slot was free at {freedAt}");
        Assert.False(await Heartbeat.Beating(_fixture.At("beat-first.txt")), "the gate's child outlived the cancelled run");
        Assert.Contains("run-heavy: stopped while `hold` ran; everything it started was ended before the slot was let go.", said.Lines);
    }

    /// <summary>A gate that passes and leaves a process running (a build's lingering worker) has it ended before the slot goes.</summary>
    [Fact]
    public async Task What_a_passing_gate_leaves_running_is_ended_before_the_slot_is_let_go()
    {
        if (!OperatingSystem.IsWindows()) return;

        _fixture.Gates(_fixture.Checkout, ("leaves", $"node \"{_fixture.At("leave.mjs")}\" \"{_fixture.Folder}\"", true));
        var said = new SaidWriter();

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Checkout), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(_fixture.At("beat-left.txt")), said.Text);
        Assert.False(await Heartbeat.Beating(_fixture.At("beat-left.txt")), "the gate's leftover outlived the run");
        Assert.Contains(said.Lines, line => System.Text.RegularExpressions.Regex.IsMatch(line, @"^run-heavy: ended \d+ process(es)? the gates left running before letting go of the slot\.$"));
    }

    /// <summary>
    /// 🔴 Never waits on itself: a heavy gate whose command runs the wrapper again, as the host as built, runs that one inside the slot
    /// it holds, and hands it the marker naming the slot and the gates running, outermost first.
    /// </summary>
    [Fact]
    public async Task A_heavy_gate_that_runs_the_wrapper_again_runs_it_inside_the_slot_it_holds()
    {
        if (!OperatingSystem.IsWindows()) return;

        var inner = $"set \"DAORIS_HOME={_fixture.Home}\" && dotnet \"{DriverHostTests.HostDll()}\" run-heavy --gate inner";
        _fixture.Gates(
            _fixture.Checkout,
            ("outer", inner, true),
            ("inner", $"node \"{_fixture.At("touch.mjs")}\" \"{_fixture.At("marker.txt")}\"", true));
        var said = new SaidWriter();

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk("outer"), World(_fixture.Checkout), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(0, exit);
        var marker = HeavyRun.ReadMarker(File.ReadAllText(_fixture.At("marker.txt")));
        Assert.Equal(HeavySlot.KeyOf(_fixture.Checkout), marker?.Slot);
        Assert.Equal(["outer", "inner"], marker?.Gates);
    }

    /// <summary>
    /// 🔴 A wrapper killed outright, as a session's end kills it, closes its job and its slot with its process. The second run, waiting
    /// in a tree of the same repository, starts only once nothing of the first is left: its job is gone, and the child it left beating
    /// beat last before the second began. Its gate's output reached the terminal as it was said, never held back to its end.
    /// </summary>
    [Fact]
    public async Task A_wrapper_killed_outright_admits_no_second_run_beside_what_it_left()
    {
        if (!OperatingSystem.IsWindows()) return;

        _fixture.Gates(_fixture.Checkout, ("hold", Hold("first"), true));
        _fixture.Gates(_fixture.Tree, ("hold", Hold("second"), true));
        var key = HeavySlot.KeyOf(_fixture.Checkout)!;

        var (firstHost, firstSaid) = Host(_fixture.Checkout);
        await Poll.Until(() => firstSaid.Text.Contains("begun first", StringComparison.Ordinal), () => firstSaid.Text);
        Assert.False(firstHost.HasExited);
        Assert.Null(Ended("first"));
        var job = HeavySlot.Holder(_fixture.Home, key)?.Job;
        Assert.NotNull(job);

        var (secondHost, secondSaid) = Host(_fixture.Tree);
        await Poll.Until(() => secondSaid.Text.Contains("waits for it", StringComparison.Ordinal), () => secondSaid.Text);
        Assert.Contains($"held by process {firstHost.Id}, running hold for", secondSaid.Text);

        firstHost.Kill();
        await Poll.Until(() => Started("second") is not null, () => Log() + secondSaid.Text);

        var lastBeat = await LastBeatAsync("first");
        Assert.True(lastBeat > 0 && lastBeat <= Started("second"), $"the killed run's child beat at {lastBeat}, after the second began at {Started("second")}");
        Assert.False(ProcessJob.Exists(job!), "the killed run's job was still there once the second began");
        Assert.False(await Heartbeat.Beating(_fixture.At("beat-first.txt")), "the killed run's child outlived it");

        Go("second");
        await secondHost.WaitForExitAsync().WaitAsync(Poll.Patience);
        Assert.Equal(0, secondHost.ExitCode);
        Assert.Contains("`engine`'s heavy-run slot is this run's, after waiting", secondSaid.Text);
    }

    private HeavyRunWorld World(string directory) =>
        new(_fixture.Home, directory, _ => null) { Waiting = new HeavyWaiting(TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30), () => DateTimeOffset.UtcNow) };

    private string Hold(string tag) => $"node \"{_fixture.At("hold.mjs")}\" {tag} \"{_fixture.Folder}\"";

    private void Go(string tag) => File.WriteAllText(_fixture.At($"go-{tag}"), "");

    /// <summary>The host as built, run in <paramref name="directory"/> on the fixture's home alone, its output read as it comes.</summary>
    private (Process Host, SaidWriter Said) Host(string directory)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = directory,
        };
        info.ArgumentList.Add(DriverHostTests.HostDll());
        info.ArgumentList.Add("run-heavy");
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = _fixture.Home;

        var said = new SaidWriter();
        var host = new Process { StartInfo = info };
        host.OutputDataReceived += (_, line) => { if (line.Data is { } data) said.WriteLine(data); };
        host.ErrorDataReceived += (_, line) => { if (line.Data is { } data) said.WriteLine(data); };
        host.Start();
        host.BeginOutputReadLine();
        host.BeginErrorReadLine();
        _hosts.Add(host);
        return (host, said);
    }

    private string Log()
    {
        try
        {
            using var stream = new FileStream(_fixture.At("intervals.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private List<(string What, string Tag, long At)> Marks() =>
        [.. Log().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' '))
            .Where(parts => parts.Length == 3 && long.TryParse(parts[2], out _))
            .Select(parts => (parts[0], parts[1], long.Parse(parts[2])))];

    private long? Started(string tag) => Marks().Where(mark => mark.What == "start" && mark.Tag == tag).Select(mark => (long?)mark.At).FirstOrDefault();

    private long? Ended(string tag) => Marks().Where(mark => mark.What == "end" && mark.Tag == tag).Select(mark => (long?)mark.At).FirstOrDefault();

    /// <summary>The last heartbeat a gate's child wrote, in the milliseconds node's clock counts; 0 where it never beat.</summary>
    private async Task<long> LastBeatAsync(string tag) =>
        long.TryParse(await Heartbeat.ReadAsync(_fixture.At($"beat-{tag}.txt")), out var at) ? at : 0;
}
