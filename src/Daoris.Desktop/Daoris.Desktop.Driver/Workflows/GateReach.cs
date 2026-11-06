namespace Daoris.Driver;

/// <summary>
/// What a batch's check is told to keep from reaching (the pipeline design §4.7): the commit its batch began at and the gates whose newest
/// evidence on the line is a pass, which stand until a path they reach changes.
/// </summary>
/// <param name="Base">The tip of the line's newest run with a verdict: the batch is the commits from it to the run's commit.</param>
/// <param name="Passed">The gates whose newest evidence on the line, before this run, is a pass: any other gate is owed and runs.</param>
public sealed record VerifyReach(string Base, IReadOnlyList<string> Passed);

/// <summary>
/// The gates a batch's changed paths reach, for any repository (the pipeline design §4.7): read from the accepted declaration's own
/// <c>paths</c> on each gate, as the merge tool's lane table places this repository's. A gate with no <c>paths</c> is reached by every change,
/// as a path no rule places reaches every gate there; a gate that is owed (its newest evidence on the line is not a pass) runs whatever the
/// batch changed, since the batch's base moves past a failed verdict.
/// </summary>
internal static class GateReach
{
    /// <summary>The gates <paramref name="declared"/> that run and the ones kept, in the order declared.</summary>
    internal sealed record Plan(IReadOnlyList<DeclaredGate> Run, IReadOnlyList<DeclaredGate> Kept);

    /// <summary>Whether a change to any of <paramref name="changed"/> reaches <paramref name="gate"/>.</summary>
    internal static bool Reaches(DeclaredGate gate, IReadOnlyCollection<string> changed) =>
        gate.Paths is not { Count: > 0 } paths || changed.Any(path => paths.Any(entry => Covers(entry, path)));

    /// <summary>Which of <paramref name="chosen"/> run, given the batch's <paramref name="changed"/> paths and the gates passed on the line.</summary>
    internal static Plan Of(IReadOnlyList<DeclaredGate> chosen, IReadOnlyCollection<string> changed, IReadOnlyCollection<string> passed)
    {
        var run = new List<DeclaredGate>();
        var kept = new List<DeclaredGate>();
        foreach (var gate in chosen)
        {
            if (!passed.Contains(gate.Name, StringComparer.Ordinal) || Reaches(gate, changed)) run.Add(gate);
            else kept.Add(gate);
        }

        return new Plan(run, kept);
    }

    /// <summary>The paths changed from <paramref name="from"/> to <paramref name="commit"/> in <paramref name="checkout"/>, or null where git could not say.</summary>
    internal static async Task<IReadOnlyList<string>?> ChangedAsync(string checkout, string from, string commit, CancellationToken ct)
    {
        var (code, names, _) = await WorkingTree.GitAsync(checkout, ["--no-optional-locks", "diff", "--name-only", "-z", "--no-renames", from, commit], ct).ConfigureAwait(false);
        return code == 0 ? [.. names.Split('\0', StringSplitOptions.RemoveEmptyEntries)] : null;
    }

    private static bool Covers(string entry, string path)
    {
        var named = entry.Replace('\\', '/');
        if (named.StartsWith("./", StringComparison.Ordinal)) named = named[2..];
        return named.EndsWith('/')
            ? path.StartsWith(named, StringComparison.Ordinal)
            : string.Equals(path, named, StringComparison.Ordinal) || path.StartsWith(named + "/", StringComparison.Ordinal);
    }
}

/// <summary>The gates a line has passed, read from the results its earlier runs left (the pipeline design §4.7).</summary>
internal static class GatesPassed
{
    /// <summary>
    /// The gates whose newest evidence on <paramref name="run"/>'s line, in the runs before it, is a pass or a flake that passed on a second
    /// run. A gate that failed or ended there, or has no evidence at all, is not among them: it is owed. A gate kept by reach or not run after
    /// a failure is no evidence, so what an older result says of it stands.
    /// </summary>
    internal static IReadOnlyList<string> Of(string home, VerifyRun run)
    {
        var decided = new HashSet<string>(StringComparer.Ordinal);
        var passed = new List<string>();
        var earlier = LineBatching.On(home, run.Repository, run.Line ?? "").Where(each => each.At < run.At && each.Run != run.Run);
        foreach (var before in earlier)
        {
            foreach (var result in ResultsOf(home, before.Run))
            {
                foreach (var gate in result.Gates.Where(each => each.Verdict is RunnerVerdicts.Pass or RunnerVerdicts.Flake or RunnerVerdicts.Fail or RunnerVerdicts.Ended))
                {
                    if (!decided.Add(gate.Name)) continue;
                    if (gate.Verdict is RunnerVerdicts.Pass or RunnerVerdicts.Flake) passed.Add(gate.Name);
                }
            }
        }

        return passed;
    }

    /// <summary>A run's results, newest attempt first; a result that does not read is passed over.</summary>
    private static IEnumerable<VerifyResult> ResultsOf(string home, string run)
    {
        var attempts = Path.Combine(VerifyFiles.RunFolder(home, run), "attempts");
        if (!Directory.Exists(attempts)) yield break;
        foreach (var folder in Directory.EnumerateDirectories(attempts).OrderByDescending(Directory.GetLastWriteTimeUtc))
        {
            if (VerifyResult.Read(Path.Combine(folder, VerifyFiles.Result)) is { } result) yield return result;
        }
    }
}
