using Daoris.Ports.Repositories;

namespace Daoris.Driver;

/// <summary>
/// A ref transaction run on real git: refused where a name is a symbolic ref (git would follow it to another name) or, on a case-blind
/// disk, where a create has a case-alias that stands; else one <c>update-ref --stdin</c>. Not done means nothing moved where git refused;
/// where it did not answer, each ref is read and said as it stands.
/// </summary>
internal static class GitRefTransacting
{
    internal delegate Task<(int Code, string Stdout, string Stderr)> InputRunner(string folder, IReadOnlyList<string> arguments, string input, CancellationToken ct);

    internal static async Task<GitRefRun> RunAsync(
        GitRepository.Runner run, InputRunner withInput, string root, GitRefTransaction transaction, CancellationToken ct)
    {
        var names = transaction.Acts.Select(act => act.Ref).Concat(transaction.Checks.Select(each => each.Ref)).Distinct(StringComparer.Ordinal).ToList();
        if (names.Count > 0)
        {
            var (listed, rows, listErr) = await run(root, ["for-each-ref", "--format=%(refname)%09%(symref)", "--", .. names], ct).ConfigureAwait(false);
            if (listed != 0) return new(false, $"whether a name is a symbolic ref is unread ({ContentHolds.Said(listErr)})");
            foreach (var row in rows.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = row.TrimEnd('\r').Split('\t');
                if (parts.Length == 2 && parts[1].Length > 0 && names.Contains(parts[0])) return new(false, $"`{parts[0]}` is a symbolic ref to `{parts[1]}`");
            }
        }

        if (await AliasStandingAsync(run, root, transaction, ct).ConfigureAwait(false) is { } alias) return new(false, alias);

        // One act and nothing to verify is one command line, the same operation without a stdin.
        var (code, _, err) = transaction.Checks.Count == 0 && transaction.Acts.Count == 1 && Single(transaction.Acts[0]) is { } line
            ? await run(root, line, ct).ConfigureAwait(false)
            : await withInput(root, ["update-ref", "--stdin"], transaction.Input(), ct).ConfigureAwait(false);
        if (code == 0) return new(true, "");
        // -1 is no answer: the bound reached or git never started. Only the refs themselves say whether anything moved.
        return code == -1 ? await UnansweredAsync(run, root, transaction, err, ct).ConfigureAwait(false) : new(false, ContentHolds.Said(err));
    }

    /// <summary>Where the disk is case-blind, a create whose name has a case-alias standing (loose or packed) is refused: the file system would make the two one file.</summary>
    private static async Task<string?> AliasStandingAsync(GitRepository.Runner run, string root, GitRefTransaction transaction, CancellationToken ct)
    {
        var creates = transaction.Acts.Where(act => act.Kind == GitRefActKind.Create).ToList();
        if (creates.Count == 0) return null;
        var (blind, said, configErr) = await run(root, ["config", "--type=bool", "--get", "core.ignorecase"], ct).ConfigureAwait(false);
        if (blind == -1) return $"whether the disk tells names apart by case is unread ({ContentHolds.Said(configErr)})";
        if (blind != 0 || said.Trim() != "true") return null;

        foreach (var prefix in creates.Select(act => act.Ref[..(act.Ref.LastIndexOf('/') + 1)]).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (code, rows, err) = await run(root, ["for-each-ref", "--format=%(refname)", "--", prefix], ct).ConfigureAwait(false);
            if (code != 0) return $"which refs stand under `{prefix}` is unread ({ContentHolds.Said(err)})";
            var standing = rows.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.TrimEnd('\r')).ToList();
            foreach (var act in creates)
            {
                var other = standing.FirstOrDefault(each =>
                    string.Equals(each, act.Ref, StringComparison.OrdinalIgnoreCase) && !string.Equals(each, act.Ref, StringComparison.Ordinal));
                if (other is not null) return $"`{act.Ref}` would be one ref with `{other}`, which stands, on a disk that reads names without case";
            }
        }

        return null;
    }

    /// <summary>Reads each ref the transaction moves and says how it stands: moved (at the new value), not moved (at the old), or unknown.</summary>
    private static async Task<GitRefRun> UnansweredAsync(GitRepository.Runner run, string root, GitRefTransaction transaction, string err, CancellationToken ct)
    {
        var told = new List<string>();
        foreach (var act in transaction.Acts)
        {
            var (code, at, _) = await run(root, ["rev-parse", "--verify", "--quiet", act.Ref], ct).ConfigureAwait(false);
            var stands = code == 0 ? at.Trim() : code == 1 ? null : "?";
            told.Add(stands == "?" ? $"`{act.Ref}` unknown (unread)"
                : stands == act.New ? $"`{act.Ref}` moved"
                : stands == act.Old ? $"`{act.Ref}` not moved"
                : $"`{act.Ref}` unknown (stands at {stands?[..Math.Min(stands.Length, 12)] ?? "absent"}, neither as it was nor as it was to be)");
        }

        return new(false, $"git did not answer ({ContentHolds.Said(err)}); as the refs stand: {string.Join(", ", told)}", Unanswered: true);
    }

    private static string[]? Single(GitRefAct act) => act.Kind switch
    {
        GitRefActKind.Create => ["update-ref", act.Ref, act.New!, ""],
        GitRefActKind.Update => ["update-ref", act.Ref, act.New!, act.Old!],
        _ => null,
    };
}
