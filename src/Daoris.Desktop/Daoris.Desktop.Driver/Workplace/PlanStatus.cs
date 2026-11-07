namespace Daoris.Driver;

/// <summary>
/// What a switch or a stash plan rests on in the working tree, from one <c>status --porcelain=v2 --branch -z --untracked-files=no</c> where
/// the head, the branch, the changed tracked paths and the files in conflict were four starts. A plan and the press that judges it read the same
/// way, so what the page compared by and what the press compares are one shape.
/// </summary>
/// <param name="Head">The head commit's id; null on an unborn branch.</param>
/// <param name="Branch">The branch the head names; null when it is detached.</param>
/// <param name="Tracked">The tracked paths changed, a file in conflict one of them.</param>
/// <param name="Unmerged">Whether a file in conflict stands in the index.</param>
internal sealed record PlanStatus(string? Head, string? Branch, int Tracked, bool Unmerged)
{
    /// <summary>The checkout's status, or null where git could not give it or named no head and branch.</summary>
    internal static async Task<PlanStatus?> ReadAsync(string root, CancellationToken ct)
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "status", "--porcelain=v2", "--branch", "-z", "--untracked-files=no", "--no-renames"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        return code == 0 ? Parse(stdout) : null;
    }

    internal static PlanStatus? Parse(string porcelain)
    {
        string? head = null, branch = null;
        var seen = 0;
        var tracked = 0;
        var unmerged = false;
        foreach (var entry in porcelain.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.StartsWith("# branch.oid ", StringComparison.Ordinal))
            {
                seen++;
                head = entry["# branch.oid ".Length..] is var oid and not "(initial)" ? oid : null;
            }
            else if (entry.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                seen++;
                branch = entry["# branch.head ".Length..] is var name and not "(detached)" ? name : null;
            }
            else if (!entry.StartsWith('#'))
            {
                tracked++;
                unmerged |= entry.StartsWith("u ", StringComparison.Ordinal);
            }
        }

        return seen == 2 ? new PlanStatus(head, branch, tracked, unmerged) : null;
    }
}
