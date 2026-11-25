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

    private string? _onto;

    private IReadOnlyList<string> _regenerate = [];

    private FailedRun? _failed;

    private LoadReading? _seen;

    private ReachedRunner? _reached;

    private string? _baseRef;

    private LineMerge? _merge;

    private long _began;

    private bool _loadChecked;

    private string? _loadGate;

    private sealed record FailedRun(string Gate, string Folder, bool Clock, string Log, string Marker);

    public async Task<(VerifyResult? Result, bool Busy)> ExecuteAsync(CancellationToken ct)
    {
        _began = world.Clock.GetTimestamp();
        Directory.CreateDirectory(_folder);
        AttemptMarks.Write(AttemptMarks.Began(home, send.Run, send.Invocation, send.Delivery), send.Delivery, world.Clock.GetUtcNow());
        var mode = DeclarationMode.Of(home, send.Repository, send.Workspace);
        if (send.Accepted is null && mode == DeclarationMode.Restricted)
        {
            return (Unknown("no gates are accepted for this repository's verification, so nothing runs until the person accepts them"), false);
        }

        var (ran, busy) = await RunAsync(mode, ct).ConfigureAwait(false);
        if (ran is { Verdict: VerifyVerdicts.Failed } && _failed is { } failed && await LoadAsync(ran, failed, mode, ct).ConfigureAwait(false) is { } settled) ran = settled;
        var result = ran is null ? null : ran with { Judged = _at, Onto = _onto };
        return send.Accepted is null && result is not null
            ? (Told(result, $"nothing was accepted yet, so the work's own declaration ran ({DeclarationMode.Said(DeclarationMode.Full)})"), busy)
            : (result, busy);
    }

    private async Task<(VerifyResult? Result, bool Busy)> RunAsync(string mode, CancellationToken ct)
    {
        var under = await CheckDeclaration.OfAsync(
            send.Checkout, send.Commit, send.Accepted ?? IntegrationAcceptance.None, mode,
            each => CheckDeclaration.LineAsync(home, send.Repository, send.Checkout, send.Workspace, each),
            DeclarationUnits.Run(send.Gates, send.Reached || send.Base is not null), ct).ConfigureAwait(false);
        if (under.Problem is { } problem) return (Unknown(problem), false);
        if (under.Held is { } held) return (Unknown(held) with { Code = VerifyResult.Changed, Declaration = under.Work, Accepted = send.Accepted }, false);
        _ran = under.Blob ?? send.Accepted;
        var text = under.Text;
        _regenerate = TreePrepDeclared.Of(text).Regenerate;
        _reached = ReachedRunner.Of(text);
        _baseRef = send.Base;

        IReadOnlyList<DeclaredGate> declared = text is null
            ? [.. (world.Mapping(send.Repository) ?? []).Select(gate => new DeclaredGate(gate.Name, gate.Run, gate.WorkingDirectory, true))]
            : DeclaredGates.Read(text);
        if (send.Reached) return await HandbackAsync(mode, declared, text, ReachedRunner.Of(text)?.Outcome ?? ReachedRunner.DefaultOutcome, ct).ConfigureAwait(false);
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
        if (_merge is not null) return _merge;
        var merge = await LineMerge.ReadAsync(send.Checkout, line, send.Commit, ct).ConfigureAwait(false);
        _at = merge.At ?? send.Commit;
        _onto = merge.Onto;
        return _merge = merge;
    }

    private VerifyResult Back(string why) =>
        new(send.Invocation, send.Commit, VerifyVerdicts.Failed, why, [new VerifyGate(HandbackReading.Gate, RunnerVerdicts.Fail)]) { Failed = HandbackReading.Gate, Declaration = _ran };

    private static VerifyResult Told(VerifyResult result, string? words) =>
        words is null ? result : result with { Why = result.Why is { } why ? $"{why}\n{words}" : words };

    /// <summary>The <c>reached</c> choice: the hand-back read from git, a fault there the verdict and no gate run; else the required commands (<see cref="HandbackGates"/>) and the line's admission (<see cref="AdmissionGates"/>, the gates the merge tool is held to) the change reaches, each printing the outcome line (<paramref name="marker"/>) saying what it ran, replayed and left owed (<see cref="ReachedVerdict.Told"/>). No line, or git unable to say, is unrun and never failed.</summary>
    private async Task<(VerifyResult? Result, bool Busy)> HandbackAsync(string mode, IReadOnlyList<DeclaredGate> declared, string? text, string marker, CancellationToken ct)
    {
        if (await HandbackReading.LineAsync(send.Checkout, ct).ConfigureAwait(false) is not { } line)
        {
            return (Unknown("no line (the remote's head, `main` or `master`) is here to read the hand-back against") with { Code = VerifyResult.Unrun }, false);
        }

        if (await HandbackReading.ReadAsync(send.Checkout, line, send.Commit, ct).ConfigureAwait(false) is not { } read)
        {
            return (Unknown($"git could not read the hand-back at {TextBounds.ShortCommit(send.Commit, 7)} against `{line}`") with { Code = VerifyResult.Unrun }, false);
        }

        _baseRef = line;
        var seen = read with { Merge = await OntoAsync(line, ct).ConfigureAwait(false) };
        _onto ??= seen.Base;
        if (seen.Faulty) return (Back(seen.Words()), false);

        if (mode == DeclarationMode.Full && _at != send.Commit
            && await IntegrationAcceptance.BlobAtAsync(send.Checkout, _at, ct).ConfigureAwait(false) is { } merged && merged != IntegrationAcceptance.None
            && await IntegrationAcceptance.TextAsync(send.Checkout, merged, ct).ConfigureAwait(false) is { } mergedText)
        {
            text = mergedText;
            declared = DeclaredGates.Read(mergedText);
        }

        var files = new CheckFiles(home);
        var admission = AdmissionGates.Of(declared, text, seen.Touched);
        if (admission.Problem is { } unread) return (Unknown(unread) with { Code = VerifyResult.Unrun }, false);
        if (admission.Missing.Count > 0)
        {
            return (Unknown($"the declaration's admission names {string.Join(", ", admission.Missing.Select(name => $"`{name}`"))}, which it does not declare as a gate, so the line's admission would run short") with { Code = VerifyResult.Unrun }, false);
        }

        var required = declared.Where(gate => (gate.Handback && GateReach.Reaches(gate, seen.Touched)) || admission.Gates.Any(admitted => admitted.Name == gate.Name)).ToList();
        var verified = required.Where(gate => HandbackGates.Verified(files, gate, _at)).ToList();
        var earlier = await RecheckKeep.OfAsync(
            RecheckKeep.Before(home, send.Run, send.Invocation), _onto, _ran, [.. required.Except(verified).Select(HandbackGates.ForHandback)],
            async from => await MachinePorts.Repository.ChangedAsync(send.Checkout, from, _at, ct).ConfigureAwait(false),
            (paths, gate) => (gate.Handback && GateReach.Reaches(gate, paths)) || AdmissionGates.Of(declared, text, paths).Gates.Any(admitted => admitted.Name == gate.Name)).ConfigureAwait(false);
        var kept = verified.Select(gate => gate.Name).Concat(earlier.Select(each => each.Gate)).Select(name => new VerifyGate(name, VerifyGate.Kept)).ToList();
        var chosen = required.Where(gate => kept.All(each => each.Name != gate.Name)).Select(HandbackGates.ForHandback).ToList();
        var words = _onto is { } onto && RecheckKeep.Words(earlier, onto, line) is { } keptWords ? $"{seen.Words()}\n{keptWords}" : seen.Words();
        if (chosen.Count == 0) return (new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, words, kept) { Declaration = _ran }, false);
        var result = Told(await GatesAsync(chosen, kept, ct, marker).ConfigureAwait(false), words);
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
            _failed = new FailedRun(ReachedVerdict.Gate, folder, runner.Clock, log, runner.Outcome);
            if (exit != 0) _seen = LoadFailures.Read(home, world, place.Hold?.Id);
            return ReachedVerdict.Judge(send.Invocation, send.Commit, exit.Value, ReadText(log) ?? "", log, runner.Outcome, home) with { Declaration = _ran, WallSeconds = wall.Elapsed.TotalSeconds };
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

            var progress = GateProgress.Read(_folder, send.Commit, _onto, _ran);
            var carried = new List<string>();
            for (var at = 0; at < gates.Count; at++)
            {
                var gate = gates[at];
                if (progress.TryGetValue(gate.Name, out var passed) && passed.Command == VerifyGate.CommandOf(gate))
                {
                    ran.Add(passed);
                    carried.Add(gate.Name);
                    continue;
                }

                var log = Path.Combine(_folder, $"gate-{at + 1:D2}-{Slug(gate.Name)}.log");
                var folder = gate.Cwd is { } cwd ? Path.GetFullPath(Path.Combine(tree, cwd)) : tree;
                var index = at;
                keep(holder => holder with { Gates = [.. gates.Select(each => each.Name)], Now = new HeavyGateNow(gate.Name, index, world.Clock.GetUtcNow()) });
                var wall = Stopwatch.StartNew();
                var rest = gates.Skip(at + 1).Select(each => new VerifyGate(each.Name, RunnerVerdicts.NotRun));
                int? exit;
                try
                {
                    exit = Directory.Exists(folder) ? await StepAsync(place.Hold, gate.Run, folder, [gate.Name], new Dictionary<string, string>(), log, gate.Clock, ct).ConfigureAwait(false) : await MissingAsync(log, gate).ConfigureAwait(false);
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
                ran.Add(new VerifyGate(gate.Name, exit == 0 ? RunnerVerdicts.Pass : RunnerVerdicts.Fail, exit)
                {
                    Log = log, WallSeconds = wall.Elapsed.TotalSeconds, Command = VerifyGate.CommandOf(gate),
                });
                if (exit == 0)
                {
                    GateProgress.Write(_folder, send.Commit, _onto, _ran, ran);
                    continue;
                }

                keep(holder => holder with { Failed = new HeavyGateFailed(gate.Name, exit.Value) });
                _failed = marker is null ? null : new FailedRun(gate.Name, folder, gate.Clock, log, marker);
                if (marker is not null) _seen = LoadFailures.Read(home, world, place.Hold?.Id);
                var failed = new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Failed, $"the gate '{gate.Name}' failed with exit {exit}", [.. ran, .. rest, .. kept])
                {
                    Failed = gate.Name, Declaration = _ran,
                };
                return Told(Told(failed, Carried(carried)), ReadText(log) is not { } text ? null
                    : marker is not null && ReachedVerdict.Told(text, marker, log, home) is { } classes ? classes
                    : GateLogExcerpt.Told(text, log, home));
            }

            return Told(new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, null, [.. ran, .. kept]) { Declaration = _ran }, Carried(carried));
        }
        finally
        {
            await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], CancellationToken.None).ConfigureAwait(false);
        }
    }

    private string? Carried(IReadOnlyList<string> carried) => GateProgress.Words([.. carried.Where(name => name != _loadGate)]);

    /// <summary>A reached failure under load runs its red classes again alone at a quiet slot (<see cref="LoadFailures"/>); null where it is no load failure and stands.</summary>
    private async Task<VerifyResult?> LoadAsync(VerifyResult ran, FailedRun failed, string mode, CancellationToken ct)
    {
        if (_loadChecked || _seen is not { Loaded: true } seen || ReadText(failed.Log) is not { } text) return null;
        _loadChecked = true;
        if (LoadFailures.Red(text, failed.Marker) is not { } names) return null;
        if (_reached is null || _baseRef is null || _reached.RerunFor(names, _baseRef) is not { } command) return null;

        world.Said.WriteLine($"verify: {string.Join(", ", names)} failed while the machine was loaded ({seen.Words}); they run again alone at a quiet slot before the result counts.");
        var tree = send.Tree ?? Path.Combine(VerifyFiles.LinesFolder(home), "trees", slot);
        AdmissionHold hold;
        try
        {
            hold = await MachineAdmission.TakeAsync(
                home, new AdmissionRequest($"verification of {send.Repository} at {TextBounds.ShortCommit(send.Commit, 7)} {LoadFailures.Mark}") { Repository = send.Repository, Tree = tree },
                world.Said, ct, world.Admission, world.Capacity, world.Variable).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Ended(ran.Gates);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Unknown($"load: the machine's admission could not be kept ({error.Message}), so {string.Join(", ", names)} could not be run again alone; a load failure counts no round, so the check is asked again") with { Code = VerifyResult.Load };
        }

        using (hold)
        {
            var (quiet, stop) = await QuietAsync(hold, names, seen, ran, LoadFailures.Timeouts(text, failed.Marker) is not null, ct).ConfigureAwait(false);
            if (quiet is null) return stop;

            var log = Path.Combine(_folder, "load.log");
            var wall = Stopwatch.StartNew();
            int? exit;
            try
            {
                await TreeAtAsync(tree, ct).ConfigureAwait(false);
                keep(holder => holder with { Gates = [failed.Gate], Now = new HeavyGateNow(failed.Gate, 0, world.Clock.GetUtcNow()) });
                exit = await StepAsync(hold, command, failed.Folder, [failed.Gate], new Dictionary<string, string>(), log, failed.Clock, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return Unknown($"the classes that failed under load would not run again alone: {error.Message}") with { Code = VerifyResult.Unrun };
            }
            finally
            {
                await WorkingTree.GitAsync(send.Checkout, ["worktree", "unlock", tree], CancellationToken.None).ConfigureAwait(false);
            }

            if (exit is null) return Ended(ran.Gates);
            var again = ReadText(log) ?? "";
            if (exit != 0)
            {
                var red = ReachedVerdict.Judge(send.Invocation, send.Commit, exit.Value, again, log, failed.Marker, home);
                if (red.Verdict == VerifyVerdicts.Unknown) return Unknown($"the classes that failed under load could not be run again alone: {red.Why}") with { Code = VerifyResult.Unrun };
                if (LoadFailures.Timeouts(again, failed.Marker) is not null && LoadFailures.Read(home, world, hold.Id) is { Loaded: true } now)
                {
                    return Unknown(LoadFailures.Again(names, seen, now)) with { Code = VerifyResult.Load };
                }

                return Told(ran, $"{LoadFailures.Failed(names, seen)}\n{red.Why}") with { Red = ran.Red is null ? null : red.Red ?? ran.Red };
            }

            if (ReachedVerdict.RanOf(again, failed.Marker) == 0) return Unknown(LoadFailures.Empty(names, seen)) with { Code = VerifyResult.Load };
            var said = LoadFailures.Passed(names, seen, quiet) + (ReachedVerdict.Told(again, failed.Marker) is { } classes ? $"\n{classes}" : "");
            world.Said.WriteLine($"verify: {said}");
            var passed = new VerifyGate(failed.Gate, RunnerVerdicts.Pass, 0)
            {
                Log = log, WallSeconds = wall.Elapsed.TotalSeconds, Command = ran.Gates.FirstOrDefault(each => each.Name == failed.Gate)?.Command,
            };
            if (!send.Reached) return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Reached, said, [passed]) { Declaration = _ran };

            hold.Dispose();
            GateProgress.Write(_folder, send.Commit, _onto, _ran, [.. ran.Gates.Select(each => each.Name == failed.Gate ? passed : each)]);
            _loadGate = failed.Gate;
            var (rest, _) = await RunAsync(mode, ct).ConfigureAwait(false);
            return rest is null ? null : Told(rest, said);
        }
    }

    /// <summary>Waits, holding <paramref name="hold"/>, for a quiet slot: the load then, or the result to answer with.</summary>
    private async Task<(LoadReading? Quiet, VerifyResult? Stop)> QuietAsync(AdmissionHold hold, IReadOnlyList<string> names, LoadReading seen, VerifyResult ran, bool timedOut, CancellationToken ct)
    {
        var left = send.Bound - world.Clock.GetElapsedTime(_began);
        var allowed = left <= TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromTicks(Math.Min(world.QuietWait.Ticks, left.Ticks / 2));
        var started = world.Clock.GetTimestamp();
        while (true)
        {
            var look = MachineAdmission.Look(home, world.Admission.Alive, world.Admission.Clock(), world.Capacity);
            var now = world.Admission.Clock();
            var mine = look.Holds.FirstOrDefault(each => each.Id == hold.Id) ?? new AdmissionEntry(hold.Id, "session", Environment.ProcessId, "", now, now, null, null, null);
            var commit = world.Memory()?.Commit;
            var need = CheckAdmission.QuietNeed(home);
            if (LoadFailures.Quiet(look, mine, commit, need)) return (new LoadReading(look.Holds.Count(each => each.Id != hold.Id), commit, need), null);

            var waited = world.Clock.GetElapsedTime(started);
            if (waited >= allowed)
            {
                if (!timedOut)
                {
                    var work = LoadFailures.Unrerun(names, seen, waited);
                    world.Said.WriteLine($"verify: {work}");
                    return (null, Told(ran, work));
                }

                var words = LoadFailures.Waited(names, seen, waited);
                world.Said.WriteLine($"verify: {words}");
                return (null, Unknown(words) with { Code = VerifyResult.Load });
            }

            try
            {
                await Task.Delay(world.Poll, world.Clock, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return (null, Ended(ran.Gates));
            }
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

    private async Task<VerifyResult?> UnregeneratedAsync(string tree, CheckPlace place, IReadOnlyList<VerifyGate> pending, CancellationToken ct)
    {
        VerifyResult? unbuilt = null;
        var unfinished = await UnfinishedAsync(async () =>
        {
            if (await TreePreparation.RegenerateAsync(home, send.Repository, _regenerate, tree, ct, place.Hold?.Id).ConfigureAwait(false) is not { } failed) return null;
            var log = KeptRegenerate(failed.Ran.Output);
            if (UnbuiltTrees.Errors(failed.Ran.Output, tree) is not { Count: > 0 } errors)
            {
                return $"`{failed.Command}` exited {failed.Ran.Code}" + (GateLogExcerpt.Told(failed.Ran.Output, log, home) is { } told ? $"\n{told}" : "");
            }

            unbuilt = Unbuilt(failed, errors, log, pending);
            return null;
        }, pending, ct).ConfigureAwait(false);
        return unbuilt ?? unfinished;
    }

    private VerifyResult Unbuilt(RegenerateFailed failed, IReadOnlyList<string> errors, string? log, IReadOnlyList<VerifyGate> pending)
    {
        var head = $"the work does not build{(send.AsMerged ? " with the line" : "")}: `{failed.Command}` exited {failed.Ran.Code} preparing the verification tree";
        world.Said.WriteLine($"verify: {head}");
        var why = $"{head}, its compiler saying:\n{string.Join('\n', errors)}"
            + (log is null ? "" : $"\nits log, {Path.GetRelativePath(home, log).Replace('\\', '/')}, keeps the rest");
        return new VerifyResult(send.Invocation, send.Commit, VerifyVerdicts.Failed, why,
            [new VerifyGate(UnbuiltTrees.Gate, RunnerVerdicts.Fail, failed.Ran.Code) { Log = log, Command = failed.Command }, .. pending])
        {
            Failed = UnbuiltTrees.Gate, Declaration = _ran,
        };
    }

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

    private string? KeptRegenerate(string output)
    {
        var log = Path.Combine(_folder, "regenerate.log");
        try
        {
            AtomicFile.WriteText(log, output);
            return log;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
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
            step = await HeavyStep.RetriedAsync(
                () => HeavyStep.Start(job.Job, run, folder, new Dictionary<string, string>(), new StepOptions(environment, log) { BelowNormal = !clock }),
                TimeSpan.FromMilliseconds(100), world.Clock, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            await KeepLogAsync(log, $"error: it would not start: {error.Message}\n").ConfigureAwait(false);
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

    private async Task<int?> MissingAsync(string log, DeclaredGate gate)
    {
        var why = $"`{gate.Name}`'s cwd, {gate.Cwd}, is no folder of the verification tree";
        await KeepLogAsync(log, $"error: {why}\n").ConfigureAwait(false);
        throw new DriverException(why);
    }

    private async Task KeepLogAsync(string log, string text)
    {
        if (await HeavyStep.WriteLogAsync(log, text, world.Clock).ConfigureAwait(false)) return;
        world.Said.WriteLine($"verify: the log {log} was held by another process, so Daoris's words in it ({text.Trim()}) could not be written; the check's answer carries them.");
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
