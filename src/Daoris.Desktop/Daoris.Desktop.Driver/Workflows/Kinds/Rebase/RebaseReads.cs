namespace Daoris.Driver;

/// <summary>
/// What a run's <c>rebase</c> nodes read in the pass: the tip of each target their <c>onto</c> names, read once from the run's repository, which
/// the step is bound to (<see cref="WireTargets"/>); and what its door wrote of each attempt (<see cref="RebaseRecords"/>), its reading's outcome.
/// </summary>
internal static class RebaseReads
{
    public static async Task<NodeGitReads> ReadAsync(GovernedUnit governed, string home, NodeGitReads reads, CancellationToken ct)
    {
        var line = governed.Scope?.Branch?.Records.Branch?.Line;
        var named = governed.Graph.Nodes.Where(node => node.Kind == NodeKinds.Rebase).Select(node => RebaseOnto.Of(node, line)).OfType<string>()
            .Distinct(StringComparer.Ordinal).ToList();
        var tips = await TipsAsync(governed.Watched.Root, named, ct).ConfigureAwait(false);
        var earlier = reads.Tip;
        var records = new RebaseRecords(home);
        var read = new Dictionary<string, IReadOnlyList<RebaseAttempt>>(StringComparer.Ordinal);
        return reads with
        {
            Tip = target => tips.TryGetValue(target, out var tip) ? tip : earlier?.Invoke(target),
            Rebased = (run, node, attempt) =>
            {
                if (!read.TryGetValue(run, out var attempts)) read[run] = attempts = records.Of(run);
                return attempts.LastOrDefault(each => each.Node == node && each.Attempt == attempt);
            },
        };
    }

    /// <summary>The commit each of <paramref name="refs"/> stands at in <paramref name="root"/>; a ref git cannot read is absent, so nothing binds to it.</summary>
    public static async Task<Dictionary<string, string>> TipsAsync(string root, IReadOnlyList<string> refs, CancellationToken ct)
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        if (refs.Count == 0 || root.Length == 0 || !Directory.Exists(root)) return tips;
        var (code, listed, _) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)%09%(objectname)", .. refs], ct).ConfigureAwait(false);
        if (code != 0) return tips;
        foreach (var parts in listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => line.Split('\t')))
        {
            if (parts.Length == 2 && refs.Contains(parts[0], StringComparer.Ordinal)) tips[parts[0]] = parts[1];
        }

        return tips;
    }
}
