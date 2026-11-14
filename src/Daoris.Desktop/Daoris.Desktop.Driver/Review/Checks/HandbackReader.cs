namespace Daoris.Driver;

/// <summary>
/// Reads a handed-back tree from git and its declared gates: the tip, the merge base with the line, how many commits the line moved past
/// it and the paths touched since, then judged with the checks the session's asks have (<see cref="HandbackObservation.Judge"/>).
/// </summary>
internal static class HandbackReader
{
    /// <summary>The observation of <paramref name="folder"/> against <paramref name="line"/>, or null where git could not say.</summary>
    internal static async Task<HandbackObservation?> ReadAsync(
        string folder, string line, IReadOnlyList<DeclaredGate> gates, IReadOnlyList<(CheckAsk Ask, CheckState? State)> checks,
        IReadOnlyList<LeftProcess> left, CancellationToken ct)
    {
        var tip = await OneAsync(folder, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
        var mergeBase = await OneAsync(folder, ["merge-base", "HEAD", line], ct).ConfigureAwait(false);
        if (tip is null || mergeBase is null) return null;
        var behind = await OneAsync(folder, ["rev-list", "--count", $"HEAD..{line}"], ct).ConfigureAwait(false);
        var touched = await GateReach.ChangedAsync(folder, mergeBase, tip, ct).ConfigureAwait(false);
        if (touched is null) return null;
        var unsigned = await CommitAttribution.UnsignedAsync(folder, mergeBase, tip, ct).ConfigureAwait(false);
        return HandbackObservation.Judge(tip, mergeBase, int.TryParse(behind, out var count) ? count : 0, touched, gates, checks, left, unsigned);
    }

    private static readonly NoteCode[] Dones = [EndNoteCodes.EndedDone, EndNoteCodes.EndedDoneLeft, EndNoteCodes.EndedSubmitted];

    /// <summary>Whether <paramref name="record"/> is a session of this machine's that ended done in a tree of its own.</summary>
    internal static bool EndedDone(PriorSession record) =>
        !record.Teammate && record.Tree is { Length: > 0 } && string.Equals(record.State, "completed", StringComparison.OrdinalIgnoreCase)
        && Dones.Any(code => HelperWaitTrees.Carries(record.NoteParts, code));

    /// <summary>The gates the tree's declaration names, none where it names none.</summary>
    internal static IReadOnlyList<DeclaredGate> GatesOf(string folder)
    {
        var declared = Path.Combine(folder, DeclarationNames.Gates);
        return DeclaredGates.Read(File.Exists(declared) ? File.ReadAllText(declared) : null);
    }

    /// <summary>The line a tree's work goes back to: the remote's head where it names one, else a local <c>main</c> or <c>master</c>; null where none is there.</summary>
    internal static async Task<string?> LineAsync(string folder, CancellationToken ct)
    {
        if (await OneAsync(folder, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], ct).ConfigureAwait(false) is { } remote) return remote;
        foreach (var name in new[] { "main", "master" })
        {
            if (await OneAsync(folder, ["rev-parse", "--verify", "--quiet", name], ct).ConfigureAwait(false) is not null) return name;
        }

        return null;
    }

    private static async Task<string?> OneAsync(string folder, string[] arguments, CancellationToken ct)
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(folder, ["--no-optional-locks", .. arguments], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() is { Length: > 0 } read ? read : null;
    }
}
