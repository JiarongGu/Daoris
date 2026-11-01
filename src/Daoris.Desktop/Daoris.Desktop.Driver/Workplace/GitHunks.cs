using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>One line of a hunk: <c>' '</c> context, <c>'+'</c> added, <c>'-'</c> removed, or <c>'\\'</c> git's no-newline marker.</summary>
public sealed record HunkLine(char Kind, string Text);

/// <summary>A hunk as git prints it; <paramref name="Id"/> names its text, so a press can say which hunk it meant.</summary>
public sealed record Hunk(string Id, string Header, IReadOnlyList<HunkLine> Lines);

/// <summary>
/// One file's hunks on one side (GITAPP2h): <c>unstaged</c> is the index to the worktree, <c>staged</c> HEAD to the index. The ids are what a press
/// must still find equal: <paramref name="Index"/> the index entry (<c>mode blob</c>, or empty where the index holds none), <paramref name="Source"/>
/// the bytes the hunks come from (the worktree file's id for <c>unstaged</c>, HEAD's entry for <c>staged</c>, empty where there is none).
/// </summary>
public sealed record FileHunks(string Path, string Side, string? Head, string Index, string Source, bool Binary, IReadOnlyList<Hunk> Hunks);

/// <summary>
/// **The hunks of a file and the patches made from them** (GITAPP2h, D204, the git-writes design §1, §4). Read as <c>diff --no-ext-diff
/// --no-textconv --full-index</c> under REVIEWLOCK1's flags (the index never refreshed), through the counting seam as a question. A patch of
/// chosen hunks or lines is built here, never by git's own editor; it is applied only to a copy of the index to find what an act would leave.
/// </summary>
public static class GitHunks
{
    public const string Unstaged = "unstaged";

    public const string Staged = "staged";

    private static readonly IReadOnlyDictionary<string, string> Fresh = new Dictionary<string, string>();

    private static readonly string[] Quiet = ["--no-optional-locks", "-c", "core.quotePath=false", "-c", "diff.autoRefreshIndex=false"];

    private static readonly string[] DiffFlags = ["--no-color", "--no-ext-diff", "--no-textconv", "--no-renames", "--full-index", "-U3"];

    /// <summary>The commands a person types for the same answer.</summary>
    public static IReadOnlyList<string> Commands { get; } = ["git diff --no-ext-diff --no-textconv --full-index -- <path>", "git diff --cached --no-ext-diff --no-textconv --full-index HEAD -- <path>"];

    public static async Task<GitAnswer<FileHunks>> ReadAsync(string root, string path, string side, CancellationToken ct = default)
    {
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(null, GitReadRefusal.NotTop, "This folder is not the top of a repository of its own, so git is not asked.") { Commands = Commands };
        }

        if (side is not (Unstaged or Staged) || path.Length == 0 || Path.IsPathRooted(path) || path.Split('/', '\\').Contains(".."))
        {
            return new(null, GitReadRefusal.NotAPath, "That is not a path in the repository.") { Commands = Commands };
        }

        var faces = await FacesAsync(root, path, side, ct).ConfigureAwait(false);
        var diff = faces is null ? null : await DiffAsync(root, path, side, faces.Head, ct).ConfigureAwait(false);
        if (faces is null || diff is null) return new(null, GitReadRefusal.NotAnswered, "git could not read this file's changes: it may be busy, or git did not start.") { Commands = Commands };
        var (_, hunks, binary) = diff.Value;
        return new(new FileHunks(path, side, faces.Head, faces.Index, faces.Source, binary, hunks), GitReadRefusal.None, null) { Commands = Commands };
    }

    /// <summary>What a press must find unchanged: HEAD, the index entry of the path and the source's id.</summary>
    internal sealed record Faces(string? Head, string Index, string Source);

    internal static async Task<Faces?> FacesAsync(string root, string path, string side, CancellationToken ct)
    {
        var head = await Run(root, ["rev-parse", "--verify", "--quiet", "HEAD"], null, ct).ConfigureAwait(false);
        var tip = head.Code == 0 ? head.Stdout.Trim() : null;
        var listed = await Run(root, [.. Quiet, "ls-files", "-s", "-z", "--full-name", "--", ":(literal)" + path], null, ct).ConfigureAwait(false);
        if (listed.Code != 0) return null;
        var rows = listed.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        // An unmerged path holds stages 1 to 3: Resolve's, never this act's.
        if (rows.Length > 1 || (rows.Length == 1 && !rows[0].Contains(" 0\t", StringComparison.Ordinal))) return new(tip, "unmerged", "");
        var index = rows.Length == 0 ? "" : rows[0][..rows[0].IndexOf(" 0\t", StringComparison.Ordinal)];
        if (side == Staged)
        {
            if (tip is null) return new(tip, index, "");
            var tree = await Run(root, [.. Quiet, "ls-tree", "-z", "HEAD", "--", ":(literal)" + path], null, ct).ConfigureAwait(false);
            if (tree.Code != 0) return null;
            var at = tree.Stdout.IndexOf('\t');
            var source = at < 0 ? "" : tree.Stdout[..at].Replace(" blob ", " ", StringComparison.Ordinal);
            return new(tip, index, source);
        }

        var file = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(file)) return new(tip, index, "");
        var hashed = await Run(root, [.. Quiet, "hash-object", "--path=" + path, "--", path], null, ct).ConfigureAwait(false);
        return hashed.Code == 0 ? new(tip, index, hashed.Stdout.Trim()) : null;
    }

    /// <summary>The file's diff on a side: its header lines, its hunks, and whether git calls it binary; null where git could not say.</summary>
    internal static async Task<(IReadOnlyList<string> Header, IReadOnlyList<Hunk> Hunks, bool Binary)?> DiffAsync(
        string root, string path, string side, string? head, CancellationToken ct)
    {
        IReadOnlyList<string> arguments = side == Staged
            ? [.. Quiet, "diff", "--cached", .. DiffFlags, .. (head is null ? Array.Empty<string>() : ["HEAD"]), "--", ":(literal)" + path]
            : [.. Quiet, "diff", .. DiffFlags, "--", ":(literal)" + path];
        var run = await Run(root, arguments, null, ct).ConfigureAwait(false);
        return run.Code == 0 ? Parse(run.Stdout) : null;
    }

    internal static (IReadOnlyList<string> Header, IReadOnlyList<Hunk> Hunks, bool Binary) Parse(string diff)
    {
        var lines = diff.Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        var header = new List<string>();
        var hunks = new List<Hunk>();
        string? title = null;
        List<HunkLine>? body = null;
        var binary = false;
        void Close()
        {
            if (title is null || body is null) return;
            var text = title + "\n" + string.Join('\n', body.Select(line => line.Kind + line.Text));
            hunks.Add(new Hunk(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12], title, body));
            title = null;
            body = null;
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                Close();
                (title, body) = (line, []);
            }
            else if (body is not null && line.Length > 0 && line[0] is ' ' or '+' or '-' or '\\')
            {
                body.Add(new HunkLine(line[0], line[1..]));
            }
            else if (body is null)
            {
                if (line.StartsWith("Binary files ", StringComparison.Ordinal) || line.StartsWith("GIT binary patch", StringComparison.Ordinal)) binary = true;
                header.Add(line);
            }
        }

        Close();
        return (header, hunks, binary);
    }

    /// <summary>
    /// A patch of the chosen hunks, or of the chosen lines of a hunk (indexes into its <see cref="Hunk.Lines"/>; null takes the whole hunk), to be applied to
    /// the index: forward for <c>unstaged</c>, reversed for <c>staged</c>. A line not chosen is left as the index has it: for a forward patch an unchosen
    /// removal stays as context and an unchosen addition goes; for a reversed one an unchosen addition stays as context and an unchosen removal goes.
    /// Null where no line is left to change.
    /// </summary>
    internal static string? Patch(IReadOnlyList<string> header, IReadOnlyList<(Hunk Hunk, IReadOnlySet<int>? Lines)> picks, bool reverse)
    {
        var text = new StringBuilder();
        foreach (var line in header) text.Append(line).Append('\n');
        var changed = false;
        foreach (var (hunk, chosen) in picks)
        {
            var kept = new List<HunkLine>();
            var previousKept = false;
            for (var at = 0; at < hunk.Lines.Count; at++)
            {
                var line = hunk.Lines[at];
                if (line.Kind == '\\')
                {
                    // The marker belongs to the line before it.
                    if (previousKept) kept.Add(line);
                    continue;
                }

                var stays = reverse ? line.Kind == '+' : line.Kind == '-';
                if (line.Kind == ' ' || chosen is null || chosen.Contains(at)) kept.Add(line);
                else if (stays) kept.Add(line with { Kind = ' ' });
                previousKept = line.Kind == ' ' || chosen is null || chosen.Contains(at) || stays;
            }

            if (!kept.Any(line => line.Kind is '+' or '-')) continue;
            changed = true;
            var (oldCount, newCount) = (kept.Count(line => line.Kind is ' ' or '-'), kept.Count(line => line.Kind is ' ' or '+'));
            text.Append(Recounted(hunk.Header, oldCount, newCount)).Append('\n');
            foreach (var line in kept) text.Append(line.Kind).Append(line.Text).Append('\n');
        }

        return changed ? text.ToString() : null;
    }

    private static string Recounted(string header, int oldCount, int newCount)
    {
        // "@@ -a,b +c,d @@ tail"
        var close = header.IndexOf(" @@", 3, StringComparison.Ordinal);
        var tail = close < 0 ? "" : header[(close + 3)..];
        var parts = (close < 0 ? header[3..] : header[3..close]).Split(' ');
        var oldStart = parts[0].TrimStart('-').Split(',')[0];
        var newStart = parts[1].TrimStart('+').Split(',')[0];
        return $"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@{tail}";
    }

    private static async Task<(int Code, string Stdout)> Run(string root, IReadOnlyList<string> arguments, string? input, CancellationToken ct)
    {
        var (code, stdout, _) = input is null
            ? await WorkingTree.GitAsync(root, arguments, Fresh, timeout: null, ct).ConfigureAwait(false)
            : await WorkingTree.GitAsync(root, arguments, Fresh, input, ct).ConfigureAwait(false);
        return (code, stdout);
    }
}
