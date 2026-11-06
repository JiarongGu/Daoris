namespace Daoris.Driver;

/// <summary>
/// A verification run as its line's read draws it (FABRIC-PIPELINE1d; the pipeline design §4.4): what it judged, who asked it, and its verdict,
/// with each gate's evidence from its checks' newest results.
/// </summary>
/// <param name="Run">The run's id, <c>v-</c> and sixteen hex digits; its record is <c>run:&lt;run&gt;</c>.</param>
/// <param name="Commit">The full commit it judges.</param>
/// <param name="Level">Who chose it: <see cref="VerifyRuns.Person"/> for a person's verification of a commit or a branch.</param>
/// <param name="Verdict">One of <see cref="LineVerdicts"/>.</param>
public sealed record WorkflowRunLine(string Run, string Commit, string Level, DateTimeOffset At, string Verdict)
{
    /// <summary>The branch the person asked it of; null for a commit.</summary>
    public string? Branch { get; init; }

    /// <summary>The line a landed run's batch landed on; null for a person's run.</summary>
    public string? Line { get; init; }

    /// <summary>Where a landed run's batch begins: the tip of the line's newest run with a verdict; null for its first verification.</summary>
    public string? Base { get; init; }

    /// <summary>The gate a failed check named, where its verdict is <see cref="LineVerdicts.Failed"/>.</summary>
    public string? Gate { get; init; }

    /// <summary>Why it is not known, where its verdict is <see cref="LineVerdicts.Unknown"/>: <see cref="VerifyResult"/>'s code, or the attempt's.</summary>
    public string? Why { get; init; }

    /// <summary>Each gate its checks ran, in their order, from each check's newest result; none before a result is written.</summary>
    public IReadOnlyList<VerifyGate> Gates { get; init; } = [];
}

/// <summary>
/// A verification run's verdict, read from its checks' steps (the pipeline design §4.4's words): verified where every check reached, failed
/// where one failed, not now where the person stopped one, not known where one is unknown, running while one runs, else waiting.
/// </summary>
public static class LineVerdicts
{
    public const string Verified = "verified";
    public const string Failed = "failed";
    public const string NotNow = "not-now";
    public const string Unknown = "unknown";
    public const string Running = "running";
    public const string Waiting = "waiting";

    /// <summary>The verdict of <paramref name="steps"/>' checks; <see cref="Unknown"/> for a run that has none.</summary>
    public static string Of(IReadOnlyList<WorkflowRunStep> steps)
    {
        var checks = steps.Where(step => step.Node == NodeKinds.Check).ToList();
        if (checks.Count == 0) return Unknown;
        if (checks.Any(step => step.Detail == NodeStates.Failed)) return Failed;
        if (checks.Any(step => step.Detail == NodeStates.Stopped || (step.Detail == NodeStates.Unknown && step.Held?.Resolution == HeldResolutions.Stop))) return NotNow;
        if (checks.Any(step => step.Detail == NodeStates.Unknown)) return Unknown;
        if (checks.All(step => step.Detail == NodeStates.Reached)) return Verified;
        return checks.Any(step => step.Detail == NodeStates.Active) ? Running : Waiting;
    }
}
