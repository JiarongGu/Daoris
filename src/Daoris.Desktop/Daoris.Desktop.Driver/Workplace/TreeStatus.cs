namespace Daoris.Driver;

/// <summary>
/// A tree's uncommitted paths, its head commit and its branch, from one <c>status --porcelain=v2 --branch</c> where each would take a start of its
/// own (<c>status --porcelain</c>, <c>rev-parse HEAD^{commit}</c>, <c>symbolic-ref --short -q HEAD</c>). Read-only: the same question
/// asked again in a flow that reads once is answered from the first.
/// </summary>
/// <param name="Changed">The paths git lists as changed, unmerged or untracked, as <c>status --porcelain</c> counts them.</param>
/// <param name="Head">The head commit's id; null on an unborn branch.</param>
/// <param name="Branch">The branch the tree is on; null when its head is detached.</param>
internal sealed record TreeStatus(int Changed, string? Head, string? Branch)
{
    /// <summary>The one question, spelled as every read of a tree's status is, so a flow that reads once keeps the answer.</summary>
    internal static readonly string[] Ask = ["--no-optional-locks", "status", "--porcelain=v2", "--branch"];

    /// <summary>The tree's status, or null where git could not give it or answered in a shape not understood: the caller asks as before.</summary>
    internal static async Task<TreeStatus?> ReadAsync(string tree, CancellationToken ct)
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(tree, Ask, ct).ConfigureAwait(false);
        return code == 0 ? Parse(stdout) : null;
    }

    /// <summary>What <paramref name="porcelain"/> says; null where it names no head and no branch, as an older git prints none.</summary>
    internal static TreeStatus? Parse(string porcelain)
    {
        string? head = null, branch = null;
        var seen = 0;
        var changed = 0;
        foreach (var line in porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith('#')) changed++;
            else if (line.StartsWith("# branch.oid ", StringComparison.Ordinal))
            {
                seen++;
                head = line["# branch.oid ".Length..] is var oid and not "(initial)" ? oid : null;
            }
            else if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                seen++;
                branch = line["# branch.head ".Length..] is var name and not "(detached)" ? name : null;
            }
        }

        return seen == 2 ? new TreeStatus(changed, head, branch) : null;
    }

    /// <summary>A head and a branch read from the tree's own files, its paths not yet counted, so one <c>status</c> is asked where the walk knows which.</summary>
    internal static TreeStatus Unasked(string? head, string? branch) => new(-1, head, branch);

    internal bool Counted => Changed >= 0;

    /// <summary>The branch the tree's agent moved it onto, as <see cref="TreeFollows.OwnBranchOfAsync"/> names it: not a session branch.</summary>
    internal string? OwnBranch =>
        Branch is { } branch && !branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal) ? branch : null;
}
