using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1f part f2's proof, with real processes (the integration design §4.5, §5.6, §5.9): a candidate's gates run through a stand-in
/// runner that honours the contract, or each gate whole where the repository declares none; the candidate's job closed, a grandchild ended,
/// before any place in the machine's admission is let go; the confirm run waiting for a session's heavy run, held to the whole machine,
/// inconclusive when a newcomer holds a place beside it, and ended by a drop with its job; and a failure placed on the member that made it.
/// The runner is a node script keyed by files in its folder; the session's heavy run is the merge tool's own admission, in another process.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class IntegrationVerdictProcessTests : LandedFixture, IDisposable
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    private readonly string _standin;
    private readonly List<Process> _started = [];

    public IntegrationVerdictProcessTests()
    {
        _standin = Path.Combine(Scratch, "standin");
        Directory.CreateDirectory(_standin);
        Write("beat.mjs", """
            import { renameSync, writeFileSync } from 'node:fs';
            const at = process.argv[2];
            let count = 0;
            const beat = () => {
              count += 1;
              writeFileSync(`${at}.next`, String(count));
              renameSync(`${at}.next`, at);
            };
            beat();
            setInterval(beat, 20);
            setTimeout(() => process.exit(0), 120000);
            """);
        Write("runner.mjs", """
            import { appendFileSync, existsSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
            import { createHash } from 'node:crypto';
            import { spawn } from 'node:child_process';
            import { dirname, join } from 'node:path';
            import { fileURLToPath } from 'node:url';
            const here = dirname(fileURLToPath(import.meta.url));
            const flag = (name) => existsSync(join(here, name));
            const sleep = (ms) => new Promise((done) => { setTimeout(done, ms); });
            const env = process.env;
            const mode = env.DAORIS_VERIFY_MODE;
            const tree = env.DAORIS_VERIFY_TREE;
            const text = readFileSync(env.DAORIS_VERIFY_DECLARATION);
            const blob = createHash('sha1').update(`blob ${text.length}\0`).update(text).digest('hex');
            appendFileSync(join(here, 'calls.log'), `${JSON.stringify({
              mode, commit: env.DAORIS_VERIFY_COMMIT, pid: process.pid, hold: env.DAORIS_ADMISSION_HOLD ?? null, gates: env.DAORIS_VERIFY_GATES ?? null,
              daoris: Object.keys(env).filter((name) => /^DAORIS_/i.test(name)).sort(), git: Object.keys(env).filter((name) => /^GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|COMMON_DIR|NAMESPACE|CEILING_DIRECTORIES|CONFIG.*)$|^MERGE_BRANCH_/i.test(name)), tree,
            })}\n`);
            if (flag('busy')) process.exit(75);
            if (flag('refuse') || (mode === 'probe' && flag('refuse-probe'))) {
              console.error('merge-branch: refused for the test');
              process.exit(2);
            }
            const envelope = { schema: 2, mode, commit: env.DAORIS_VERIFY_COMMIT, base: env.DAORIS_VERIFY_BASE, declaration: flag('wrong-blob') ? '0'.repeat(40) : blob };
            const declared = JSON.parse(text).gates.map((gate) => gate.name);
            const named = env.DAORIS_VERIFY_GATES ? env.DAORIS_VERIFY_GATES.split(',') : declared;
            const answer = (body) => {
              writeFileSync(`${env.DAORIS_VERIFY_RESULT}.tmp`, `${JSON.stringify({ ...envelope, ...body })}\n`);
              renameSync(`${env.DAORIS_VERIFY_RESULT}.tmp`, env.DAORIS_VERIFY_RESULT);
            };
            const broken = () => existsSync(join(tree, 'bad.txt'));
            const signature = { test: 'Unit.Fails', exception: 'Error', message: 'boom' };
            const grandchild = (tag) => spawn(process.execPath, [join(here, 'beat.mjs'), join(here, `beat-${tag}.txt`)], { detached: true, stdio: 'ignore' }).unref();
            if (mode === 'plan') {
              const keep = flag('kept') || flag('kept-no-tree');
              const kept = keep ? [{ name: 'lint', why: 'kept', commit: env.DAORIS_VERIFY_COMMIT, ...(flag('kept') ? { tree: 'tree1' } : {}), standing: 'exact' }] : [];
              answer({ run: named.filter((name) => !keep || name !== 'lint').map((name) => ({ name, why: 'asked' })), kept, deferred: [] });
              process.exit(0);
            }
            if (mode === 'run') {
              if (flag('grandchild')) {
                grandchild('run');
                while (!existsSync(join(here, 'beat-run.txt'))) await sleep(10);
              }
              const failing = broken() || flag('fail-run');
              const gates = named.map((name) => {
                const fails = name === 'unit' && failing;
                const settings = join(here, `${name}.composition.json`);
                const digest = `sha256:${createHash('sha256').update(name).digest('hex')}`;
                writeFileSync(settings, JSON.stringify({ schema: 1, digest, units: null, settings: {} }));
                return { name, verdict: fails ? 'fail' : 'pass', exit: fails ? 1 : 0, composition: { units: null, settings, digest }, failures: fails ? [{ unit: 'Unit', test: 'Unit.Fails', signature }] : [] };
              });
              answer({ gates, kept: [], deferred: [] });
              process.exit(gates.some((gate) => gate.verdict === 'fail') ? 1 : 0);
            }
            if (mode === 'probe') {
              if (flag('hang-probe') || flag('probe-wait')) {
                if (flag('hang-probe')) grandchild('probe');
                writeFileSync(join(here, 'probe-started'), String(process.pid));
                if (flag('hang-probe')) await sleep(120000);
                while (!existsSync(join(here, 'probe-go'))) await sleep(10);
              }
              const fails = broken();
              answer({ result: fails ? 'fail' : 'pass', signature: fails ? signature : null });
              process.exit(fails ? 1 : 0);
            }
            process.exit(2);
            """);
        Write("hold.mjs", $$"""
            import { existsSync, writeFileSync } from 'node:fs';
            import { dirname, join } from 'node:path';
            import { fileURLToPath } from 'node:url';
            import { createAdmission } from '{{new Uri(Path.Combine(WorkspaceRoot.Folder, "tools", "machine-admission.mjs")).AbsoluteUri}}';
            const here = dirname(fileURLToPath(import.meta.url));
            const [home, capacity, tag] = process.argv.slice(2);
            const admission = createAdmission({ daorisHome: home, kind: 'session', capacity: Number(capacity), pollMs: 30, beatMs: 200, say: () => {} });
            const hold = await admission.take({ what: 'dotnet test --filter Foo', session: 's-7', repository: 'engine' });
            writeFileSync(join(here, `held-${tag}.txt`), String(process.pid));
            while (!existsSync(join(here, `release-${tag}`))) await new Promise((done) => { setTimeout(done, 20); });
            hold.release();
            """);
        Write("check.mjs", """
            import { appendFileSync, existsSync } from 'node:fs';
            import { dirname, join } from 'node:path';
            import { fileURLToPath } from 'node:url';
            const here = dirname(fileURLToPath(import.meta.url));
            appendFileSync(join(here, 'gates.log'), `${JSON.stringify({ daoris: Object.keys(process.env).filter((name) => /^DAORIS_/i.test(name)).sort(), cwd: process.cwd() })}\n`);
            if (existsSync('bad.txt')) {
              console.error('error: check failed');
              process.exit(1);
            }
            """);
    }

    public new void Dispose()
    {
        foreach (var process in _started)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process.Dispose();
        }

        base.Dispose();
    }

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(_standin, name), text, Utf8);

    private void Flag(string name) => File.WriteAllText(Path.Combine(_standin, name), "");

    private string At(string name) => Path.Combine(_standin, name);

    private Process Spawn(string script, params string[] arguments)
    {
        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = _standin };
        info.ArgumentList.Add(At(script));
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        var process = Process.Start(info)!;
        _started.Add(process);
        return process;
    }

    private async Task SessionHoldsAsync(string tag, int capacity)
    {
        Spawn("hold.mjs", Home, capacity.ToString(System.Globalization.CultureInfo.InvariantCulture), tag);
        await Poll.Until(() => File.Exists(At($"held-{tag}.txt")), () => "the session's heavy run did not take its place");
    }

    private static string BlobId(string text)
    {
        var bytes = Utf8.GetBytes(text);
        return Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes]));
    }

    private const string Declaration = """{ "gates": [ { "name": "unit", "run": "node check.mjs" }, { "name": "lint", "run": "node check.mjs" } ] }""";

    /// <summary>The engine repository on <c>main</c> with the members' commits made in order, each adding its files; the tree stands at the last one.</summary>
    private async Task<(string Root, string Base, IReadOnlyList<string> Tips)> MembersAsync(params (string File, string Content)[] members)
    {
        var root = await RepositoryAsync("engine");
        var @base = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var tips = new List<string>();
        foreach (var (file, content) in members)
        {
            await CommitAsync(root, file, content, $"adds {file}");
            tips.Add((await GitAsync(root, "rev-parse", "HEAD")).Trim());
        }

        return (root, @base, tips);
    }

    private IntegrationWorld World(SaidWriter said, int capacity = 2) => new()
    {
        Start =
        [
            .. System.Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().Select(entry => KeyValuePair.Create((string)entry.Key, (string)entry.Value!)),
            KeyValuePair.Create("DAORIS_HOME", "X:/daoris/home"), KeyValuePair.Create("DAORIS_ROLE_KEY", "secret"), KeyValuePair.Create("GIT_DIR", "X:/daoris/other/.git"),
            KeyValuePair.Create("MERGE_BRANCH_FULL", "1"),
        ],
        Waiting = new AdmissionWaiting(TimeSpan.FromMilliseconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(100), () => DateTimeOffset.UtcNow, KeepLock.Lives),
        Capacity = capacity,
        Said = said,
    };

    private IntegrationVerdictAsk Ask(
        string root, string @base, IReadOnlyList<string> tips, string unconfirmed = "split", bool runner = true, CancellationToken drop = default, string? declaration = null)
    {
        var id = "c" + Guid.NewGuid().ToString("N")[..8];
        var candidate = new IntegrationCandidate(id, "engine", "aurora", "main", @base, IntegrationJobs.NameOf(id), "run-a", 1, DateTimeOffset.UtcNow) { Tip = tips[^1] };
        var text = declaration ?? Declaration;
        return new IntegrationVerdictAsk(
            candidate, root, @base, text, BlobId(text), Home, Path.Combine(Scratch, "judging", id), unconfirmed,
            [.. tips.Select((tip, at) => new VerdictMember($"feature/m{at + 1}", tip))], drop)
        {
            Runner = runner ? new VerdictRunner($"node \"{At("runner.mjs")}\"") : null,
        };
    }

    private Task<IntegrationVerdict> JudgeAsync(IntegrationVerdictAsk ask, SaidWriter? said = null, int capacity = 2) =>
        new IntegrationVerdictOperations(World(said ?? new SaidWriter(), capacity)).JudgeAsync(ask);

    private List<System.Text.Json.JsonElement> Calls() =>
        [.. File.ReadAllLines(At("calls.log")).Select(line => System.Text.Json.JsonDocument.Parse(line).RootElement.Clone())];

    private string Beat(string tag) => File.Exists(At($"beat-{tag}.txt")) ? File.ReadAllText(At($"beat-{tag}.txt")) : "";

    private AdmissionLook Look() => MachineAdmission.Look(Home, KeepLock.Lives, capacity: 2);

    /// <summary>
    /// 🔴 A job closed ends a grandchild, and before the place is let go: the runner starts a detached child that beats, and the verdict is
    /// back only once the child is gone; the beat the child last wrote when the place was seen free is the last it ever wrote.
    /// </summary>
    [Fact]
    public async Task A_job_closed_ends_a_grandchild_before_the_place_in_the_admission_is_let_go()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag("grandchild");
        var ask = Ask(root, @base, tips);
        string? atRelease = null;
        var seenHeld = false;
        using var stop = new CancellationTokenSource();
        var sampler = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                var held = Look().Holds.Count > 0;
                if (held) seenHeld = true;
                else if (seenHeld && atRelease is null) atRelease = Beat("run");
                await Task.Delay(2).ConfigureAwait(false);
            }
        });

        var verdict = await JudgeAsync(ask).WaitAsync(Poll.Patience);
        await stop.CancelAsync();
        await sampler;

        Assert.Equal(VerdictActions.Green, verdict.Action);
        Assert.False(ProcessJob.Exists(ask.Candidate.Job), "the candidate's job must be closed when the verdict is back");
        var first = Beat("run");
        await Task.Delay(300);
        Assert.Equal(first, Beat("run"));
        Assert.True(seenHeld, "the run took a place in the machine's admission");
        Assert.Equal(first, atRelease);
        Assert.Empty(Look().Holds);
    }

    /// <summary>
    /// 🔴 A confirm run waits for a session's heavy run: the failing gate passes its place, the whole admission is asked for in order and
    /// waits behind the session's hold, said; nothing of the confirm run starts until the session lets go, and then it holds the whole
    /// machine and is conclusive.
    /// </summary>
    [Fact]
    public async Task A_confirm_run_waits_for_a_session_s_heavy_run_and_then_holds_the_whole_machine()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag("fail-run");
        await SessionHoldsAsync("one", 2);
        var said = new SaidWriter();

        var judging = JudgeAsync(Ask(root, @base, tips), said);
        await Poll.Until(() => said.Text.Contains("whole heavy-run admission", StringComparison.Ordinal), () => said.Text + string.Join('\n', Look().Holds.Select(hold => hold.What)));
        Assert.False(judging.IsCompleted);
        Assert.DoesNotContain(Calls(), call => call.GetProperty("mode").GetString() == "probe");
        var look = Look();
        Assert.Contains(look.Holds, hold => hold.Kind == "session" && hold.Session == "s-7");
        Assert.Contains("session s-7's dotnet test --filter Foo", said.Text);
        Assert.All(look.Waits.Concat(look.Holds.Where(hold => hold.Kind == "queue")), entry => Assert.Equal("queue", entry.Kind));

        Flag("release-one");
        var verdict = await judging.WaitAsync(Poll.Patience);

        // Its probe passed while it held the whole machine: a sighting, never a pass.
        Assert.Equal(VerdictActions.HandBack, verdict.Action);
        var sighting = Assert.Single(verdict.Sightings);
        Assert.Equal("unconfirmed", sighting.Outcome);
        Assert.True(sighting.Confirm!.Admission!.Conclusive, string.Join("; ", sighting.Confirm.Admission.Doubts));
        Assert.Equal(2, sighting.Confirm.Admission.Ids.Count);
        var handed = Assert.Single(verdict.HandedBack);
        Assert.Equal(("feature/m1", "unresolved"), (handed.Branch, handed.How));
        var probe = Assert.Single(Calls(), call => call.GetProperty("mode").GetString() == "probe");
        Assert.Equal(sighting.Confirm.Admission.Ids[0], probe.GetProperty("hold").GetString());
        Assert.Empty(Look().Holds);
        Assert.Empty(Look().Waits);
    }

    /// <summary>
    /// 🔴 A drop ends the confirm run with its job: the probe hangs with a grandchild beating, the drop is signalled, and the verdict answers
    /// dropped with the runner and its grandchild gone, the job closed and no place held.
    /// </summary>
    [Fact]
    public async Task A_drop_ends_the_confirm_run_with_its_job_and_lets_every_place_go()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag("fail-run");
        Flag("hang-probe");
        using var drop = new CancellationTokenSource();
        var ask = Ask(root, @base, tips, drop: drop.Token);

        var judging = JudgeAsync(ask);
        await Poll.Until(() => File.Exists(At("probe-started")) && Beat("probe").Length > 0, () => "the confirm run did not start its probe");
        var runner = Process.GetProcessById(int.Parse(File.ReadAllText(At("probe-started"))));
        Assert.Equal(2, Look().Holds.Count);
        await drop.CancelAsync();
        var verdict = await judging.WaitAsync(Poll.Patience);

        Assert.Equal(VerdictActions.Dropped, verdict.Action);
        Assert.False(ProcessJob.Exists(ask.Candidate.Job));
        await runner.WaitForExitAsync().WaitAsync(Poll.Patience);
        var beat = Beat("probe");
        await Task.Delay(300);
        Assert.Equal(beat, Beat("probe"));
        Assert.Empty(Look().Holds);
        Assert.Empty(Look().Waits);
        runner.Dispose();
    }

    /// <summary>
    /// 🔴 The whole take is inconclusive when a newcomer holds a place beside it (a writer of a larger capacity): even under the owner's
    /// <c>land</c>, the confirm run's pass is read as split, never land, and the newcomer's place is the doubt said.
    /// </summary>
    [Fact]
    public async Task The_whole_take_is_inconclusive_on_a_newcomer_and_is_never_land()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag("fail-run");
        Flag("probe-wait");
        var judging = JudgeAsync(Ask(root, @base, tips, unconfirmed: "land"));
        await Poll.Until(() => File.Exists(At("probe-started")), () => "the confirm run did not start its probe");
        Assert.Equal(2, Look().Holds.Count);

        await SessionHoldsAsync("newcomer", 3);
        Flag("probe-go");
        var verdict = await judging.WaitAsync(Poll.Patience);
        Flag("release-newcomer");

        Assert.Equal(VerdictActions.HandBack, verdict.Action);
        var inconclusive = Assert.Single(verdict.Judgements);
        Assert.Equal("inconclusive", inconclusive.Outcome);
        Assert.False(inconclusive.Confirm!.Admission!.Conclusive);
        Assert.Contains(inconclusive.Confirm.Admission.Doubts, doubt => doubt.Contains("another live hold stands beside it: session s-7's dotnet test --filter Foo", StringComparison.Ordinal));
        Assert.Single(verdict.Inconclusive);
        Assert.Empty(verdict.Sightings);
        Assert.Empty(verdict.PassedOnConfirm);
        Assert.DoesNotContain(verdict.Action, new[] { VerdictActions.Green, VerdictActions.Land });
    }

    /// <summary>The landing rule's word decides what a conclusive sighting does: <c>land</c> names the gate it passed on, <c>hold</c> waits for a decide, <c>split</c> hands the one member back.</summary>
    [Theory]
    [InlineData("land", VerdictActions.Land)]
    [InlineData("hold", VerdictActions.Hold)]
    [InlineData("split", VerdictActions.HandBack)]
    [InlineData("anything else", VerdictActions.HandBack)]
    public async Task A_sighting_goes_by_the_rules_word_and_is_never_green(string word, string action)
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag("fail-run");

        var verdict = await JudgeAsync(Ask(root, @base, tips, unconfirmed: word)).WaitAsync(Poll.Patience);

        Assert.Equal(action, verdict.Action);
        Assert.Equal("unconfirmed", Assert.Single(verdict.Sightings).Outcome);
        Assert.NotEqual(VerdictActions.Green, verdict.Action);
        Assert.Equal(word == "land" ? new[] { "unit" } : Array.Empty<string>(), verdict.PassedOnConfirm);
        Assert.Empty(Look().Holds);
    }

    /// <summary>A failure that fails again is confirmed, not on the base, and placed on the last member that made it fail, by the prefixes the runner repeats it on.</summary>
    [Fact]
    public async Task A_confirmed_failure_is_placed_on_the_member_that_made_it_by_the_runner_s_probe()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"), ("bad.txt", "broken\n"));

        var verdict = await JudgeAsync(Ask(root, @base, tips)).WaitAsync(Poll.Patience);

        Assert.Equal(VerdictActions.HandBack, verdict.Action);
        var back = Assert.Single(verdict.HandedBack);
        Assert.Equal(("feature/m2", "pinned"), (back.Branch, back.How));
        Assert.Equal(["feature/m1"], verdict.Retain);
        var judged = Assert.Single(verdict.Judgements);
        Assert.Equal(("pinned", 2), (judged.Outcome, judged.Member));
        Assert.Equal("confirmed", judged.Confirm!.Result);
        Assert.True(judged.Confirm.Admission!.Conclusive);
        // The runs the candidate made, each with its composition's digest, and the tree back at the tip.
        Assert.All(verdict.Runs.Where(run => run.Mode == "run"), run => Assert.StartsWith("sha256:", run.Digest, StringComparison.Ordinal));
        Assert.Equal(tips[^1], (await GitAsync(root, "rev-parse", "HEAD")).Trim());
        var probes = Calls().Where(call => call.GetProperty("mode").GetString() == "probe").Select(call => call.GetProperty("commit").GetString()).ToList();
        Assert.Contains(@base, probes);
        Assert.Contains(tips[0], probes);
    }

    /// <summary>The runner's process holds the composed environment and its own inputs, and no other Daoris name, git locating variable or switch of the tool's, even where the install holds them.</summary>
    [Fact]
    public async Task The_runner_process_holds_no_daoris_name_but_its_own_inputs_and_the_hold()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag("fail-run");

        await JudgeAsync(Ask(root, @base, tips)).WaitAsync(Poll.Patience);

        var verify = new[]
        {
            "DAORIS_VERIFY_BASE", "DAORIS_VERIFY_COMMIT", "DAORIS_VERIFY_DECLARATION", "DAORIS_VERIFY_MODE", "DAORIS_VERIFY_RESULT", "DAORIS_VERIFY_TREE",
        };
        foreach (var call in Calls())
        {
            var names = call.GetProperty("daoris").EnumerateArray().Select(name => name.GetString()!).ToList();
            var mode = call.GetProperty("mode").GetString();
            var expected = verify.Concat(mode == "plan" ? [] : ["DAORIS_ADMISSION_HOLD"]).Concat(mode == "probe" ? ["DAORIS_VERIFY_FAILURE"] : []).ToList();
            // The tip's plan names no gates (the runner's own plan), and the run names the ones the plan ran.
            if (mode == "run") expected.Add("DAORIS_VERIFY_GATES");
            Assert.Equal(expected.Order(StringComparer.Ordinal), names);
            Assert.Empty(call.GetProperty("git").EnumerateArray());
        }
    }

    /// <summary>An answer for another blob, a refusal, a busy runner and a digest the probe cannot repeat are each answered as what they are, and none is a pass.</summary>
    [Theory]
    [InlineData("wrong-blob", VerdictActions.Unknown, "the runner's answer is for the declaration")]
    [InlineData("refuse", VerdictActions.Unknown, "the runner refused (exit 2) and wrote no answer: merge-branch: refused for the test")]
    [InlineData("busy", VerdictActions.Busy, "the runner's own run holds its lock")]
    public async Task A_runner_that_cannot_answer_is_unknown_or_busy_and_never_a_pass(string flag, string action, string said)
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag(flag);

        var verdict = await JudgeAsync(Ask(root, @base, tips)).WaitAsync(Poll.Patience);

        Assert.Equal(action, verdict.Action);
        Assert.Contains(said, verdict.Why, StringComparison.Ordinal);
        Assert.Empty(Look().Holds);
    }

    /// <summary>A gate the plan keeps is promoted only on the tree and standing the runner states, and said beside the gates that ran; one it keeps on no tree is unknown.</summary>
    [Theory]
    [InlineData("kept", VerdictActions.Green)]
    [InlineData("kept-no-tree", VerdictActions.Unknown)]
    public async Task A_gate_the_plan_keeps_is_shown_on_its_standing_or_the_run_is_unknown(string flag, string action)
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag(flag);

        var verdict = await JudgeAsync(Ask(root, @base, tips)).WaitAsync(Poll.Patience);

        Assert.Equal(action, verdict.Action);
        if (action == VerdictActions.Green)
        {
            var kept = Assert.Single(verdict.Kept);
            Assert.Equal(("lint", "tree1", "exact"), (kept.Gate, kept.Tree, kept.Standing));
            Assert.Equal(["unit"], verdict.Runs.Where(run => run.Mode == "run").Select(run => run.Gate));
        }
        else
        {
            Assert.Contains("keeps the gate 'lint' on no tree", verdict.Why, StringComparison.Ordinal);
        }
    }

    /// <summary>A probe the runner refuses (the composition's digest at the commit is not the failing run's) is a refusal, not a run: the confirm run is inconclusive and read as split.</summary>
    [Fact]
    public async Task A_probe_the_runner_refuses_is_an_inconclusive_confirm_run_and_never_land()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"));
        Flag("fail-run");
        Flag("refuse-probe");

        var verdict = await JudgeAsync(Ask(root, @base, tips, unconfirmed: "land")).WaitAsync(Poll.Patience);

        Assert.Equal(VerdictActions.HandBack, verdict.Action);
        var judged = Assert.Single(verdict.Judgements);
        Assert.Equal("inconclusive", judged.Outcome);
        Assert.Contains("the runner refused (exit 2) and wrote no answer", judged.Why, StringComparison.Ordinal);
        Assert.Empty(verdict.PassedOnConfirm);
    }

    /// <summary>A repository with no runner runs each gate's command whole in the tree, its exit the verdict, composed as a gate is, and places a failure on its member.</summary>
    [Fact]
    public async Task A_repository_with_no_runner_runs_each_gate_whole_and_judges_its_exit()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"), ("bad.txt", "broken\n"));
        var ask = Ask(root, @base, tips, runner: false, declaration: """{ "gates": [ { "name": "unit", "run": "node check.mjs" }, { "name": "lint", "run": "node -e 0" } ] }""");
        // The tree's own copy of the check, which the declaration's gate runs from the tree: the command is the blob's, the file is the tree's.
        File.Copy(At("check.mjs"), Path.Combine(root, "check.mjs"));

        var verdict = await JudgeAsync(ask).WaitAsync(Poll.Patience);

        Assert.Equal(VerdictActions.HandBack, verdict.Action);
        var back = Assert.Single(verdict.HandedBack);
        Assert.Equal(("feature/m2", "pinned"), (back.Branch, back.How));
        Assert.Contains(verdict.Runs, run => run is { Mode: "run", Gate: "unit", Verdict: "fail" } && run.Digest!.StartsWith("sha256:", StringComparison.Ordinal));
        Assert.Contains(verdict.Runs, run => run is { Mode: "run", Gate: "lint", Verdict: "pass" });
        Assert.Empty(Look().Holds);
    }

    /// <summary>
    /// A gate's log another process still holds open to write is read whole: a process started elsewhere in this one while the gate's start
    /// has the log's handle inheritable keeps a copy of it, and a read that shares no writing reads nothing, a failure signed by no line.
    /// </summary>
    [Fact]
    public async Task A_gate_log_another_process_still_holds_is_read_and_its_failure_placed()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (root, @base, tips) = await MembersAsync(("a.txt", "a\n"), ("bad.txt", "broken\n"));
        var ask = Ask(root, @base, tips, runner: false, declaration: """{ "gates": [ { "name": "unit", "run": "node check.mjs" } ] }""");
        File.Copy(At("check.mjs"), Path.Combine(root, "check.mjs"));
        Directory.CreateDirectory(ask.Scratch);
        // The tip's run's log, held as an inherited copy of its handle holds it: open to write, sharing everything.
        using var inherited = new FileStream(Path.Combine(ask.Scratch, "gate-001-unit.log"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        var verdict = await JudgeAsync(ask).WaitAsync(Poll.Patience);

        Assert.Equal(VerdictActions.HandBack, verdict.Action);
        var back = Assert.Single(verdict.HandedBack);
        Assert.Equal(("feature/m2", "pinned"), (back.Branch, back.How));
    }
}
