using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a creation's ask and the registry alone say, judged without a disk or a git: the one code path the terminal's plan, the apply and the
/// window's press read, so a refusal is said the same wherever it is met.
/// </summary>
public static partial class CreateJudge
{
    /// <summary>The kinds a creation makes.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [PlaceKind.Git, PlaceKind.Folder];

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Name();

    [GeneratedRegex(@"^[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex LineName();

    /// <summary>The refusals the ask itself holds, in the order they are said: the name, the purpose, the kind, and a line on a folder.</summary>
    public static IReadOnlyList<CreateRefusal> Asked(CreateAsk ask)
    {
        var refusals = new List<CreateRefusal>();
        if (!NameUsable(ask.Name))
        {
            refusals.Add(new(CreateRefusals.NameUnusable,
                $"`{ask.Name}` cannot name a department: it is a folder's name and a registered name, so letters, digits, `.`, `_` and `-`, beginning with a letter or digit."));
        }

        if (ask.Purpose.Trim().Length == 0)
        {
            refusals.Add(new(CreateRefusals.NoPurpose, "say why it is opened with `--purpose \"…\"`: that line goes into its set-up quest and its first commit."));
        }

        if (!Kinds.Contains(ask.Kind, StringComparer.Ordinal))
        {
            refusals.Add(new(CreateRefusals.Kind, $"`{ask.Kind}` is not a kind a creation makes: {string.Join(" or ", Kinds.Select(each => $"`{each}`"))}."));
        }
        else if (ask.Kind == PlaceKind.Folder && ask.Line is { Length: > 0 })
        {
            refusals.Add(new(CreateRefusals.LineFolder, "a folder department has no history, so no line: leave `--line` out, or make a git repository."));
        }
        else if (ask.Line is { } line && LineProblem(line) is { } problem)
        {
            refusals.Add(new(CreateRefusals.LineUnusable, $"`{line}` cannot be a line: {problem}"));
        }

        return refusals;
    }

    /// <summary>Whether <paramref name="name"/> can be a folder's name and a registered name.</summary>
    public static bool NameUsable(string name) => Name().IsMatch(name) && !name.EndsWith('.');

    /// <summary>Why <paramref name="line"/> cannot name a branch, or null where it can: the part of git's rules this press holds to.</summary>
    public static string? LineProblem(string line)
    {
        if (line.Length == 0) return "it is empty.";
        if (!LineName().IsMatch(line)) return "a branch is named with letters, digits, `.`, `_`, `-` and `/` only.";
        if (line.StartsWith('-')) return "it would read as a flag.";
        if (line.Contains("..", StringComparison.Ordinal) || line.Contains("//", StringComparison.Ordinal)) return "it holds `..` or `//`.";
        if (line.EndsWith('/') || line.EndsWith('.') || line.EndsWith(".lock", StringComparison.Ordinal)) return "it ends in `/`, `.` or `.lock`.";
        return line.Split('/').Any(part => part.StartsWith('.') || part.EndsWith(".lock", StringComparison.Ordinal)) ? "a part of it begins with `.` or ends in `.lock`." : null;
    }

    /// <summary>The refusal of a name the registry holds already, in any workspace and in any case; null where it is free.</summary>
    public static CreateRefusal? Registered(string name, IReadOnlyList<RepoView> registry) =>
        registry.FirstOrDefault(row => string.Equals(row.Repository, name, StringComparison.OrdinalIgnoreCase)) is { } held
            ? new(CreateRefusals.NameRegistered,
                $"`{held.Repository}` is registered already, in workspace `{held.Workspace}`{(held.Root is { } root ? $" at {root}" : "")}: a name is one in every workspace, whatever its case.")
            : null;

    /// <summary>Whether the registry names <paramref name="workspace"/>: the default one always exists.</summary>
    public static bool WorkspaceExists(string workspace, IReadOnlyList<RepoView> registry) =>
        string.Equals(workspace, RemoteTarget.DefaultWorkspace, StringComparison.OrdinalIgnoreCase)
        || registry.Any(row => string.Equals(RemoteTarget.Workspace(row.Workspace), workspace, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The folder the workspace's git roots share most, where the registry says it: the parent most of them sit in, the one holding the newest of
    /// them where two tie. Null where no root can be read.
    /// </summary>
    public static string? Parent(IEnumerable<RepoView> rows, Func<string, DateTime> createdAt) =>
        rows.Where(row => row.Kind == PlaceKind.Git && row.Root is { Length: > 0 })
            .Select(row => Path.TrimEndingDirectorySeparator(row.Root!))
            .Select(root => (Root: root, Parent: Path.GetDirectoryName(root)))
            .Where(each => each.Parent is { Length: > 0 })
            .GroupBy(each => each.Parent!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(each => createdAt(each.Root)))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();

    /// <summary>The value most of <paramref name="values"/> share; the first met where two tie.</summary>
    public static T? Most<T>(IEnumerable<T> values) where T : class =>
        values.GroupBy(each => each).OrderByDescending(group => group.Count()).Select(group => group.Key).FirstOrDefault();
}
