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

/// <summary>One attempt's run, holding both slots: the declaration it runs under (<see cref="CheckDeclaration"/>, its blob <c>_ran</c>), then its runner or its gates in the job.</summary>
internal sealed class VerifyExecution(string home, VerifySend send, CandidateJob job, VerifyWorld world, string key, string slot, Action<Func<HeavyHolder, HeavyHolder>> keep)
{
    private readonly string _folder = VerifyFiles.AttemptFolder(home, send.Run, send.Invocation);

    private string _at = send.Commit;

    private string? _ran = send.Accepted;

    private IReadOnlyList<string> _regenerate = [];

    public async Task<(VerifyResult? Result, bool Busy)> ExecuteAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_folder);
        AttemptMarks.Write(AttemptMarks.Began(home, send.Run, send.Invocation, send.Delivery), send.Delivery, world.Clock.GetUtcNow());
        var mode = DeclarationMode.Of(home, send.Repository, send.Workspace);
        if (send.Accepted is null && mode == DeclarationMode.Restricted)
        {
            return (Unknown("no gates are accepted for this repository's verification, so nothing runs until the person accepts them"), false);
        }

        var (result, busy) = await RunAsync(mode, ct).ConfigureAwait(false);
        return send.Accepted is null && result is not null
            ? (Told(result, $"nothing was accepted yet, so the work's own declaration ran ({DeclarationMode.Said(DeclarationMode.Full)})"), busy)
            : (result, busy);
    }

    private async Task<(VerifyResult? Result, bool Busy)> RunAsync(string mode, CancellationToken ct)
    {
        var under = await CheckDeclaration.OfAsync(
            send.Checkout, send.Commit, send.Accepted ?? IntegrationAcceptance.None, mode,
            each => CheckDeclaration.LineAsync(home, send.Repository, send.Checkout, send.Workspace, each), ct).ConfigureAwait(false);
        if (under.Problem is { } problem) return (Unknown(problem), false);
        if (under.Held is { } held) return (Unknown(held) with { Code = VerifyResult.Changed, Declaration = under.Work, Accepted = send.Accepted }, false);
        _ran = under.Blob ?? send.Accepted;
        var text = under.Text;
        _regenerate = TreePrepDeclared.Of(text).Regenerate;

        IReadOnlyList<DeclaredGate> declared = text is null
            ? [.. (world.Mapping(send.Repository) ?? []).Select(gate => new DeclaredGate(gate.Name, gate.Run, gate.WorkingDirectory, true))]
            : DeclaredGates.Read(text);
        if (send.Reached) return await HandbackAsync(declared, ReachedRunner.Of(text)?.Outcome ?? ReachedRunner.DefaultOutcome, ct).ConfigureAwait(false);
        if (send.Base is { } from && ReachedRunner.Of(text) is { } reached && reached.CommandFor(from, send.Heavy ? ReachedRunner.EveryTier : ReachedRunner.FastTier) is { } command)
        {
            return (await ReachedAsync(reached, command, declared, ct).ConfigureAwait(false), false);
        }

        var runner = VerifyRunner.Of(text);
        if (send.Gates is null && runner is not null && !send.Full) return await RunnerAsync(runner, declared, ct).ConfigureAwait(false);

        List<DeclaredGate> chosen;
        if (send.Full)
        {
            chosen = [.. declared];
        }
        else if (send.Gates is { } names)
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

        LineMerge? merge = null;
        if (send.AsMerged && await HandbackReading.LineAsync(send.Checkout, ct).ConfigureAwait(false) is { } line)
        {
            merge = await OntoAsync(line, ct).ConfigureAwait(false);
            if (merge is { Conflicts.Count: > 0 }) return (Back(merge.Words()!), false);
        }

        if (chosen.Count == 0) return (Told(new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, null, kept) { Declaration = _ran }, merge?.Words()), false);
        return (Told(await GatesAsync(chosen, kept, ct).ConfigureAwait(false), merge?.Words()), false);
    }

    private async Task<LineMerge?> OntoAsync(string line, CancellationToken ct)
    {
        if (!send.AsMerged) return null;
        var merge = await LineMerge.ReadAsync(send.Checkout, line, send.Commit, ct).ConfigureAwait(false);
        _at = merge.At ?? send.Commit;
        return merge;
    }

    private VerifyResult Back(string why) =>
        new(send.Invocation, send.Commit, VerifyVerdicts.Failed, why, [new VerifyGate(HandbackReading.Gate, RunnerVerdicts.Fail)]) { Failed = HandbackReading.Gate, Declaration = _ran };

    private static VerifyResult Told(VerifyResult result, string? words) =>
        words is null ? result : result with { Why = result.Why is { } why ? $"{why}\n{words}" : words };

    /// <summary>The <c>reached</c> choice: the hand-back read from git, a fault there the verdict and no gate run; else the required commands (<see cref="HandbackGates"/>) the change reaches, each printing the outcome line (<paramref name="marker"/>) saying what it ran, replayed and left owed (<see cref="ReachedVerdict.Told"/>). No line, or git unable to say, is unrun and never failed.</summary>
    private async Task<(VerifyResult? Result, bool Busy)> HandbackAsync(IReadOnlyList<DeclaredGate> declared, string marker, CancellationToken ct)
    {
        if (await HandbackReading.LineAsync(send.Checkout, ct).ConfigureAwait(false) is not { } line)
        {
            return (Unknown("no line (the remote's head, `main` or `master`) is here to read the hand-back against") with { Code = VerifyResult.Unrun }, false);
        }

        if (await HandbackReading.ReadAsync(send.Checkout, line, send.Commit, ct).ConfigureAwait(false) is not { } read)
        {
            return (Unknown($"git could not read the hand-back at {TextBounds.ShortCommit(send.Commit, 7)} against `{line}`") with { Code = VerifyResult.Unrun }, false);
        }

        var seen = read with { Merge = await OntoAsync(line, ct).ConfigureAwait(false) };
        if (seen.Faulty) return (Back(seen.Words()), false);

        var files = new CheckFiles(home);
        var required = declared.Where(gate => gate.Handback && GateReach.Reaches(gate, seen.Touched)).ToList();
        var verified = required.Where(gate => HandbackGates.Verified(files, gate, _at)).ToList();
        var kept = verified.Select(gate => new VerifyGate(gate.Name, VerifyGate.Kept)).ToList();
        var chosen = required.Except(verified).Select(HandbackGates.ForHandback).ToList();
        if (chosen.Count == 0) return (new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, seen.Words(), kept) { Declaration = _ran }, false);
        var result = Told(await GatesAsync(chosen, kept, ct, marker).ConfigureAwait(false), seen.Words());
        var classes = result.Gates.Where(gate => gate is { Log: not null, Verdict: RunnerVerdicts.Pass })
            .Select(gate => ReadText(gate.Log!) is { } log ? ReachedVerdict.Told(log, marker) : null).OfType<string>().ToList();
        return (classes.Count == 0 ? result : Told(result, string.Join("\n", classes)), false);
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
        keep(holder => holder with { Gates = [VerifyJudge.RunnerGate], Now = new HeavyGateNow(VerifyJudge.RunnerGate, 0, world.Clock.GetUtcNow()) });
        using var place = await PlaceAsync(folder, ct).ConfigureAwait(false);
        var exit = place is null ? null : await StepAsync(place.Hold, runner.Run, folder, [VerifyJudge.RunnerGate], inputs, log, runner.Clock, ct).ConfigureAwait(false);
        if (exit is null) return (Ended([new VerifyGate(VerifyJudge.RunnerGate, RunnerVerdicts.Ended) { Log = log }]), false);
        var judged = runner.Result
            ? VerifyJudge.ByFile(send.Invocation, send.Commit, exit.Value, ReadText(answer), [.. declared.Select(gate => gate.Name)],
                [.. declared.Where(gate => gate.Heavy).Select(gate => gate.Name)], log)
            : VerifyJudge.Opaque(send.Invocation, send.Commit, exit.Value, log);
        return (judged.Result is { } result ? result with { Declaration = _ran } : null, judged.Busy);
    }

    /// <summary>The repository's reached runner in the verification tree at the commit, in place of the gates, under the step's one admission hold.</summary>
    private async Task<VerifyResult> ReachedAsync(ReachedRunner runner, string command, IReadOnlyList<DeclaredGate> declared, CancellationToken ct)
    {
        var tree = send.Tree ?? Path.Combine(VerifyFiles.LinesFolder(home), "trees", slot);
        await TreeAtAsync(tree, ct).ConfigureAwait(false);
        try
        {
            IReadOnlyList<VerifyGate> unrun = [new VerifyGate(ReachedVerdict.Gate, RunnerVerdicts.NotRun)];
            if (await UnpreparedAsync(tree, unrun, ct).ConfigureAwait(false) is { } unprepared) return unprepared;
            var log = Path.Combine(_folder, "reached.log");
            var folder = runner.Cwd is { } cwd ? Path.GetFullPath(Path.Combine(tree, cwd)) : tree;
            var ended = Ended([new VerifyGate(ReachedVerdict.Gate, RunnerVerdicts.Ended) { Log = log }]);
            using var place = await PlaceAsync(tree, ct).ConfigureAwait(false);
            if (place is null) return ended;
            if (await UnregeneratedAsync(tree, place, unrun, ct).ConfigureAwait(false) is { } regenerated) return regenerated;
            keep(holder => holder with { Gates = [ReachedVerdict.Gate], Now = new HeavyGateNow(ReachedVerdict.Gate, 0, world.Clock.GetUtcNow()) });
            var wall = Stopwatch.StartNew();
            var exit = await StepAsync(place.Hold, command, folder, [ReachedVerdict.Gate], new Dictionary<string, string>(), log, runner.Clock, ct).ConfigureAwait(false);
            if (exit is null) return Ended([new VerifyGate(ReachedVerdict.Gate, RunnerVerdicts.Ended) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds }]);
            return ReachedVerdict.Judge(send.Invocation, send.Commit, exit.Value, ReadText(log) ?? "", log, runner.Outcome) with { Declaration = _ran, WallSeconds = wall.Elapsed.TotalSeconds };
        }
        finally
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>The gates one after another in Daoris's verification tree at the commit, the first failure ending the sequence and told by its log.</summary>
    private async Task<VerifyResult> GatesAsync(IReadOnlyList<DeclaredGate> gates, IReadOnlyList<VerifyGate> kept, CancellationToken ct, string? marker = null)
    {
        var tree = send.Tree ?? Path.Combine(VerifyFiles.LinesFolder(home), "trees", slot);
        await TreeAtAsync(tree, ct).ConfigureAwait(false);
        var ran = new List<VerifyGate>();
        try
        {
            IReadOnlyList<VerifyGate> unrun = [.. gates.Select(each => new VerifyGate(each.Name, RunnerVerdicts.NotRun)), .. kept];
            if (await UnpreparedAsync(tree, unrun, ct).ConfigureAwait(false) is { } unprepared) return unprepared;
            using var place = await PlaceAsync(tree, ct).ConfigureAwait(false);
            if (place is null) return Ended(unrun);
            if (await UnregeneratedAsync(tree, place, unrun, ct).ConfigureAwait(false) is { } regenerated) return regenerated;

            for (var at = 0; at < gates.Count; at++)
            {
                var gate = gates[at];
                var log = Path.Combine(_folder, $"gate-{at + 1:D2}-{Slug(gate.Name)}.log");
                var folder = gate.Cwd is { } cwd ? Path.GetFullPath(Path.Combine(tree, cwd)) : tree;
                var index = at;
                keep(holder => holder with { Gates = [.. gates.Select(each => each.Name)], Now = new HeavyGateNow(gate.Name, index, world.Clock.GetUtcNow()) });
                var wall = Stopwatch.StartNew();
                var rest = gates.Skip(at + 1).Select(each => new VerifyGate(each.Name, RunnerVerdicts.NotRun));
                int? exit;
                try
                {
                    exit = Directory.Exists(folder) ? await StepAsync(place.Hold, gate.Run, folder, [gate.Name], new Dictionary<string, string>(), log, gate.Clock, ct).ConfigureAwait(false) : Missing(log, gate);
                }
                catch (DriverException error)
                {
                    return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Unknown, $"the gate '{gate.Name}' would not start: {error.Message}",
                        [.. ran, new VerifyGate(gate.Name, RunnerVerdicts.Ended) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds }, .. rest, .. kept])
                    {
                        Declaration = _ran,
                    };
                }

                if (exit is null) return Ended([.. ran, new VerifyGate(gate.Name, RunnerVerdicts.Ended) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds }, .. rest, .. kept]);
                ran.Add(new VerifyGate(gate.Name, exit == 0 ? RunnerVerdicts.Pass : RunnerVerdicts.Fail, exit) { Log = log, WallSeconds = wall.Elapsed.TotalSeconds });
                if (exit == 0) continue;
                keep(holder => holder with { Failed = new HeavyGateFailed(gate.Name, exit.Value) });
                var failed = new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Failed, $"the gate '{gate.Name}' failed with exit {exit}", [.. ran, .. rest, .. kept])
                {
                    Failed = gate.Name, Declaration = _ran,
                };
                return Told(failed, ReadText(log) is not { } text ? null
                    : marker is not null && ReachedVerdict.Told(text, marker, log) is { } classes ? classes
                    : GateLogExcerpt.Told(text, log, home));
            }

            return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, null, [.. ran, .. kept]) { Declaration = _ran };
        }
        finally
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The verification tree at the commit it takes (<see cref="_at"/>): made once, detached, then moved to each run's, and locked while this
    /// run uses it, the lock's reason naming this process as a replay's tree names its pass's.
    /// </summary>
    private async Task TreeAtAsync(string tree, CancellationToken ct)
    {
        if (!File.Exists(Path.Combine(tree, ".git")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tree)!);
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "prune"], ct).ConfigureAwait(false);
            var (made, _, problem) = await WorkingTree.GitAsync(send.Checkout, ["worktree", "add", "--detach", "--force", tree, _at], ct).ConfigureAwait(false);
            if (made != 0) throw new DriverException($"the verification tree could not be made at {tree}: {problem.Trim()}");
        }
        else
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], ct).ConfigureAwait(false);
            var (moved, _, problem) = await WorkingTree.GitAsync(tree, ["checkout", "--detach", "--force", _at], ct).ConfigureAwait(false);
            if (moved != 0) throw new DriverException($"the verification tree could not be moved to {TextBounds.ShortCommit(_at, 7)}: {problem.Trim()}");
        }

        await WorkingTree.GitAsync(send.Checkout, ["worktree", "lock", "--reason", PassLeftovers.HeldBy(Environment.ProcessId), tree], ct).ConfigureAwait(false);
    }

    private async Task<VerifyResult?> UnpreparedAsync(string tree, IReadOnlyList<VerifyGate> pending, CancellationToken ct) =>
        await UnfinishedAsync(() => VerifyTreeReady.EnsureAsync(home, send, tree, world.Preparation, ct), pending, ct).ConfigureAwait(false);

    private async Task<VerifyResult?> UnregeneratedAsync(string tree, CheckPlace place, IReadOnlyList<VerifyGate> pending, CancellationToken ct) =>
        await UnfinishedAsync(() => RegeneratedAsync(tree, place.Hold?.Id, ct), pending, ct).ConfigureAwait(false);

    private async Task<VerifyResult?> UnfinishedAsync(Func<Task<string?>> step, IReadOnlyList<VerifyGate> pending, CancellationToken ct)
    {
        string? failed;
        try
        {
            failed = await step().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Ended(pending);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            failed = error.Message.Trim();
        }

        if (failed is null) return null;
        var why = $"the verification tree's preparation did not finish: {failed}";
        world.Said.WriteLine($"verify: {why}");
        return Unknown(why) with { Code = VerifyResult.Unprepared };
    }

    private async Task<string?> RegeneratedAsync(string tree, string? place, CancellationToken ct)
    {
        if (await TreePreparation.RegenerateAsync(home, send.Repository, _regenerate, tree, ct, place).ConfigureAwait(false) is not { } failed) return null;
        var log = Path.Combine(_folder, "regenerate.log");
        string? kept = log;
        try
        {
            AtomicFile.WriteText(log, failed.Ran.Output);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            kept = null;
        }

        return $"`{failed.Command}` exited {failed.Ran.Code}" + (GateLogExcerpt.Told(failed.Ran.Output, kept, home) is { } told ? $"\n{told}" : "");
    }

    private async Task<CheckPlace?> PlaceAsync(string folder, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return null;
        try
        {
            return new CheckPlace(await MachineAdmission.TakeAsync(
                home, new AdmissionRequest($"verification of {send.Repository} at {TextBounds.ShortCommit(send.Commit, 7)}") { Repository = send.Repository, Tree = folder },
                world.Said, ct, world.Admission, world.Capacity, world.Variable).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            world.Said.WriteLine($"verify: the machine's admission could not be kept ({error.Message}); the check goes on without a place in it.");
            return new CheckPlace(null);
        }
    }

    private sealed class CheckPlace(AdmissionHold? hold) : IDisposable
    {
        internal AdmissionHold? Hold { get; } = hold;

        public void Dispose() => Hold?.Dispose();
    }

    /// <summary>One command in the job, composed from the driver's environment; its exit, or null where the run is ended before it.</summary>
    private async Task<int?> StepAsync(
        AdmissionHold? hold, string run, string folder, IReadOnlyList<string> running, IReadOnlyDictionary<string, string> inputs, string log, bool clock, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return null;
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

    private VerifyResult Unknown(string why) => HeavyExecutor.Unknown(send, why) with { Declaration = _ran };

    private VerifyResult Ended(IReadOnlyList<VerifyGate> gates) =>
        new(send.Invocation, send.Commit, VerifyVerdicts.Unknown, "it was ended before its verdict", gates) { Declaration = _ran };

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
