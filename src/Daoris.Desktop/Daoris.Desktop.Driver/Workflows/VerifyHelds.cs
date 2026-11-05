namespace Daoris.Driver;

/// <summary>
/// What a check held for the person verifies (the pipeline design §4.4, §4.5): its run, the repository and the commit it judged, the line and
/// base its route bound it on, or the branch a person asked it of, and the gate that failed or why its verdict is not known. <i>What needs
/// you</i> words a failed or unknown verification by it.
/// </summary>
/// <param name="Level">Who chose the run: <see cref="VerifyRuns.Person"/>, or the landed route's level.</param>
public sealed record HeldVerification(string Run, string Repository, string Commit, string Level)
{
    /// <summary>The line a landed run's batch landed on; null for a person's run.</summary>
    public string? Line { get; init; }

    /// <summary>The tip of the line's newest run with a verdict, where its batch begins; null for the line's first verification.</summary>
    public string? Base { get; init; }

    /// <summary>The branch a person asked it of; null for a commit or a landed run.</summary>
    public string? Branch { get; init; }

    /// <summary>The gate the check's newest result names failed; null where none failed or no result reads.</summary>
    public string? Gate { get; init; }

    /// <summary>Why its verdict is not known, as its newest result gives it; null where that result is not unknown or none reads.</summary>
    public string? Why { get; init; }
}

/// <summary>The held checks on verification runs, each named by what it verifies, read beside the attempts held for the person.</summary>
public static class VerifyHelds
{
    /// <summary>Each held check on a verification run with its <see cref="HeldAttempt.Verification"/>; every other attempt as it is.</summary>
    public static IReadOnlyList<HeldAttempt> Named(NodeRecordFiles records, IReadOnlyList<HeldAttempt> held)
    {
        if (!held.Any(IsVerification)) return held;
        var runs = VerifyRuns.All(records.Home).ToDictionary(run => run.Run, StringComparer.Ordinal);
        return
        [
            .. held.Select(each => IsVerification(each) && runs.TryGetValue(CheckExecution.RunOf(each.Unit), out var run)
                ? each with { Verification = Of(records, run, each.Node) }
                : each),
        ];
    }

    /// <summary>What <paramref name="node"/>'s check on <paramref name="run"/> verifies, its gate and why from its newest result.</summary>
    public static HeldVerification Of(NodeRecordFiles records, VerifyRun run, string node)
    {
        var result = CheckResults.Newest(records.Home, run.Run, records.Read(RunUnits.IdOf(run.Run)), node);
        return new HeldVerification(run.Run, run.Repository, run.Commit, run.Level)
        {
            Line = run.Line, Base = run.Base, Branch = run.Branch, Gate = result?.Failed,
            Why = result is { Verdict: VerifyVerdicts.Unknown } ? result.Code ?? result.Why : null,
        };
    }

    /// <summary>Why a held check stands, as the terminal says it: the gate that failed, or why its verdict is not known and what it holds.</summary>
    public static string WhySaid(HeldAttempt held, HeldVerification verify) =>
        $"the verification of {verify.Repository} at {TextBounds.ShortCommit(verify.Commit, 7)}{Batch(verify)} "
        + (held.Failed
            ? $"failed{(verify.Gate is { } gate ? $" at the gate {gate}" : "")}, and runs again only on your word"
            : $"is not known ({LineRunWords.Why(verify.Why)}), and {(verify.Line is { } line ? $"holds its line {line}" : "waits")} until you run it again or say not now");

    private static string Batch(HeldVerification verify) => (verify.Line, verify.Base, verify.Branch) switch
    {
        ({ } line, { } from, _) => $" (its line {line}, the batch since {TextBounds.ShortCommit(from, 7)})",
        ({ } line, null, _) => $" (its line {line}, its first verification)",
        (null, _, { } branch) => $" ({branch})",
        _ => "",
    };

    private static bool IsVerification(HeldAttempt held) => held.Plugin == NodeKinds.Check && RunUnits.Is(held.Unit);
}
