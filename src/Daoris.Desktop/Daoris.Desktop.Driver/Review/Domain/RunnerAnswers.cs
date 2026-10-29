using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The verdicts D192's runner contract gives a gate (<c>pass</c>, <c>flake</c>, <c>fail</c>, <c>ended</c>, <c>not-run</c>).</summary>
internal static class RunnerVerdicts
{
    internal const string Pass = "pass";
    internal const string Flake = "flake";
    internal const string Fail = "fail";
    internal const string Ended = "ended";
    internal const string NotRun = "not-run";

    internal static readonly IReadOnlyList<string> All = [Pass, Flake, Fail, Ended, NotRun];
}

/// <summary>The standings a plan may state for a gate it keeps: the pass stands on this exact tree, on the records, because no path reaches it, or because every class of a half is kept.</summary>
internal static class RunnerStandings
{
    internal static readonly IReadOnlyList<string> All = ["exact", "records", "unreached", "composed"];
}

/// <summary>What a call to the runner asked, to judge its answer by: the tip, the declared gates of the accepted declaration, and, for a mode, the base and the accepted blob.</summary>
/// <param name="Commit">The candidate's tip, a full object id, compared exactly.</param>
/// <param name="Declared">The gates the accepted declaration declares, by the names it writes.</param>
internal sealed record RunnerAsk(string Commit, IReadOnlyList<string> Declared)
{
    /// <summary>The mode asked (<c>run</c>, <c>plan</c>, <c>probe</c> or <c>commits</c>); null where none is, which is D192's schema 1.</summary>
    public string? Mode { get; init; }

    /// <summary>The candidate's base, which every schema-2 answer names.</summary>
    public string? Base { get; init; }

    /// <summary>The accepted declaration's blob id, which every schema-2 answer names.</summary>
    public string? Declaration { get; init; }

    /// <summary>The gates a <c>run</c> or <c>plan</c> was narrowed to by <c>DAORIS_VERIFY_GATES</c>; null where the runner chose its own plan.</summary>
    public IReadOnlyList<string>? Gates { get; init; }
}

/// <summary>What a gate ran with: its units (null where it ran whole), the file where its settings were kept (omitted where they were not), and the digest a probe takes.</summary>
internal sealed record RunnerComposition(IReadOnlyList<string>? Units, string? Settings, string Digest);

/// <summary>One failure of a gate, as the runner signs it.</summary>
internal sealed record RunnerFailure(string Unit, string Test, FailureSignature Signature)
{
    /// <summary>The failure as the judge takes it: it names a test unless its signature is a gate's, signed by its step.</summary>
    public JudgedFailure ToJudged(string gate) => Signature.Gate is not null
        ? new JudgedFailure(FailureNames.Gate, gate, null, Signature)
        : new JudgedFailure(FailureNames.Test, gate, Test, Signature);
}

/// <summary>A gate as the runner answers it.</summary>
internal sealed record RunnerGate(string Name, string Verdict, int? Exit = null)
{
    public double? WallSeconds { get; init; }

    public string? Log { get; init; }

    public RunnerComposition? Composition { get; init; }

    public IReadOnlyList<RunnerFailure> Failures { get; init; } = [];
}

/// <summary>A gate in one of a plan's lists; a kept gate also says the commit and tree its pass stands on, and how it stands.</summary>
internal sealed record RunnerPlanEntry(string Name, string Why)
{
    public string? Commit { get; init; }

    public string? Tree { get; init; }

    public string? Standing { get; init; }
}

/// <summary>What an answer is.</summary>
internal enum RunnerKind
{
    /// <summary>The answer cannot be read as the runner's answer to this call. Never a pass.</summary>
    Unknown,

    /// <summary>The runner's own run holds its lock: the executor waits it out.</summary>
    Busy,

    /// <summary>D192's schema 1: the gates of the declaration, each with its verdict.</summary>
    Gates,

    Run,

    Plan,

    Probe,

    Commits,
}

/// <summary>A runner's answer, read. <see cref="RunnerKind.Unknown"/> says why and holds nothing else.</summary>
internal sealed record RunnerRead(RunnerKind Kind)
{
    public string? Why { get; init; }

    public IReadOnlyList<RunnerGate> Gates { get; init; } = [];

    /// <summary>A plan's gates to run.</summary>
    public IReadOnlyList<RunnerPlanEntry> Run { get; init; } = [];

    public IReadOnlyList<RunnerPlanEntry> Kept { get; init; } = [];

    public IReadOnlyList<RunnerPlanEntry> Deferred { get; init; } = [];

    public string? ProbeResult { get; init; }

    public FailureSignature? ProbeSignature { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];
}

internal enum RunnerStandingKind
{
    Reached,
    Failed,
    Unknown,
    Busy,
}

/// <summary>How a run stands by its gates alone (D192): reached, failed naming the gate, or unknown naming each gate missing, ended or not run.</summary>
internal sealed record RunnerStanding(RunnerStandingKind Kind)
{
    public string? Why { get; init; }

    /// <summary>The gates that passed only on a second run alone (a FLAKE1 re-run): read as a pass, and said.</summary>
    public IReadOnlyList<string> Flaked { get; init; } = [];
}

/// <summary>A failure of a gate, handed to the judge.</summary>
internal sealed record GateFailure(string Gate, RunnerFailure Failure)
{
    public string Test => Failure.Test;

    public JudgedFailure ToJudged() => Failure.ToJudged(Gate);
}

/// <summary>What promotion may do with a plan and its run: only a reached run promotes. Everything else is not a pass, and a failed one has failures for the judge.</summary>
internal sealed record RunnerPromotion(RunnerStandingKind Kind)
{
    public string? Why { get; init; }

    public IReadOnlyList<string> Flaked { get; init; } = [];

    /// <summary>The plan's kept gates, on the standing the runner states.</summary>
    public IReadOnlyList<RunnerPlanEntry> Kept { get; init; } = [];

    public IReadOnlyList<GateFailure> Failures { get; init; } = [];

    public bool Promotes => Kind == RunnerStandingKind.Reached;
}

/// <summary>
/// A repository's runner answer, read (INTEGRATE1f part f1; the integration design §5.9; D192's runner contract, extended). The answer is
/// <b>unknown, never a pass</b>, where the file is missing, does not read, or names another schema; names another commit, another base, or
/// another declaration blob than the accepted one; names a gate the accepted declaration does not declare, or a plan names one; or a
/// <c>run</c> lacks a gate its plan's <c>run</c> named or holds one <c>ended</c> or <c>not-run</c>. Promotion consumes only a <c>run</c> for the
/// candidate's exact tip and base under the accepted blob, with every gate of the accepted plan <c>pass</c> or <c>flake</c> beside the plan's
/// <c>kept</c> gates on the standing the runner states. Busy is <c>busy: true</c> (exit 75 is the caller's to read).
/// </summary>
/// <remarks>
/// The merge tool writes these answers (<c>verifyGates</c>, <c>verifyRunGates</c>, <c>verifyPlanAnswer</c>, <c>verifyCommitsAnswer</c>,
/// <c>verifyUnfinished</c> in <c>tools/merge-branch.mjs</c>); the field names are the ones it writes, and
/// <c>fixtures/runner-answers.json</c> holds both sides to them (<c>RunnerAnswerTests</c>, <c>integration-runner-answers.test.ts</c>).
/// An answer that carries <c>unfinished</c> is the runner saying it did not finish: unknown in its own words.
/// </remarks>
internal static class RunnerAnswers
{
    private static readonly Regex Digest = new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    private static RunnerRead NotKnown(string why) => new(RunnerKind.Unknown) { Why = why };

    private static RunnerRead Misshapen(string what) => NotKnown($"the runner's answer is misshapen: {what}");

    private static RunnerRead Undeclared(string name) => NotKnown($"the runner's answer names the gate '{name}', which the accepted declaration does not declare");

    private static string Short(string id) => id.Length > 7 ? id[..7] : id;

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Present(JsonElement element, string name, out JsonElement value) =>
        element.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;

    /// <summary>The answer in <paramref name="text"/> (null where the file is not there), judged against what the call asked.</summary>
    public static RunnerRead Read(string? text, RunnerAsk ask)
    {
        if (text is null) return NotKnown("the runner wrote no answer");
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return NotKnown("the runner's answer does not read as JSON");
        }

        using (document) return Read(document.RootElement, ask);
    }

    private static RunnerRead Read(JsonElement root, RunnerAsk ask)
    {
        if (root.ValueKind != JsonValueKind.Object) return NotKnown("the runner's answer is not a JSON object");
        var expected = ask.Mode is null ? 1 : 2;
        if (!Present(root, "schema", out var schema) || schema.ValueKind != JsonValueKind.Number) return NotKnown("the runner's answer names no schema");
        var named = schema.TryGetInt32(out var number) ? number.ToString(CultureInfo.InvariantCulture) : schema.GetRawText();
        if (named != expected.ToString(CultureInfo.InvariantCulture)) return NotKnown($"the runner's answer names schema {named}, not {expected}");
        if (Str(root, "commit") is not { } commit) return NotKnown("the runner's answer names no commit");
        if (commit != ask.Commit) return NotKnown($"the runner's answer is for commit {Short(commit)}, not {Short(ask.Commit)}");
        if (ask.Mode is not null)
        {
            if (Str(root, "mode") is not { } mode) return NotKnown("the runner's answer names no mode");
            if (mode != ask.Mode) return NotKnown($"the runner's answer is for the mode '{mode}', not '{ask.Mode}'");
            if (Str(root, "base") is not { } candidateBase) return NotKnown("the runner's answer names no base");
            if (ask.Base is not null && candidateBase != ask.Base) return NotKnown($"the runner's answer is for the base {Short(candidateBase)}, not {Short(ask.Base)}");
            if (Str(root, "declaration") is not { } declaration) return NotKnown("the runner's answer names no declaration");
            if (ask.Declaration is not null && declaration != ask.Declaration)
            {
                return NotKnown($"the runner's answer is for the declaration {Short(declaration)}, not the accepted {Short(ask.Declaration)}");
            }
        }

        if (root.TryGetProperty("busy", out var busy) && busy.ValueKind == JsonValueKind.True) return new RunnerRead(RunnerKind.Busy);
        if (Str(root, "unfinished") is { Length: > 0 } unfinished) return NotKnown(unfinished);
        return ask.Mode switch
        {
            null => ReadGates(root, ask, RunnerKind.Gates),
            "run" => ReadRun(root, ask),
            "plan" => ReadPlan(root, ask),
            "probe" => ReadProbe(root),
            "commits" => ReadCommits(root),
            _ => NotKnown($"the mode '{ask.Mode}' is none the runner answers"),
        };
    }

    private static RunnerRead ReadGates(JsonElement root, RunnerAsk ask, RunnerKind kind)
    {
        if (!root.TryGetProperty("gates", out var list) || list.ValueKind != JsonValueKind.Array) return Misshapen("gates is not a list of gates");
        var gates = new List<RunnerGate>();
        foreach (var each in list.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object) return Misshapen("a gate is not an object");
            if (Str(each, "name") is not { Length: > 0 } name) return Misshapen("a gate has no name");
            if (!ask.Declared.Contains(name)) return Undeclared(name);
            if (gates.Any(known => known.Name == name)) return Misshapen($"the gate '{name}' is named twice");
            if (Str(each, "verdict") is not { } verdict || !RunnerVerdicts.All.Contains(verdict))
            {
                return Misshapen($"the gate '{name}' has a verdict that is none of pass, flake, fail, ended or not-run");
            }

            int? exit = null;
            double? wall = null;
            if (Present(each, "exit", out var code))
            {
                if (!code.TryGetInt32(out var parsed)) return Misshapen($"the gate '{name}' has an exit that is not a number");
                exit = parsed;
            }

            if (Present(each, "wallSeconds", out var seconds))
            {
                if (seconds.ValueKind != JsonValueKind.Number) return Misshapen($"the gate '{name}' has a wall time that is not a number");
                wall = seconds.GetDouble();
            }

            RunnerComposition? composition = null;
            IReadOnlyList<RunnerFailure> failures = [];
            if (kind == RunnerKind.Run)
            {
                if (Present(each, "composition", out var kept))
                {
                    if (ReadComposition(kept, name) is not { } read) return Misshapen($"the composition of the gate '{name}' holds no digest (sha256: and 64 hex digits)");
                    composition = read;
                }

                var (read2, problem) = ReadFailures(each, name);
                if (problem is not null) return Misshapen(problem);
                failures = read2;
            }

            gates.Add(new RunnerGate(name, verdict, exit) { WallSeconds = wall, Log = Str(each, "log"), Composition = composition, Failures = failures });
        }

        return new RunnerRead(kind) { Gates = gates };
    }

    private static RunnerComposition? ReadComposition(JsonElement composition, string gate)
    {
        _ = gate;
        if (composition.ValueKind != JsonValueKind.Object || Str(composition, "digest") is not { } digest || !Digest.IsMatch(digest)) return null;
        IReadOnlyList<string>? units = null;
        if (Present(composition, "units", out var list))
        {
            if (list.ValueKind != JsonValueKind.Array || list.EnumerateArray().Any(each => each.ValueKind != JsonValueKind.String)) return null;
            units = [.. list.EnumerateArray().Select(each => each.GetString()!)];
        }

        return new RunnerComposition(units, Str(composition, "settings"), digest);
    }

    private static (IReadOnlyList<RunnerFailure> Failures, string? Problem) ReadFailures(JsonElement gate, string name)
    {
        if (!Present(gate, "failures", out var list)) return ([], null);
        if (list.ValueKind != JsonValueKind.Array) return ([], $"the failures of the gate '{name}' are not a list");
        var failures = new List<RunnerFailure>();
        foreach (var each in list.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Str(each, "unit") is not { } unit || Str(each, "test") is not { } test) return ([], $"a failure of the gate '{name}' names no unit and test");
            if (!each.TryGetProperty("signature", out var signature) || FailureSignature.FromJson(signature) is not { } read) return ([], $"a failure of the gate '{name}' holds no signature that reads");
            failures.Add(new RunnerFailure(unit, test, read));
        }

        return (failures, null);
    }

    private static RunnerRead ReadRun(JsonElement root, RunnerAsk ask)
    {
        var run = ReadGates(root, ask, RunnerKind.Run);
        if (run.Kind == RunnerKind.Unknown) return run;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = Entries(root, "kept", ask, seen, required: false, out var keptProblem);
        if (keptProblem is not null) return keptProblem;
        var deferred = Entries(root, "deferred", ask, seen, required: false, out var deferredProblem);
        if (deferredProblem is not null) return deferredProblem;
        return run with { Kept = kept, Deferred = deferred };
    }

    private static List<RunnerPlanEntry> Entries(JsonElement root, string key, RunnerAsk ask, HashSet<string> seen, bool required, out RunnerRead? problem)
    {
        problem = null;
        var entries = new List<RunnerPlanEntry>();
        if (!root.TryGetProperty(key, out var list) || list.ValueKind == JsonValueKind.Null)
        {
            if (required) problem = Misshapen("the plan's run, kept and deferred are lists of gates");
            return entries;
        }

        if (list.ValueKind != JsonValueKind.Array)
        {
            problem = Misshapen("the plan's run, kept and deferred are lists of gates");
            return entries;
        }

        foreach (var each in list.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Str(each, "name") is not { Length: > 0 } name)
            {
                problem = Misshapen($"an entry of the plan's {key} has no name");
                return entries;
            }

            if (!ask.Declared.Contains(name))
            {
                problem = Undeclared(name);
                return entries;
            }

            if (!seen.Add(name))
            {
                problem = Misshapen($"the plan lists the gate '{name}' twice");
                return entries;
            }

            entries.Add(new RunnerPlanEntry(name, Str(each, "why") ?? "") { Commit = Str(each, "commit"), Tree = Str(each, "tree"), Standing = Str(each, "standing") });
        }

        return entries;
    }

    private static RunnerRead ReadPlan(JsonElement root, RunnerAsk ask)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var run = Entries(root, "run", ask, seen, required: true, out var runProblem);
        if (runProblem is not null) return runProblem;
        var kept = Entries(root, "kept", ask, seen, required: true, out var keptProblem);
        if (keptProblem is not null) return keptProblem;
        var deferred = Entries(root, "deferred", ask, seen, required: true, out var deferredProblem);
        if (deferredProblem is not null) return deferredProblem;
        if (ask.Gates is { } named)
        {
            if (!run.Select(each => each.Name).ToHashSet(StringComparer.Ordinal).SetEquals(named) || kept.Count > 0 || deferred.Count > 0)
            {
                return Misshapen($"the plan does not run exactly the gates named ({string.Join(", ", named)})");
            }
        }
        else if (ask.Declared.FirstOrDefault(name => !seen.Contains(name)) is { } missing)
        {
            return Misshapen($"the plan lists no place for the gate '{missing}'");
        }

        return new RunnerRead(RunnerKind.Plan) { Run = run, Kept = kept, Deferred = deferred };
    }

    private static RunnerRead ReadProbe(JsonElement root)
    {
        if (Str(root, "result") is not ("pass" or "fail" or "cannot") || Str(root, "result") is not { } result) return Misshapen("the probe's result is none of pass, fail or cannot");
        FailureSignature? signature = null;
        if (Present(root, "signature", out var given)) signature = FailureSignature.FromJson(given);
        if (result == "fail" && signature is null) return Misshapen("a probe that failed holds no signature that reads");
        if (Present(root, "signature", out _) && signature is null) return Misshapen("the probe's signature does not read");
        return new RunnerRead(RunnerKind.Probe) { ProbeResult = result, ProbeSignature = result == "pass" ? null : signature };
    }

    private static RunnerRead ReadCommits(JsonElement root)
    {
        if (!root.TryGetProperty("problems", out var list) || list.ValueKind != JsonValueKind.Array || list.EnumerateArray().Any(each => each.ValueKind != JsonValueKind.String))
        {
            return Misshapen("problems is not a list of texts");
        }

        return new RunnerRead(RunnerKind.Commits) { Problems = [.. list.EnumerateArray().Select(each => each.GetString()!)] };
    }

    /// <summary>
    /// How a run stands by its gates alone, as D192 judges it: <b>failed</b> where any gate's verdict is <c>fail</c>, naming it with its exit;
    /// else <b>reached</b> where every gate <paramref name="required"/> is there as <c>pass</c> or <c>flake</c>; else <b>unknown</b>, naming
    /// each gate missing, ended or not run. An empty list of gates is never a pass, and a check that names no gate shows nothing.
    /// </summary>
    public static RunnerStanding Standing(IReadOnlyList<RunnerGate> gates, IReadOnlyList<string> required)
    {
        if (gates.FirstOrDefault(gate => gate.Verdict == RunnerVerdicts.Fail) is { } failed)
        {
            return new RunnerStanding(RunnerStandingKind.Failed)
            {
                Why = $"the gate '{failed.Name}' failed{(failed.Exit is { } exit ? $" with exit {exit}" : "")}",
                Flaked = Flaked(gates, required),
            };
        }

        if (required.Count == 0) return new RunnerStanding(RunnerStandingKind.Unknown) { Why = "the check names no gate: nothing is shown" };
        var lacking = new List<string>();
        foreach (var name in required)
        {
            var gate = gates.FirstOrDefault(each => each.Name == name);
            if (gate is null) lacking.Add($"{name} is missing");
            else if (gate.Verdict == RunnerVerdicts.Ended) lacking.Add($"{name} ended before its verdict");
            else if (gate.Verdict == RunnerVerdicts.NotRun) lacking.Add($"{name} did not run");
        }

        if (lacking.Count > 0) return new RunnerStanding(RunnerStandingKind.Unknown) { Why = $"the run does not show every gate it was asked for: {string.Join(", ", lacking)}" };
        return new RunnerStanding(RunnerStandingKind.Reached) { Flaked = Flaked(gates, required) };
    }

    private static IReadOnlyList<string> Flaked(IReadOnlyList<RunnerGate> gates, IReadOnlyList<string> required) =>
        [.. required.Where(name => gates.Any(gate => gate.Name == name && gate.Verdict == RunnerVerdicts.Flake))];

    private static string Called(RunnerKind kind) => kind == RunnerKind.Gates ? "schema 1" : kind.ToString().ToLowerInvariant();

    /// <summary>
    /// What promotion may do: it consumes only a <c>run</c> answer, for the candidate's exact tip and base under the accepted blob (the reads
    /// were made against what the call asked, so another commit, base or blob is already unknown), that holds every gate of the accepted plan's
    /// <c>run</c> as <c>pass</c> or <c>flake</c>, beside the plan's <c>kept</c> gates, each of which must say the tree its pass stands on and a
    /// standing the runner states. Anything unknown or busy is not a pass; a failed run hands its failures to the judge.
    /// </summary>
    public static RunnerPromotion Promotion(RunnerRead plan, RunnerRead run)
    {
        RunnerPromotion Not(RunnerStandingKind kind, string? why = null) => new(kind) { Why = why };
        if (plan.Kind == RunnerKind.Unknown) return Not(RunnerStandingKind.Unknown, plan.Why);
        if (run.Kind == RunnerKind.Unknown) return Not(RunnerStandingKind.Unknown, run.Why);
        if (plan.Kind == RunnerKind.Busy || run.Kind == RunnerKind.Busy) return Not(RunnerStandingKind.Busy);
        if (plan.Kind != RunnerKind.Plan) return Not(RunnerStandingKind.Unknown, $"promotion consumes the runner's plan, and the answer is a {Called(plan.Kind)}'s");
        if (run.Kind != RunnerKind.Run) return Not(RunnerStandingKind.Unknown, $"promotion consumes a run answer for the exact tip and base, and the answer is a {Called(run.Kind)}'s");
        foreach (var kept in plan.Kept)
        {
            if (string.IsNullOrEmpty(kept.Tree)) return Not(RunnerStandingKind.Unknown, $"the plan keeps the gate '{kept.Name}' on no tree");
            if (kept.Standing is null || !RunnerStandings.All.Contains(kept.Standing))
            {
                return Not(RunnerStandingKind.Unknown, $"the plan keeps the gate '{kept.Name}' on a standing the runner does not state ('{kept.Standing}')");
            }
        }

        var required = plan.Run.Select(each => each.Name).ToList();
        if (required.Count == 0 && plan.Kept.Count == 0) return Not(RunnerStandingKind.Unknown, "the plan runs no gate and keeps none: nothing is shown");
        var standing = required.Count == 0
            ? run.Gates.FirstOrDefault(gate => gate.Verdict == RunnerVerdicts.Fail) is null
                ? new RunnerStanding(RunnerStandingKind.Reached)
                : Standing(run.Gates, required)
            : Standing(run.Gates, required);
        var failures = standing.Kind == RunnerStandingKind.Failed
            ? [.. run.Gates.Where(gate => gate.Verdict == RunnerVerdicts.Fail).SelectMany(gate => gate.Failures.Select(failure => new GateFailure(gate.Name, failure)))]
            : (IReadOnlyList<GateFailure>)[];
        return new RunnerPromotion(standing.Kind) { Why = standing.Why, Flaked = standing.Flaked, Kept = plan.Kept, Failures = failures };
    }
}
