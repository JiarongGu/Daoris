using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A candidate's gates run and judged, the way the merge tool does (INTEGRATE1f part f2; the integration design §4.5, §5.2 steps 6 and 7,
/// §5.3, §5.9): the candidate's runs start in one job, closed before any place in the machine's admission is let go; the gates run at the
/// tip through the repository's declared runner or, with none, each whole; a red run's failures are judged by <see cref="FailureJudge"/>
/// (a known red, the confirm run holding the whole admission, the base, the prefixes) and the candidate's action is
/// <see cref="CandidateJudgement"/>'s. Nothing here promotes: the answer says what the step may do next, and a sighting is never a pass.
/// </summary>
/// <remarks>
/// <para><b>One confirm run per failing gate per candidate</b>, before any base or prefix run, answering for all the gate's failures. It
/// repeats each of its failures (the first <see cref="IntegrationWorld.MaxRepeats"/>) through the runner's <c>probe</c> as the failing run
/// composed it, under one whole admission; a gate that kept no composition is run whole. A composition the runner cannot repeat (its digest
/// differs at the commit) is a refusal, which reads as an inconclusive confirm run, not a run. A drop ends it with the job.</para>
/// <para>The judge's functions are plain, not awaited (a <c>Domain</c> holds nothing <c>async</c>); this runs them on a worker and waits for
/// each run there.</para>
/// </remarks>
internal sealed class IntegrationJudging(IntegrationVerdictAsk ask, IntegrationWorld world)
{
    private static readonly Regex ObjectId = new("^(?:[0-9a-f]{40}|[0-9a-f]{64})$", RegexOptions.CultureInvariant);

    private readonly object _gate = new();
    private readonly List<VerdictRun> _runs = [];
    private readonly Dictionary<string, IReadOnlyList<(JudgedFailure Failure, ConfirmReading Confirm)>> _readings = new(StringComparer.Ordinal);
    private IReadOnlyList<string> _wholeIds = [];
    private CandidateJob _job = null!;
    private IGateRuns _gates = null!;
    private GatesRun _tipRun = null!;
    private string _tip = "";

    public async Task<IntegrationVerdict> RunAsync()
    {
        if (ask.Candidate.Tip is not { } tip || !ObjectId.IsMatch(tip)) return Said(VerdictActions.Unknown, "the candidate has no tip that is a full lower-case object id, so nothing was run");
        if (!ObjectId.IsMatch(ask.Base)) return Said(VerdictActions.Unknown, "the candidate's base is not a full lower-case object id, so nothing was run");
        if (ask.Members.Count == 0) return Said(VerdictActions.Unknown, "the candidate has no member, so nothing was run");
        _tip = tip;
        CandidateJob job;
        try
        {
            job = await CandidateJob.OpenAsync(ask.Candidate.Job).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            return Said(VerdictActions.Unknown, error.Message);
        }

        _job = job;
        try
        {
            Directory.CreateDirectory(ask.Scratch);
            var declaration = Path.Combine(ask.Scratch, "declaration.json");
            // The accepted blob's text byte for byte: the runner names the blob it read, and the answer must name the accepted one.
            await File.WriteAllBytesAsync(declaration, new UTF8Encoding(false).GetBytes(ask.Declaration)).ConfigureAwait(false);
            var places = new Places(ask, job, world);
            _gates = ask.Runner is { } runner
                ? new RunnerGateRuns(
                    ask, declaration, [.. DeclaredGates.Read(ask.Declaration).Select(gate => gate.Name)], new RunnerCaller(job, runner, world.Start, ask.Pass, ask.Scratch), places)
                : new DirectGateRuns(ask, job, places, world);
            await StandAsync(tip).ConfigureAwait(false);
            _tipRun = await _gates.RunAsync(tip, null, null).ConfigureAwait(false);
            lock (_gate) _runs.AddRange(_tipRun.Runs);
            var verdict = _tipRun.Kind switch
            {
                RunnerStandingKind.Reached => new IntegrationVerdict(VerdictActions.Green) { Flaked = _tipRun.Flaked, Runs = Runs(), Kept = Kept() },
                RunnerStandingKind.Busy => new IntegrationVerdict(VerdictActions.Busy) { Why = "the runner's own run holds its lock", Runs = Runs() },
                RunnerStandingKind.Failed => await Task.Run(JudgeFailures).ConfigureAwait(false),
                _ => new IntegrationVerdict(VerdictActions.Unknown) { Why = _tipRun.Why ?? "the gates' run cannot be read as a verdict of the tip", Runs = Runs() },
            };
            return verdict;
        }
        catch (OperationCanceledException) when (ask.Drop.IsCancellationRequested)
        {
            return new IntegrationVerdict(VerdictActions.Dropped) { Why = "the candidate was dropped; its job was closed and every place let go", Runs = Runs() };
        }
        catch (DriverException error)
        {
            return new IntegrationVerdict(VerdictActions.Unknown) { Why = error.Message, Runs = Runs() };
        }
        finally
        {
            // The job closes while no place is held (each call let its own go after ending what its job held), and the tree goes back to the tip.
            await job.DisposeAsync().ConfigureAwait(false);
            await StandBackAsync().ConfigureAwait(false);
        }
    }

    private static IntegrationVerdict Said(string action, string why) => new(action) { Why = why };

    private IReadOnlyList<VerdictRun> Runs()
    {
        lock (_gate) return [.. _runs];
    }

    // ------------------------------------------------------------------------------------------------ the tree

    /// <summary>The integration tree standing at <paramref name="commit"/>, which a probe at a prefix and the runner's own <c>TREE</c> check both need.</summary>
    private async Task StandAsync(string commit, CancellationToken? ct = null)
    {
        var token = ct ?? ask.Drop;
        var (code, head, _) = await WorkingTree.GitAsync(ask.Tree, ["rev-parse", "HEAD"], token).ConfigureAwait(false);
        if (code == 0 && head.Trim() == commit) return;
        var (moved, _, error) = await WorkingTree.GitAsync(ask.Tree, ["checkout", "--detach", "--force", "--quiet", commit], token).ConfigureAwait(false);
        if (moved != 0) throw new DriverException($"the integration tree would not stand at {commit[..7]} ({error.Trim()}), so the run there was not made.");
    }

    /// <summary>The tree back at the tip once the judgement is over, a drop included: the steps that follow find it where they left it.</summary>
    private async Task StandBackAsync()
    {
        try
        {
            await StandAsync(_tip, CancellationToken.None).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            // The next step reads the tree before it uses it.
        }
    }

    // ------------------------------------------------------------------------------------------------ the judgement

    private IntegrationVerdict JudgeFailures()
    {
        var pairs = _tipRun.Failures
            .Select(failure => (Gate: failure, Judged: failure.ToJudged()))
            .DistinctBy(pair => (pair.Judged.Gate, pair.Judged.Signature.Key))
            .ToList();
        var byJudged = pairs.ToDictionary(pair => pair.Judged, pair => pair.Gate);
        var judgements = new List<JudgedEntry>();
        foreach (var (_, judged) in pairs) judgements.Add(new JudgedEntry(judged, FailureJudge.JudgeFailure(judged, Context(judged, byJudged, () => ReadingFor(judged)))));

        // A failure of a confirm run's own, whose signature is none of the gate's: the candidate's, confirmed, since it failed with nothing heavy beside it.
        foreach (var (failure, confirm) in _readings.Values.SelectMany(each => each)
                     .Where(pair => pairs.All(known => known.Judged.Gate != pair.Failure.Gate || known.Judged.Signature.Key != pair.Failure.Signature.Key)).ToList())
        {
            var fixedReading = confirm;
            var own = new RunnerFailure(failure.Test ?? "", failure.Test ?? failure.Gate, failure.Signature);
            var extra = new Dictionary<JudgedFailure, GateFailure>(byJudged) { [failure] = new GateFailure(failure.Gate, own) };
            judgements.Add(new JudgedEntry(failure, FailureJudge.JudgeFailure(failure, Context(failure, extra, () => fixedReading))));
        }

        if (ask.Drop.IsCancellationRequested) judgements.Add(new JudgedEntry(pairs[0].Judged, new Judged(JudgeOutcome.Dropped, [])));
        var members = ask.Members.Select(member => new QueueMember(member.Branch, member.Tip)).ToList();
        var outcome = CandidateJudgement.Of(judgements, members, ask.Unconfirmed);
        foreach (var sighting in outcome.Sightings) IntegrationMemory.Current.Sighted(ask.Candidate.Repository, _tip, sighting.Failure, sighting.Confirm);
        return Verdict(outcome, judgements);
    }

    private JudgeContext Context(JudgedFailure failure, IReadOnlyDictionary<JudgedFailure, GateFailure> gateFailures, Func<ConfirmReading?> confirm)
    {
        var members = ask.Members.Count;
        var asked = gateFailures[failure];
        PrefixOutcome Probe(string commit)
        {
            if (ask.Drop.IsCancellationRequested) throw new OperationCanceledException(ask.Drop);
            Wait(StandAsync(commit));
            var composition = _tipRun.Compositions.GetValueOrDefault(failure.Gate);
            var answer = Wait(_gates.ProbeAsync(asked, composition, commit, null));
            lock (_gate) _runs.Add(answer.Run ?? new VerdictRun("probe", failure.Gate, answer.Result, composition?.Digest) { Units = composition?.Units });
            return answer switch
            {
                { Result: "pass" } => PrefixOutcome.Passed,
                { Result: "fail", Signature: { } signature } => PrefixOutcome.Failed(signature),
                { Result: "cannot", Signature: { } signature } => PrefixOutcome.Cannot(signature),
                // No answer that reads: it cannot run the selection, which leaves the failure unresolved, never placed.
                _ => new PrefixOutcome(PrefixResult.Cannot, null),
            };
        }

        string CommitOf(int prefix) => prefix == 0 ? ask.Base : prefix >= members ? _tip : ask.Members[prefix - 1].Tip;
        return new JudgeContext(members, (_, prefix) => Probe(CommitOf(prefix)))
        {
            Alone = ask.Members.Any(member => member.Alone is not null)
                ? (_, member) => ask.Members[member - 1].Alone is { } alone ? Probe(alone) : PrefixOutcome.Failed(failure.Signature)
                : null,
            Known = IntegrationMemory.Current.KnownRedFor(ask.Candidate.Repository, failure.Signature),
            BaseCommit = ask.Base,
            Confirm = confirm,
        };
    }

    /// <summary>A failure's reading by its gate's confirm run, which runs once per failing gate per candidate, the first time a failure of the gate reaches it.</summary>
    private ConfirmReading? ReadingFor(JudgedFailure failure)
    {
        if (!_readings.TryGetValue(failure.Gate, out var readings))
        {
            var failures = _tipRun.Failures.Where(each => each.Gate == failure.Gate).Select(each => each.ToJudged()).DistinctBy(each => each.Signature.Key).ToList();
            var answer = FailureJudge.ConfirmStep(TakeWhole, () => Rerun(failure.Gate), () => ask.Drop.IsCancellationRequested, $"the confirm run of {failure.Gate}");
            readings = FailureJudge.ConfirmReadings(answer, failures);
            _readings[failure.Gate] = readings;
        }

        return readings.Where(pair => pair.Failure.Signature.Key == failure.Signature.Key).Select(pair => pair.Confirm).FirstOrDefault();
    }

    private WholeTake TakeWhole(string what, Func<bool> halted)
    {
        var take = Wait(MachineAdmission.TakeWholeAsync(
            ask.Home, new AdmissionRequest(what) { Repository = ask.Candidate.Repository, Tree = ask.Tree }, world.Said, halted, ask.Drop, world.Waiting, world.Capacity, world.Variable));
        _wholeIds = take.Ids;
        return take;
    }

    /// <summary>The failing gate's run once more at the tip, as composed, inside the whole admission; every process it started is ended before the places go.</summary>
    private GateRerun? Rerun(string gate)
    {
        var hold = _wholeIds.Count > 0 ? _wholeIds[0] : null;
        try
        {
            Wait(StandAsync(_tip));
            var failures = _tipRun.Failures.Where(each => each.Gate == gate).DistinctBy(each => each.ToJudged().Signature.Key).ToList();
            var composition = _tipRun.Compositions.GetValueOrDefault(gate);
            var kept = composition is null ? null : new ConfirmComposition(composition.Units, composition.Digest);
            if (composition?.Settings is not { Length: > 0 })
            {
                // The runner keeps no composition for the gate: the gate whole, and every failure it has again is read from the one run.
                var whole = Wait(_gates.RunAsync(_tip, hold, [gate]));
                lock (_gate) _runs.AddRange(whole.Runs);
                return whole.Kind switch
                {
                    RunnerStandingKind.Reached => new GateRerun("pass") { Composition = kept },
                    RunnerStandingKind.Failed => new GateRerun("fail") { Composition = kept, Failures = [.. whole.Failures.Select(each => each.ToJudged()).DistinctBy(each => each.Signature.Key)] },
                    RunnerStandingKind.Busy => new GateRerun("unknown", "the runner's own run holds its lock") { Composition = kept },
                    _ => new GateRerun("unknown", whole.Why) { Composition = kept },
                };
            }

            var repeated = failures.Take(world.MaxRepeats).ToList();
            var again = new List<JudgedFailure?>();
            foreach (var failure in repeated)
            {
                var answer = Wait(_gates.ProbeAsync(failure, composition, _tip, hold));
                lock (_gate) _runs.Add(new VerdictRun("probe", gate, answer.Result, composition.Digest) { Units = composition.Units });
                switch (answer.Result)
                {
                    case "pass":
                        break;
                    case "fail" or "cannot":
                        again.Add(answer.Signature is { } signature ? Judged(gate, signature) : null);
                        break;
                    default:
                        return new GateRerun("unknown", answer.Why ?? $"the probe of {failure.Failure.Test} answered {answer.Result}") { Composition = kept };
                }
            }

            if (again.Count > 0) return new GateRerun("fail") { Composition = kept, Failures = again };
            if (failures.Count > repeated.Count)
            {
                return new GateRerun(
                    "partial", $"the confirm run repeated {repeated.Count} of the gate's {failures.Count} failures, so a pass of those shows nothing of the rest") { Composition = kept };
            }

            return new GateRerun("pass") { Composition = kept };
        }
        finally
        {
            // Its processes end while the whole admission is still held: the confirm run's end lets the places go after this returns.
            Wait(_job.EndLeftAsync());
        }
    }

    private static JudgedFailure Judged(string gate, FailureSignature signature) =>
        signature.Gate is not null
            ? new JudgedFailure(FailureNames.Gate, gate, null, signature)
            : new JudgedFailure(FailureNames.Test, gate, signature.Test, signature);

    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private static void Wait(Task task) => task.GetAwaiter().GetResult();

    // ------------------------------------------------------------------------------------------------ the answer

    private static VerdictFailure Named(JudgedFailure failure) => new(failure.Gate, failure.Test, failure.Label, failure.Signature.Key);

    private static VerdictConfirm? Named(ConfirmReading? reading) => reading is null
        ? null
        : new VerdictConfirm(reading.Result ?? ConfirmResults.Inconclusive, reading.Why)
        {
            Admission = reading.Evidence is { } evidence
                ? new VerdictAdmission(evidence.Conclusive, [.. evidence.Doubts.Select(doubt => $"{doubt.At}: {doubt.Why}")], evidence.WaitedMs, evidence.Ids ?? [])
                : null,
            Digest = reading.Composition?.Digest,
            Units = reading.Composition?.Units,
        };

    /// <summary>A failure judged, with the confirm run's reading of it where it had one: a failure that failed again was confirmed, and its evidence is kept with the placement it led to.</summary>
    private VerdictJudgement Named(JudgedEntry entry) => new(Named(entry.Failure), entry.Judged.Outcome.ToString().ToLowerInvariant())
    {
        Why = entry.Judged.Why,
        Said = entry.Judged.Said,
        Member = entry.Judged.Member,
        With = entry.Judged.With,
        Interacting = entry.Judged.Interacting,
        Confirm = Named(entry.Judged.Confirm ?? ReadingKept(entry.Failure)),
    };

    private ConfirmReading? ReadingKept(JudgedFailure failure) =>
        _readings.TryGetValue(failure.Gate, out var readings)
            ? readings.Where(pair => pair.Failure.Signature.Key == failure.Signature.Key).Select(pair => pair.Confirm).FirstOrDefault()
            : null;

    private IntegrationVerdict Verdict(CandidateOutcome outcome, IReadOnlyList<JudgedEntry> judgements) => new(outcome.Action.Word())
    {
        Why = outcome.Why,
        Said = outcome.Said,
        On = outcome.On,
        Failure = outcome.Failure is { } stop ? Named(stop) : null,
        PassedOnConfirm = outcome.PassedOnConfirm,
        Flaked = _tipRun.Flaked,
        Judgements = [.. judgements.Select(Named)],
        Inherited = [.. outcome.Inherited.Select(Named)],
        Flakes = [.. outcome.Flakes.Select(Named)],
        Sightings = [.. judgements.Where(each => each.Judged.Outcome == JudgeOutcome.Unconfirmed).Select(Named)],
        Inconclusive = [.. outcome.Inconclusive.Select(Named)],
        HandedBack = [.. outcome.HandedBack.Select(each => new VerdictHandedBack(each.Member.Branch, each.How, each.Failure, each.With, each.Interacting, each.Why))],
        Retain = [.. outcome.Retain.Select(member => member.Branch)],
        Runs = Runs(),
        Kept = Kept(),
    };

    private IReadOnlyList<VerdictKept> Kept() =>
        [.. _tipRun.Kept.Select(each => new VerdictKept(each.Name, each.Why, each.Commit, each.Tree, each.Standing))];
}
