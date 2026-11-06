namespace Daoris.Driver;

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
