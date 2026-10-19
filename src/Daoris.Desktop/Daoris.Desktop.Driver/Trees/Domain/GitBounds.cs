namespace Daoris.Driver;

/// <summary>
/// How long one git call may take, by its kind (PARALLEL1e, D181 point 4): every call has a bound, so one that never answers
/// (a lock, a prompt nobody types, a scan that holds it) ends there, its tree stopped, and its subject is left unknown for the
/// next wake rather than holding whoever waits. A caller that knows its call is long passes its own (<see cref="SyncBounds"/>).
/// </summary>
/// <remarks>
/// <para><b>Two kinds.</b> A question (a ref, a status, a log, a diff, an object) or a ref's move is answered from the object
/// store in seconds; anything else may write a tree, run a repository's hooks or reach the network, and is given the longer
/// bound. A command this does not name is work: a write cut short would be left half done, a question given long only ends
/// later.</para>
/// <para><b>Backstops, not waits.</b> A person's press never waits on git's reads (D181 point 1); these only end a git nobody
/// should still be waiting for, so they are set well past what a large repository takes on a loaded machine, never at it.</para>
/// </remarks>
public static class GitBounds
{
    /// <summary>A question or a ref's move: a large repository's status or log, on a loaded machine, with room to spare.</summary>
    public static readonly TimeSpan Question = TimeSpan.FromMinutes(2);

    /// <summary>Work on a tree, a commit's hooks, a clone or the network: twice <see cref="SyncBounds.Replay"/>'s bound.</summary>
    public static readonly TimeSpan Work = TimeSpan.FromMinutes(10);

    // Read-only, or a move of refs alone: answered from the object store and the refs, never writing a tree.
    private static readonly HashSet<string> Questions = new(StringComparer.Ordinal)
    {
        "rev-parse", "rev-list", "status", "log", "show", "show-ref", "diff", "diff-tree", "diff-index", "diff-files",
        "cat-file", "ls-files", "ls-tree", "for-each-ref", "merge-base", "merge-tree", "symbolic-ref", "name-rev", "describe",
        "blame", "cherry", "shortlog", "grep", "count-objects", "check-ignore", "check-attr", "check-ref-format", "var", "version",
        "config", "hash-object", "update-ref", "branch", "tag",
    };

    // The options git reads before its command that take the next word as their value.
    private static readonly HashSet<string> Valued = new(StringComparer.Ordinal) { "-c", "-C", "--git-dir", "--work-tree", "--namespace" };

    /// <summary>The bound a call with <paramref name="arguments"/> runs under: the caller's own where it passed one.</summary>
    public static TimeSpan Of(IReadOnlyList<string> arguments, TimeSpan? own = null) => own ?? (IsQuestion(arguments) ? Question : Work);

    private static bool IsQuestion(IReadOnlyList<string> arguments)
    {
        for (var at = 0; at < arguments.Count; at++)
        {
            var word = arguments[at];
            if (Valued.Contains(word))
            {
                at++;
                continue;
            }

            if (word.StartsWith('-')) continue;
            // `worktree list` and `stash list` only read; their other forms write a tree.
            if (word is "worktree" or "stash") return at + 1 < arguments.Count && arguments[at + 1] == "list";
            return Questions.Contains(word);
        }

        return false;
    }
}
