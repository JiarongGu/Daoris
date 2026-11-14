using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A declaration's <c>verification</c> runner: its command, its folder, whether it is clock-bound and whether it answers by file.</summary>
internal sealed record VerifyRunner(string Run, string? Cwd, bool Clock, bool Result)
{
    internal static VerifyRunner? Of(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || IntegrationPromoteReads.RunnerOf(text) is not { } runner) return null;
        using var document = JsonDocument.Parse(text);
        var verification = document.RootElement.GetProperty("verification");
        bool Flag(string name) => verification.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        return new VerifyRunner(runner.Run, runner.Cwd, Flag("clock"), Flag("result"));
    }
}

/// <summary>One attempt's run, holding both slots: the accepted declaration read at the commit, then its runner or its gates in the job.</summary>
internal sealed class VerifyExecution(string home, VerifySend send, CandidateJob job, VerifyWorld world, string key, Action<Func<HeavyHolder, HeavyHolder>> keep)
{
    private readonly string _folder = VerifyFiles.AttemptFolder(home, send.Run, send.Invocation);

    public async Task<(VerifyResult? Result, bool Busy)> ExecuteAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_folder);
        if (send.Accepted is null) return (Unknown("no gates are accepted for this repository's verification, so nothing runs until the person accepts them"), false);
        // The declaration is read as git objects at the commit, the blob the person accepted, never a tree's file.
        var (listed, entry, problem) = await WorkingTree.GitAsync(send.Checkout, ["ls-tree", "--full-tree", "-z", send.Commit, "--", DeclarationNames.Gates], ct).ConfigureAwait(false);
        if (listed != 0) return (Unknown($"git could not read {TextBounds.ShortCommit(send.Commit, 7)} here: {problem.Trim()}"), false);
        var parts = entry.Split('\t', 2)[0].Split(' ');
        var tip = parts is [_, "blob", var blob] ? blob.Trim() : HeavyExecutor.NoDeclaration;
        if (!string.Equals(tip, send.Accepted, StringComparison.Ordinal))
        {
            return (Unknown("the gates changed since you accepted them") with { Code = VerifyResult.Changed, Declaration = tip }, false);
        }

        string? text = null;
        if (tip != HeavyExecutor.NoDeclaration)
        {
            text = await GitObjectReads.TextAsync(send.Checkout, tip, 256 * 1024, ct).ConfigureAwait(false);
            if (text is null)
            {
                return (Unknown($"the accepted declaration {TextBounds.ShortCommit(tip, 7)} could not be read here"), false);
            }
        }

        IReadOnlyList<DeclaredGate> declared = text is null
            ? [.. (world.Mapping(send.Repository) ?? []).Select(gate => new DeclaredGate(gate.Name, gate.Run, gate.WorkingDirectory, true))]
            : DeclaredGates.Read(text);
        if (send.Base is { } from && ReachedRunner.Of(text) is { } reached && reached.CommandFor(from) is { } command) return (await ReachedAsync(reached, command, ct).ConfigureAwait(false), false);

        var runner = VerifyRunner.Of(text);
        if (send.Gates is null && runner is not null) return await RunnerAsync(runner, declared, ct).ConfigureAwait(false);

        List<DeclaredGate> chosen;
        if (send.Gates is { } names)
        {
            if (names.FirstOrDefault(name => declared.All(gate => gate.Name != name)) is { } undeclared)
            {
                return (Unknown($"the gate '{undeclared}' is not declared by the accepted declaration"), false);
            }

            chosen = [.. names.Select(name => declared.First(gate => gate.Name == name))];
        }
        else
        {
            chosen = [.. declared.Where(gate => gate.Heavy)];
        }

        if (chosen.Count == 0) return (Unknown("the accepted declaration marks no heavy gate, so the check shows nothing"), false);

        // A batch runs the gates its changed paths reach and those owed; the rest are named as kept. Where git cannot
        // say what changed, every gate runs.
        IReadOnlyList<VerifyGate> kept = [];
        if (send.Reach is { } reach && await MachinePorts.Repository.ChangedAsync(send.Checkout, reach.Base, send.Commit, ct).ConfigureAwait(false) is { } changed)
        {
            var plan = GateReach.Of(chosen, changed, reach.Passed);
            chosen = [.. plan.Run];
            kept = [.. plan.Kept.Select(gate => new VerifyGate(gate.Name, VerifyGate.Kept))];
        }

        if (chosen.Count == 0) return (new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, null, kept) { Declaration = send.Accepted }, false);
        return (await GatesAsync(chosen, kept, ct).ConfigureAwait(false), false);
    }

    private async Task<(VerifyResult? Result, bool Busy)> RunnerAsync(VerifyRunner runner, IReadOnlyList<DeclaredGate> declared, CancellationToken ct)
    {
        var answer = Path.Combine(_folder, "runner-answer.json");
        var log = Path.Combine(_folder, "runner.log");
        if (File.Exists(answer)) File.Delete(answer);
        var inputs = runner.Result
            ? new Dictionary<string, string> { [RunnerInputs.Commit] = send.Commit.ToLowerInvariant(), [RunnerInputs.Result] = answer }
            : new Dictionary<string, string>();
        var folder = runner.Cwd is { } cwd ? Path.GetFullPath(Path.Combine(send.Checkout, cwd)) : send.Checkout;
        keep(holder => holder with { Gates = [VerifyJudge.RunnerGate], Now = new HeavyGateNow(VerifyJudge.RunnerGate, 0, DateTimeOffset.UtcNow) });
        var exit = await StepAsync(runner.Run, folder, [VerifyJudge.RunnerGate], inputs, log, runner.Clock, ct).ConfigureAwait(false);
        if (exit is null) return (Ended([new VerifyGate(VerifyJudge.RunnerGate, RunnerVerdicts.Ended) { Log = log }]), false);
        var judged = runner.Result
            ? VerifyJudge.ByFile(send.Invocation, send.Commit, exit.Value, ReadText(answer), [.. declared.Select(gate => gate.Name)],
                [.. declared.Where(gate => gate.Heavy).Select(gate => gate.Name)], log)
            : VerifyJudge.Opaque(send.Invocation, send.Commit, exit.Value, log);
        return (judged.Result is { } result ? result with { Declaration = send.Accepted } : null, judged.Busy);
    }

    /// <summary>The repository's reached runner in the verification tree at the commit, in place of the gates, under the step's one admission hold.</summary>
    private async Task<VerifyResult> ReachedAsync(ReachedRunner runner, string command, CancellationToken ct)
    {
        var tree = send.Tree ?? Path.Combine(VerifyFiles.LinesFolder(home), "trees", key);
        await TreeAtAsync(tree, ct).ConfigureAwait(false);
        try
        {
            var log = Path.Combine(_folder, "reached.log");
            var folder = runner.Cwd is { } cwd ? Path.GetFullPath(Path.Combine(tree, cwd)) : tree;
            keep(holder => holder with { Gates = [ReachedVerdict.Gate], Now = new HeavyGateNow(ReachedVerdict.Gate, 0, DateTimeOffset.UtcNow) });
            var wall = Stopwatch.StartNew();
            var exit = await StepAsync(command, folder, [ReachedVerdict.Gate], new Dictionary<string, string>(), log, runner.Clock, ct).ConfigureAwait(false);
            if (exit is null) return Ended([new VerifyGate(ReachedVerdict.Gate, RunnerVerdicts.Ended) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds }]);
            return ReachedVerdict.Judge(send.Invocation, send.Commit, exit.Value, ReadText(log) ?? "", log, runner.Outcome) with { Declaration = send.Accepted, WallSeconds = wall.Elapsed.TotalSeconds };
        }
        finally
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>The gates one after another in Daoris's verification tree at the commit, the first failure ending the sequence.</summary>
    private async Task<VerifyResult> GatesAsync(IReadOnlyList<DeclaredGate> gates, IReadOnlyList<VerifyGate> kept, CancellationToken ct)
    {
        var tree = send.Tree ?? Path.Combine(VerifyFiles.LinesFolder(home), "trees", key);
        await TreeAtAsync(tree, ct).ConfigureAwait(false);
        var ran = new List<VerifyGate>();
        try
        {
            for (var at = 0; at < gates.Count; at++)
            {
                var gate = gates[at];
                var log = Path.Combine(_folder, $"gate-{at + 1:D2}-{Slug(gate.Name)}.log");
                var folder = gate.Cwd is { } cwd ? Path.GetFullPath(Path.Combine(tree, cwd)) : tree;
                var index = at;
                keep(holder => holder with { Gates = [.. gates.Select(each => each.Name)], Now = new HeavyGateNow(gate.Name, index, DateTimeOffset.UtcNow) });
                var wall = Stopwatch.StartNew();
                var rest = gates.Skip(at + 1).Select(each => new VerifyGate(each.Name, RunnerVerdicts.NotRun));
                int? exit;
                try
                {
                    exit = Directory.Exists(folder) ? await StepAsync(gate.Run, folder, [gate.Name], new Dictionary<string, string>(), log, gate.Clock, ct).ConfigureAwait(false) : Missing(log, gate);
                }
                catch (DriverException error)
                {
                    return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Unknown, $"the gate '{gate.Name}' would not start: {error.Message}",
                        [.. ran, new VerifyGate(gate.Name, RunnerVerdicts.Ended) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds }, .. rest, .. kept])
                    {
                        Declaration = send.Accepted,
                    };
                }

                if (exit is null) return Ended([.. ran, new VerifyGate(gate.Name, RunnerVerdicts.Ended) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds }, .. rest, .. kept]);
                ran.Add(new VerifyGate(gate.Name, exit == 0 ? RunnerVerdicts.Pass : RunnerVerdicts.Fail, exit) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds });
                if (exit == 0) continue;
                keep(holder => holder with { Failed = new HeavyGateFailed(gate.Name, exit.Value) });
                return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Failed, $"the gate '{gate.Name}' failed with exit {exit}", [.. ran, .. rest, .. kept])
                {
                    Failed = gate.Name, Declaration = send.Accepted,
                };
            }

            return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, null, [.. ran, .. kept]) { Declaration = send.Accepted };
        }
        finally
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The verification tree at the commit: made once, detached, then moved to each run's tip, and locked while this run uses it, the lock's
    /// reason naming this process as a replay's tree names its pass's.
    /// </summary>
    private async Task TreeAtAsync(string tree, CancellationToken ct)
    {
        if (!File.Exists(Path.Combine(tree, ".git")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tree)!);
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "prune"], ct).ConfigureAwait(false);
            var (made, _, problem) = await WorkingTree.GitAsync(send.Checkout, ["worktree", "add", "--detach", "--force", tree, send.Commit], ct).ConfigureAwait(false);
            if (made != 0) throw new DriverException($"the verification tree could not be made at {tree}: {problem.Trim()}");
        }
        else
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], ct).ConfigureAwait(false);
            var (moved, _, problem) = await WorkingTree.GitAsync(tree, ["checkout", "--detach", "--force", send.Commit], ct).ConfigureAwait(false);
            if (moved != 0) throw new DriverException($"the verification tree could not be moved to {TextBounds.ShortCommit(send.Commit, 7)}: {problem.Trim()}");
        }

        await WorkingTree.GitAsync(send.Checkout, ["worktree", "lock", "--reason", PassLeftovers.HeldBy(Environment.ProcessId), tree], ct).ConfigureAwait(false);
    }

    /// <summary>One command in the job, composed from the driver's environment; its exit, or null where the run is ended before it.</summary>
    private async Task<int?> StepAsync(
        string run, string folder, IReadOnlyList<string> running, IReadOnlyDictionary<string, string> inputs, string log, bool clock, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return null;
        AdmissionHold? hold = null;
        try
        {
            hold = await MachineAdmission.TakeAsync(
                home, new AdmissionRequest($"verification of {send.Repository} at {TextBounds.ShortCommit(send.Commit, 7)}") { Repository = send.Repository, Tree = folder },
                world.Said, ct, world.Admission, world.Capacity, world.Variable).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            world.Said.WriteLine($"verify: the machine's admission could not be kept ({error.Message}); the check goes on without a place in it.");
        }

        using (hold)
        {
            var environment = GateEnvironment.Compose(world.Start, null, hold is { IsInside: false } held ? held.Id : null)
                .ToDictionary(each => each.Key, each => each.Value, StringComparer.OrdinalIgnoreCase);
            environment[HeavyRun.Variable] = HeavyRun.Marker(key, running);
            foreach (var (name, value) in inputs) environment[name] = value;
            HeavyStep step;
            try
            {
                step = HeavyStep.Start(job.Job, run, folder, new Dictionary<string, string>(), new StepOptions(environment, log) { BelowNormal = !clock });
            }
            catch (DriverException error)
            {
                await File.WriteAllTextAsync(log, $"error: it would not start: {error.Message}\n", new UTF8Encoding(false), CancellationToken.None).ConfigureAwait(false);
                throw new DriverException(error.Message);
            }

            using (step)
            {
                try
                {
                    return await step.ExitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    job.Job?.Terminate();
                    step.Stop();
                    return null;
                }
                finally
                {
                    // Nothing it left runs past its place in the admission.
                    await job.EndLeftAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private VerifyResult Unknown(string why) => HeavyExecutor.Unknown(send, why);

    private VerifyResult Ended(IReadOnlyList<VerifyGate> gates) =>
        new(send.Invocation, send.Commit, VerifyVerdicts.Unknown, "it was ended before its verdict", gates) { Declaration = send.Accepted };

    private static int? Missing(string log, DeclaredGate gate)
    {
        var why = $"`{gate.Name}`'s cwd, {gate.Cwd}, is no folder of the verification tree";
        File.WriteAllText(log, $"error: {why}\n", new UTF8Encoding(false));
        throw new DriverException(why);
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? AtomicFile.ReadText(path) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Slug(string name) => new([.. name.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '-')]);
}
