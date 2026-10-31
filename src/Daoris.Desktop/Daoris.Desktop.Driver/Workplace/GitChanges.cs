namespace Daoris.Driver;

/// <summary>One file as a diff names it (GITAPP2b): its status letter, and the mode and blob id on each side, as git's raw form gives them.</summary>
/// <param name="Status"><c>A</c>, <c>M</c>, <c>D</c> or <c>T</c> (a type change).</param>
/// <param name="OldBlob">All zeros where the side holds none; the worktree side's blob is never hashed, so it is zeros too.</param>
public sealed record ChangeEntry(string Path, char Status, string OldMode, string NewMode, string OldBlob, string NewBlob);

/// <summary>A file the index holds in conflict (GITAPP2b): status's <c>u</c> entry, with the mode and blob id of stages 1, 2 and 3.</summary>
/// <param name="Code">Status's two letters (<c>UU</c>, <c>AA</c>, <c>DU</c> and the like).</param>
public sealed record UnmergedEntry(string Path, string Code, IReadOnlyList<string> Modes, IReadOnlyList<string> Blobs);

/// <summary>What a checkout's changes are, as three observations made at <see cref="ReadAt"/> (the git-writes design §4).</summary>
/// <param name="Head">The commit HEAD names, or null on a branch with no commit yet.</param>
/// <param name="Staged">HEAD to index.</param>
/// <param name="Unstaged">Index to worktree, with the index unrefreshed: a file whose stat data alone moved is no entry.</param>
/// <param name="Untracked">Every untracked file, not ignored, by its path.</param>
public sealed record ChangesRead(
    string? Head, IReadOnlyList<ChangeEntry> Staged, IReadOnlyList<ChangeEntry> Unstaged, IReadOnlyList<string> Untracked,
    IReadOnlyList<UnmergedEntry> Unmerged, DateTimeOffset ReadAt);

/// <summary>
/// **The Changes tab's read** (GITAPP2b, D204, the git-writes design §1 and §4). Three observations, each fresh, started through the
/// counting seam as a question under REVIEWLOCK1's flags: HEAD to index (<c>diff --cached HEAD</c>), index to worktree (<c>diff</c>,
/// the index not refreshed) and status's untracked and unmerged entries (<c>--porcelain=v2</c>). Nothing is kept across calls and no
/// memo of a landing or a judgement answers one; a staged addition whose file was then removed is in both diffs.
/// </summary>
public static class GitChanges
{
    // An empty environment is a variable of the call's own, which the memos never answer: each start is git's, as written.
    private static readonly IReadOnlyDictionary<string, string> Fresh = new Dictionary<string, string>();

    private static readonly string[] Quiet = ["--no-optional-locks", "-c", "core.quotePath=false"];

    private static readonly string[] DiffFlags = ["--no-color", "--no-ext-diff", "--no-renames", "-z", "--raw", "--no-abbrev"];

    /// <summary>The commands a person types in the checkout for the same answer.</summary>
    public static IReadOnlyList<string> Commands { get; } = ["git diff --cached HEAD", "git diff", "git status --porcelain=v2 --untracked-files=all"];

    public static async Task<GitAnswer<ChangesRead>> ReadAsync(string root, CancellationToken ct = default)
    {
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(null, GitReadRefusal.NotTop, "This folder is not the top of a repository of its own, so git is not asked.") { Commands = Commands };
        }

        var head = await Run(root, ["rev-parse", "--verify", "--quiet", "HEAD"], ct).ConfigureAwait(false);
        var tip = head.Code == 0 ? head.Stdout.Trim() : null;

        var staged = Run(root, [.. Quiet, "diff", "--cached", .. DiffFlags, .. (tip is null ? Array.Empty<string>() : ["HEAD"])], ct);
        // diff.autoRefreshIndex=false keeps the diff from writing the stat data back (REVIEWLOCK1); --numstat names what changed.
        var unstaged = Run(root, [.. Quiet, "-c", "diff.autoRefreshIndex=false", "diff", .. DiffFlags, "--numstat"], ct);
        var status = Run(root, [.. Quiet, "status", "--porcelain=v2", "-z", "--untracked-files=all", "--no-renames"], ct);
        await Task.WhenAll(staged, unstaged, status).ConfigureAwait(false);

        if (staged.Result.Code != 0 || unstaged.Result.Code != 0 || status.Result.Code != 0)
        {
            return new(null, GitReadRefusal.NotAnswered, "git could not read this checkout's changes: it may be busy, or git did not start.") { Commands = Commands };
        }

        var (untracked, unmerged) = Status(status.Result.Stdout);
        var read = new ChangesRead(tip, Entries(staged.Result.Stdout, counted: false), Entries(unstaged.Result.Stdout, counted: true), untracked, unmerged, DateTimeOffset.UtcNow);
        return new(read, GitReadRefusal.None, null) { Commands = Commands };
    }

    private static async Task<(int Code, string Stdout)> Run(string root, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(root, arguments, Fresh, timeout: null, ct).ConfigureAwait(false);
        return (code, stdout);
    }

    /// <summary>
    /// <c>--raw -z</c>'s records (<c>:old new oldblob newblob status</c>, then the path), and with <paramref name="counted"/> the
    /// <c>--numstat -z</c> ones after them: a tracked file whose stat data alone moved is listed <c>M</c> and has no count, so it is no change.
    /// </summary>
    private static List<ChangeEntry> Entries(string stdout, bool counted)
    {
        var parts = stdout.Split('\0');
        var entries = new List<ChangeEntry>();
        var at = 0;
        while (at + 1 < parts.Length && parts[at].StartsWith(':'))
        {
            var fields = parts[at][1..].Split(' ');
            if (fields.Length >= 5) entries.Add(new(parts[at + 1], fields[4][0], fields[0], fields[1], fields[2], fields[3]));
            at += 2;
        }

        if (!counted) return entries;
        var changed = new HashSet<string>(StringComparer.Ordinal);
        for (; at < parts.Length; at++)
        {
            var tabs = parts[at].Split('\t', 3);
            if (tabs.Length == 3) changed.Add(tabs[2]);
        }

        entries.RemoveAll(entry => !changed.Contains(entry.Path));
        return entries;
    }

    /// <summary>Status's v2 records, NUL-ended: <c>? path</c> untracked, <c>u XY sub m1 m2 m3 mW h1 h2 h3 path</c> unmerged; the rest are the diffs' to tell.</summary>
    private static (List<string> Untracked, List<UnmergedEntry> Unmerged) Status(string stdout)
    {
        var untracked = new List<string>();
        var unmerged = new List<UnmergedEntry>();
        foreach (var record in stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (record.StartsWith("? ", StringComparison.Ordinal))
            {
                untracked.Add(record[2..]);
            }
            else if (record.StartsWith("u ", StringComparison.Ordinal))
            {
                var fields = record.Split(' ', 11);
                if (fields.Length == 11) unmerged.Add(new(fields[10], fields[1], [fields[3], fields[4], fields[5]], [fields[7], fields[8], fields[9]]));
            }
        }

        return (untracked, unmerged);
    }
}
