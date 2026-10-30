using System.Text;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The gates' runs through the repository's declared runner (INTEGRATE1f part f2; the integration design §5.9): the plan first, then the
/// run of what the plan runs, then promotion's reading of the two; a failure repeated by <c>probe</c> as composed. Only the accepted blob
/// is the declaration (its text beside the run, in every mode); the tree is the candidate's, handed as <c>TREE</c>, standing at the commit.
/// </summary>
/// <remarks>
/// A <c>run</c> and a <c>probe</c> each take a place in the machine's admission (the runner is handed no home, so it takes none of its
/// own), unless the call is inside the confirm run's whole admission; a <c>plan</c> runs nothing and takes none. An answer that cannot be
/// read, a plan or run for another commit, base or blob, a gate the declaration does not declare: unknown, never a pass.
/// </remarks>
internal sealed class RunnerGateRuns(
    IntegrationVerdictAsk ask, string declarationFile, IReadOnlyList<string> declared, RunnerCaller caller, Places places) : IGateRuns
{
    public async Task<GatesRun> RunAsync(string commit, string? hold, IReadOnlyList<string>? only)
    {
        var named = only ?? ask.Gates;
        var plan = await caller.CallAsync(Ask("plan", commit) with { Gates = named }, hold, ask.Drop).ConfigureAwait(false);
        if (plan.Read.Kind == RunnerKind.Busy) return new GatesRun(RunnerStandingKind.Busy);
        if (plan.Read.Kind != RunnerKind.Plan) return new GatesRun(RunnerStandingKind.Unknown, plan.Read.Why ?? "the runner's plan cannot be read");
        var names = plan.Read.Run.Select(each => each.Name).ToList();
        var what = $"integration gates {(names.Count > 0 ? string.Join(", ", names.Select(name => $"`{name}`")) : "(kept)")} on {commit[..7]}";
        // A plan that runs nothing and keeps something is run with the runner's own plan: the answer is promotion's, the plan's kept gates beside it.
        var run = await places.PlacedAsync(
            what, hold, inside => caller.CallAsync(Ask("run", commit) with { Gates = names.Count > 0 ? names : null }, inside, ask.Drop)).ConfigureAwait(false);
        if (run.Read.Kind == RunnerKind.Busy) return new GatesRun(RunnerStandingKind.Busy);
        var promotion = RunnerAnswers.Promotion(plan.Read, run.Read);
        var runs = run.Read.Kind == RunnerKind.Run ? run.Read.Gates.Select(gate => Run(gate, run.Log)).ToList() : [];
        var compositions = run.Read.Kind == RunnerKind.Run
            ? run.Read.Gates.Where(gate => gate.Composition is not null).ToDictionary(gate => gate.Name, gate => gate.Composition!)
            : new Dictionary<string, RunnerComposition>();
        if (promotion.Kind == RunnerStandingKind.Failed)
        {
            // The failures are the judge's only where every other gate of the plan is shown: a gate that ended or did not run is unknown, and no
            // judgement of the failures may let it through.
            var lacking = names.Where(name => run.Read.Gates.FirstOrDefault(gate => gate.Name == name) is not { Verdict: "pass" or "flake" or "fail" }).ToList();
            if (lacking.Count > 0)
            {
                return new GatesRun(RunnerStandingKind.Unknown, $"the run fails a gate and does not show every gate it was asked for: {string.Join(", ", lacking)} ended, did not run or is missing")
                {
                    Runs = runs, Compositions = compositions,
                };
            }
        }

        return new GatesRun(promotion.Kind, promotion.Why)
        {
            Failures = Failures(promotion, run.Read, run.Log),
            Flaked = promotion.Flaked,
            Runs = runs,
            Kept = promotion.Kept,
            Compositions = compositions,
        };
    }

    public async Task<ProbeAnswer> ProbeAsync(GateFailure failure, RunnerComposition? composition, string commit, string? hold)
    {
        if (composition?.Settings is not { Length: > 0 } settings)
        {
            // The runner kept no composition for this gate: the gate whole, and its answer read as the probe's.
            var again = await RunAsync(commit, hold, [failure.Gate]).ConfigureAwait(false);
            var found = again.Failures.FirstOrDefault(each => FailureReader.Same(each.Failure.Signature, failure.Failure.Signature)) ?? again.Failures.FirstOrDefault();
            return again.Kind switch
            {
                RunnerStandingKind.Reached => new ProbeAnswer("pass"),
                RunnerStandingKind.Failed when found is not null => new ProbeAnswer("fail", found.Failure.Signature),
                RunnerStandingKind.Failed => new ProbeAnswer("unknown", Why: "the gate failed and gave no failure to compare"),
                RunnerStandingKind.Busy => new ProbeAnswer("busy"),
                _ => new ProbeAnswer("unknown", Why: again.Why),
            };
        }

        var file = Path.Combine(ask.Scratch, $"failure-{Guid.NewGuid():N}.json");
        var written = new JsonObject
        {
            ["gate"] = failure.Gate,
            ["unit"] = failure.Failure.Unit,
            ["test"] = failure.Failure.Test,
            ["signature"] = failure.Failure.Signature.ToJson(),
            ["composition"] = new JsonObject
            {
                ["units"] = composition.Units is null ? null : new JsonArray([.. composition.Units.Select(unit => (JsonNode?)JsonValue.Create(unit))]),
                ["settings"] = settings,
                ["digest"] = composition.Digest,
            },
        };
        Directory.CreateDirectory(ask.Scratch);
        File.WriteAllText(file, written.ToJsonString() + "\n", new UTF8Encoding(false));
        var what = $"integration probe of {failure.Failure.Test} ({failure.Gate}) on {commit[..7]}";
        var called = await places.PlacedAsync(
            what, hold, inside => caller.CallAsync(Ask("probe", commit) with { FailureFile = file }, inside, ask.Drop)).ConfigureAwait(false);
        return called.Read.Kind switch
        {
            RunnerKind.Busy => new ProbeAnswer("busy"),
            RunnerKind.Probe => new ProbeAnswer(called.Read.ProbeResult!, called.Read.ProbeSignature),
            _ => new ProbeAnswer("unknown", Why: called.Read.Why),
        };
    }

    private RunnerCallAsk Ask(string mode, string commit) =>
        new(mode, commit, ask.Base, declarationFile, ask.DeclarationId, ask.Tree, declared);

    private static VerdictRun Run(RunnerGate gate, string log) =>
        new("run", gate.Name, gate.Verdict, gate.Composition?.Digest) { Units = gate.Composition?.Units, Exit = gate.Exit, Log = gate.Log ?? log };

    /// <summary>
    /// The failures promotion hands the judge; a gate that failed and kept no failure is one that names no test, signed by its step from the
    /// runner's log, so a failed gate is never lost between the run and the judge.
    /// </summary>
    private static IReadOnlyList<GateFailure> Failures(RunnerPromotion promotion, RunnerRead run, string log)
    {
        if (promotion.Kind != RunnerStandingKind.Failed) return [];
        var failures = promotion.Failures.ToList();
        foreach (var gate in run.Gates.Where(gate => gate.Verdict == RunnerVerdicts.Fail && failures.All(each => each.Gate != gate.Name)))
        {
            var output = ReadLog(gate.Log ?? log);
            failures.Add(new GateFailure(gate.Name, new RunnerFailure("", gate.Name, FailureReader.GateSignature(gate.Name, "run", output))));
        }

        return failures;
    }

    /// <summary>The step's log, read sharing its writing: a process started elsewhere in this one during the step's start keeps a copy of its handle.</summary>
    private static string ReadLog(string log)
    {
        try
        {
            return File.Exists(log) ? BesideReads.Text(log) : "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }
}
