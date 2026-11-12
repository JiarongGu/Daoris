namespace Daoris.Driver;

/// <summary>What a reached gate's verdict is at the tip handed back.</summary>
public static class CheckVerdicts
{
    public const string Reached = "reached";
    public const string Failed = "failed";

    /// <summary>Asked and not finished: queued or running.</summary>
    public const string Checking = "checking";

    /// <summary>No verdict at this tip and none asked: Daoris runs it.</summary>
    public const string Missing = "missing";
}

/// <summary>One gate the hand-back's paths reach, with its verdict.</summary>
public sealed record ObservedCheck(string Gate, string Verdict);

/// <summary>
/// A hand-back as Daoris reads it, never as the session says it: the base, how far the line moved past it, the paths touched, the gates
/// those paths reach with the verdict each has at this tip, and the processes left running. The checks are the landing's own selection
/// (<see cref="GateReach"/>), never a list of the worker's.
/// </summary>
internal sealed record HandbackObservation(
    string Tip, string? Base, int Behind, IReadOnlyList<string> Touched, IReadOnlyList<ObservedCheck> Checks, IReadOnlyList<LeftProcess> Left)
{
    /// <summary>The gates with no verdict and no ask: the ones Daoris runs itself, never refusing the hand-back for them.</summary>
    public IReadOnlyList<ObservedCheck> Missing => [.. Checks.Where(check => check.Verdict == CheckVerdicts.Missing)];

    /// <summary>Based on the line, every reached check green, nothing left running.</summary>
    public bool Ready => Behind == 0 && Left.Count == 0 && Checks.All(check => check.Verdict == CheckVerdicts.Reached);

    /// <summary>
    /// Judge what was read: the gates <paramref name="touched"/> reaches, each with the newest finished verdict of <paramref name="tip"/> among
    /// this session's checks (a verdict for other inputs counts for nothing), else <see cref="CheckVerdicts.Checking"/> while one is asked.
    /// </summary>
    public static HandbackObservation Judge(
        string tip, string? mergeBase, int behind, IReadOnlyList<string> touched, IReadOnlyList<DeclaredGate> gates,
        IReadOnlyList<(CheckAsk Ask, CheckState? State)> checks, IReadOnlyList<LeftProcess> left)
    {
        var reached = gates.Where(gate => GateReach.Reaches(gate, touched)).Select(gate => new ObservedCheck(gate.Name, VerdictOf(gate.Name, tip, checks)));
        return new HandbackObservation(tip, mergeBase, behind, touched, [.. reached], left);
    }

    private static string VerdictOf(string gate, string tip, IReadOnlyList<(CheckAsk Ask, CheckState? State)> checks)
    {
        var own = checks.Where(each => each.Ask.Gate == gate).ToList();
        var finished = own.Where(each => each.State is { Tip: { } at } state && at == tip && CheckStates.Finished(state.State))
            .OrderByDescending(each => each.State!.At).FirstOrDefault();
        if (finished.State is { } done) return done.State == CheckStates.Reached ? CheckVerdicts.Reached : CheckVerdicts.Failed;
        var waiting = own.Any(each => each.State is null or { State: CheckStates.Queued or CheckStates.Running });
        return waiting ? CheckVerdicts.Checking : CheckVerdicts.Missing;
    }

    /// <summary>The reading in words, for the session's record: what Daoris read and what it does about a missing check.</summary>
    public string Words()
    {
        var lines = new List<string>
        {
            $"Daoris read this hand-back from the tree, not from the note: based on {Base ?? "no common commit"}"
            + (Behind > 0 ? $", {Behind} {(Behind == 1 ? "commit" : "commits")} behind the line" : ", up to date with the line")
            + $"; {Touched.Count} {(Touched.Count == 1 ? "path" : "paths")} touched.",
        };
        foreach (var check in Checks)
        {
            lines.Add(check.Verdict switch
            {
                CheckVerdicts.Missing => $"Check `{check.Gate}` has no verdict at this tip: Daoris runs it and goes on with the verdict.",
                CheckVerdicts.Checking => $"Check `{check.Gate}` is waiting its turn on this machine.",
                _ => $"Check `{check.Gate}` {check.Verdict} at this tip.",
            });
        }

        foreach (var process in Left)
        {
            lines.Add($"Left running: `{process.Name}` ({process.Pid}), " + (process.Owned ? "started by this run, stopped now." : "not started by this run, left alone."));
        }

        return string.Join('\n', lines);
    }
}
