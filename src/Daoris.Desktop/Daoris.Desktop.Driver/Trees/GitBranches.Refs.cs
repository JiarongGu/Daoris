using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// One ref as the branch list's <c>for-each-ref</c> answered it (GIT1a, D147 §4.1): a local branch or one of origin's, its
/// commit, and what the format asks of it.
/// </summary>
/// <param name="Name">Its whole name: <c>refs/heads/…</c> or <c>refs/remotes/origin/…</c>.</param>
/// <param name="Commit">The commit it names, in full.</param>
public sealed record GitRef(string Name, string Commit)
{
    /// <summary>Its commit's committer date, or null where git wrote none that reads.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>Its commit's subject.</summary>
    public string Subject { get; init; } = "";

    /// <summary>The ref it points to, for a symbolic one (<c>origin/HEAD</c>); null otherwise.</summary>
    public string? Symref { get; init; }

    /// <summary>The working tree it is checked out in, the repository's own checkout included; null where none, or not asked.</summary>
    public string? Worktree { get; init; }

    /// <summary>Its commits the line does not hold, or null where not counted.</summary>
    public int? Ahead { get; init; }

    /// <summary>The line's commits it does not hold, or null where not counted.</summary>
    public int? Behind { get; init; }

    /// <summary>Whether it is a local branch.</summary>
    public bool Local => Name.StartsWith(GitRefs.Heads, StringComparison.Ordinal);

    /// <summary>Its name as a person reads it: <c>topic</c>, or <c>origin/topic</c>.</summary>
    public string Short => Local ? Name[GitRefs.Heads.Length..]
        : Name.StartsWith("refs/remotes/", StringComparison.Ordinal) ? Name["refs/remotes/".Length..]
        : Name;
}

/// <summary>
/// The branch list's git calls and the pure readers of what git answers (GIT1a, D147 §4.1): each call asks a machine format,
/// never a human one, and each format has a reader here with its own tests, apart from any process.
/// </summary>
/// <remarks>
/// <para><b>One format, every form.</b> The list asks seven fields, each ended by a NUL. An atom a fallback leaves out keeps
/// its place, empty, so the one reader reads every form the fallbacks ask. A NUL ends each field, so a newline inside one
/// is the field's own; git's newline after each record is read as the start of the next record's first field, which is a
/// ref name and never begins with one.</para>
///
/// <para><b>Two atoms have a floor</b>, each read from git's own documentation of <c>for-each-ref</c> at its tags
/// (2026-10-04): <c>%(worktreepath)</c> is documented from v2.23.0 and absent at v2.22.0; <c>%(ahead-behind:…)</c> from
/// v2.41.0 and absent at v2.40.0. Both answered on Git 2.53 here.</para>
/// </remarks>
public static class GitRefs
{
    /// <summary>Where local branches live.</summary>
    public const string Heads = "refs/heads/";

    /// <summary>Where origin's branches live, as this checkout last fetched them.</summary>
    public const string Origin = "refs/remotes/origin/";

    /// <summary>The atom naming the tree a branch is checked out in.</summary>
    public const string WorktreePath = "worktreepath";

    /// <summary>The atom counting a ref against one commit.</summary>
    public const string AheadBehind = "ahead-behind";

    /// <summary>The first git whose <c>for-each-ref</c> knows <c>%(worktreepath)</c>.</summary>
    public static Version WorktreePathSince { get; } = new(2, 23);

    /// <summary>The first git whose <c>for-each-ref</c> knows <c>%(ahead-behind:…)</c>.</summary>
    public static Version AheadBehindSince { get; } = new(2, 41);

    /// <summary>The fields in a record, in the order the format asks them.</summary>
    private const int Fields = 7;

    /// <summary>
    /// The list's one call: local branches and origin's, each with its commit, time, subject, the symbolic ref it points to,
    /// and where asked the tree holding it and its distance from <paramref name="against"/>.
    /// </summary>
    /// <param name="trees">Whether to ask <c>%(worktreepath)</c>.</param>
    /// <param name="against">The whole name of the ref distances are counted from, or null to count none.</param>
    public static IReadOnlyList<string> Arguments(bool trees, string? against) =>
        ["for-each-ref", "--format=" + Format(trees, against), "refs/heads", "refs/remotes/origin"];

    private static string Format(bool trees, string? against) =>
        "%(refname)%00%(objectname)%00%(committerdate:iso-strict)%00%(symref)%00"
        + (trees ? "%(worktreepath)" : "") + "%00"
        + (against is null ? "" : $"%(ahead-behind:{against})") + "%00"
        + "%(contents:subject)%00";

    /// <summary>
    /// Every record the list's call answered, in git's order. A record cut short, or one naming no commit, is passed over
    /// rather than read askew.
    /// </summary>
    public static IReadOnlyList<GitRef> Parse(string stdout)
    {
        var fields = stdout.Split('\0');
        var refs = new List<GitRef>();
        for (var at = 0; at + Fields <= fields.Length; at += Fields)
        {
            var name = fields[at].TrimStart('\n', '\r');
            var commit = fields[at + 1];
            if (!name.StartsWith("refs/", StringComparison.Ordinal) || !WorkingTree.IsCommitId(commit)) continue;

            var (ahead, behind) = Distance(fields[at + 5]);
            refs.Add(new GitRef(name, commit)
            {
                At = DateTimeOffset.TryParse(fields[at + 2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when) ? when : null,
                Symref = Filled(fields[at + 3]),
                Worktree = Filled(fields[at + 4]),
                Ahead = ahead,
                Behind = behind,
                Subject = fields[at + 6],
            });
        }

        return refs;
    }

    /// <summary>
    /// The fallback for a git without <c>%(worktreepath)</c>: <c>git worktree list --porcelain</c>, read as each branch's
    /// whole name and the tree it is checked out in. A detached tree holds no branch.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseWorktrees(string porcelain)
    {
        var trees = new Dictionary<string, string>(StringComparer.Ordinal);
        string? tree = null;
        foreach (var raw in porcelain.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("worktree ", StringComparison.Ordinal)) tree = line["worktree ".Length..];
            else if (line.StartsWith("branch ", StringComparison.Ordinal) && tree is not null) trees[line["branch ".Length..]] = tree;
            else if (line.Length == 0) tree = null;
        }

        return trees;
    }

    /// <summary><c>rev-list --left-right --count A...B</c>: the commits only A holds, then only B; null where it does not read.</summary>
    public static (int Left, int Right)? ParseCounts(string stdout)
    {
        var parts = stdout.Trim().Split('\t');
        return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var left)
               && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var right)
            ? (left, right)
            : null;
    }

    /// <summary>
    /// A call as a person types it in the checkout (D147 §3.3): <c>git</c> and its arguments, an argument holding anything a
    /// shell reads in double quotes. Every argument here is a ref's name, a commit id or a format of this class's, none of
    /// which holds a double quote or a dollar sign, so the quotes read the same in bash, PowerShell and the Command Prompt.
    /// Daoris's own <c>-c core.longpaths=true</c> is not the read's, and is not said.
    /// </summary>
    public static string Command(IEnumerable<string> arguments) =>
        "git " + string.Join(' ', arguments.Select(argument =>
            argument.Length > 0 && argument.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/' or ':' or '=' or '@' or '+' or ',')
                ? argument
                : $"\"{argument}\""));

    /// <summary><c>%(ahead-behind:…)</c>'s two numbers, or none where the field is empty or does not read.</summary>
    private static (int? Ahead, int? Behind) Distance(string field)
    {
        var parts = field.Split(' ');
        return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ahead)
               && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var behind)
            ? (ahead, behind)
            : (null, null);
    }

    private static string? Filled(string field) => field.Length == 0 ? null : field;
}
