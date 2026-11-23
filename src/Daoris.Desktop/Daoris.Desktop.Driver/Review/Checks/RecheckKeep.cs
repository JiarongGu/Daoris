namespace Daoris.Driver;

/// <summary>
/// The gates a run's check, asked again after a fix, keeps from the run's attempts before: each gate whose newest word there is a pass, given
/// at the same base and under the same declaration, where no path changed between the tree that attempt judged and the one judged now reaches
/// it. A gate kept, not run or ended there says nothing of it, so the word before is read, as a line's evidence is (<see cref="GatesPassed"/>);
/// a newest failure keeps nothing. The merge tool's <c>--rerun</c> keeps its verdicts by the same rule.
/// </summary>
internal static class RecheckKeep
{
    /// <summary>A gate kept, and the tree it passed at.</summary>
    internal sealed record Kept(string Gate, string From);

    /// <summary>The results of <paramref name="run"/>'s attempts other than <paramref name="invocation"/>, newest written first.</summary>
    internal static IReadOnlyList<VerifyResult> Before(string home, string run, string invocation)
    {
        var folder = Path.Combine(VerifyFiles.RunFolder(home, run), "attempts");
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.EnumerateDirectories(folder)
            .Where(each => !string.Equals(Path.GetFileName(each), invocation, StringComparison.Ordinal))
            .Select(each => Path.Combine(each, VerifyFiles.Result))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Select(VerifyResult.Read)
            .OfType<VerifyResult>()];
    }

    /// <summary>
    /// The gates of <paramref name="gates"/> kept from <paramref name="earlier"/> (newest first) for a check judging a tree on
    /// <paramref name="onto"/> under <paramref name="declaration"/>: <paramref name="changedSince"/> says the paths changed between an
    /// earlier judged tree and now (null where git cannot say, which keeps nothing), and <paramref name="reaches"/> whether they reach a gate.
    /// </summary>
    internal static async Task<IReadOnlyList<Kept>> OfAsync(
        IReadOnlyList<VerifyResult> earlier, string? onto, string? declaration, IReadOnlyList<DeclaredGate> gates,
        Func<string, Task<IReadOnlyCollection<string>?>> changedSince, Func<IReadOnlyCollection<string>, DeclaredGate, bool> reaches)
    {
        if (onto is null || declaration is null) return [];
        var compared = new Dictionary<string, IReadOnlyCollection<string>?>(StringComparer.Ordinal);
        var kept = new List<Kept>();
        foreach (var gate in gates)
        {
            if (Passed(earlier, gate.Name) is not { Judged: { } from } attempt || attempt.Onto != onto || attempt.Declaration != declaration) continue;
            if (!compared.TryGetValue(from, out var changed)) compared[from] = changed = await changedSince(from).ConfigureAwait(false);
            if (changed is not null && (changed.Count == 0 || !reaches(changed, gate))) kept.Add(new Kept(gate.Name, from));
        }

        return kept;
    }

    /// <summary>The words that name each gate kept and the tree it passed at; null where it keeps none.</summary>
    internal static string? Words(IReadOnlyList<Kept> kept, string onto, string line) =>
        kept.Count == 0 ? null
            : $"Kept from this run's checks before, on the same base (`{line}` at {TextBounds.ShortCommit(onto, 7)}), as no path changed since reaches them: "
              + string.Join("; ", kept.GroupBy(each => each.From, StringComparer.Ordinal)
                  .Select(group => $"{string.Join(", ", group.Select(each => each.Gate))} (passed at {TextBounds.ShortCommit(group.Key, 7)})"))
              + ".";

    /// <summary>The newest attempt whose word on <paramref name="gate"/> is a pass, null where its newest word is a failure or none says.</summary>
    private static VerifyResult? Passed(IReadOnlyList<VerifyResult> earlier, string gate)
    {
        foreach (var attempt in earlier)
        {
            var word = attempt.Gates.FirstOrDefault(each => each.Name == gate)?.Verdict;
            if (word == RunnerVerdicts.Pass) return attempt;
            if (word == RunnerVerdicts.Fail) return null;
        }

        return null;
    }
}
