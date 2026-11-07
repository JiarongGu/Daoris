using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>One conflict of a file: what each side holds there, and a line or two of the text around it, without their line ends.</summary>
public sealed record ConflictHunk(string Id, IReadOnlyList<string> Before, IReadOnlyList<string> Ours, IReadOnlyList<string> Theirs, IReadOnlyList<string> After);

/// <summary>
/// A file in conflict as the page reads it: the index's three stages (<paramref name="Base"/>, <paramref name="Ours"/>,
/// <paramref name="Theirs"/> as <c>mode id</c>), the worktree file's id, and its conflicts. <paramref name="Why"/> is set, with no hunks, where the
/// file cannot be resolved hunk by hunk here (not in conflict, one side deleted, binary, not UTF-8 text).
/// </summary>
public sealed record FileConflict(
    string Path, string? Head, string Base, string Ours, string Theirs, string Worktree, string? Why, IReadOnlyList<ConflictHunk> Hunks);

/// <summary>
/// **A file in conflict, read by its stages**. The three stages are merged by
/// <c>git merge-file --object-id</c> (no merge driver, no attribute, the configuration's conflict style set aside), whose marked output is read
/// back as text between conflicts and the two sides of each conflict. The text is the stages' own bytes read as UTF-8; a file that is not is not
/// resolved here.
/// </summary>
public static class GitConflicts
{
    private const int MarkerSize = 15;

    private static readonly string Open = new('<', MarkerSize);
    private static readonly string Middle = new('=', MarkerSize);
    private static readonly string Close = new('>', MarkerSize);

    private static readonly IReadOnlyList<string> Commands =
        ["git ls-files -u -- <path>", "git merge-file --object-id -p <ours> <base> <theirs>"];

    /// <summary>What a press must find unchanged: HEAD, the stages as they stand, and the worktree file's id.</summary>
    internal sealed record Faces(string? Head, IReadOnlyList<ActEntry> Stages, string Worktree);

    /// <summary>The text of a file as the stages merge it: its pieces in order, each a stretch of text or a conflict.</summary>
    internal sealed record Merged(IReadOnlyList<Piece> Pieces);

    internal sealed record Piece(string Text, string? Ours, string? Theirs);

    internal static async Task<Faces?> FacesAsync(string root, string path, CancellationToken ct)
    {
        var head = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", "HEAD"], ct).ConfigureAwait(false);
        var tip = head.Code == 0 ? head.Stdout.Trim() : null;
        if (await ActIndex.EntriesAsync(root, [path], null).ConfigureAwait(false) is not { } entries) return null;
        var file = System.IO.Path.Combine(root, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!File.Exists(file)) return new(tip, [.. entries.Where(entry => entry.Stage > 0)], "");
        var hashed = await WorkingTree.GitAsync(root, ["hash-object", "--path=" + path, "--", path], ct).ConfigureAwait(false);
        return hashed.Code == 0 ? new(tip, [.. entries.Where(entry => entry.Stage > 0)], hashed.Stdout.Trim()) : null;
    }

    public static async Task<GitAnswer<FileConflict>> ReadAsync(string root, string path, CancellationToken ct = default)
    {
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false))
        {
            return new(null, GitReadRefusal.NotTop, "This folder is not the top of a repository of its own, so git is not asked.") { Commands = Commands };
        }

        if (path.Length == 0 || System.IO.Path.IsPathRooted(path) || path.Split('/', '\\').Contains(".."))
        {
            return new(null, GitReadRefusal.NotAPath, "That is not a path in the repository.") { Commands = Commands };
        }

        if (await FacesAsync(root, path, ct).ConfigureAwait(false) is not { } faces)
        {
            return new(null, GitReadRefusal.NotAnswered, "git could not read this file's conflict: it may be busy, or git did not start.") { Commands = Commands };
        }

        string Stage(int number) => faces.Stages.FirstOrDefault(entry => entry.Stage == number) is { } entry ? $"{entry.Mode} {entry.Id}" : "";
        FileConflict Said(string why) => new(path, faces.Head, Stage(1), Stage(2), Stage(3), faces.Worktree, why, []);
        if (faces.Stages.Count == 0) return Answered(Said($"`{path}` is not in conflict."));
        if (Stage(1).Length == 0 || Stage(2).Length == 0 || Stage(3).Length == 0)
        {
            return Answered(Said($"`{path}` is in conflict with a side that has no file or no common ancestor, which is resolved by keeping or removing it, not hunk by hunk."));
        }

        var (merged, why) = await MergeAsync(root, faces, ct).ConfigureAwait(false);
        if (merged is null) return Answered(Said(why!));
        var hunks = new List<ConflictHunk>();
        for (var at = 0; at < merged.Pieces.Count; at++)
        {
            if (merged.Pieces[at].Ours is not { } ours) continue;
            var theirs = merged.Pieces[at].Theirs!;
            var before = at > 0 ? Lines(merged.Pieces[at - 1].Text).TakeLast(2) : [];
            var after = at + 1 < merged.Pieces.Count ? Lines(merged.Pieces[at + 1].Text).Take(2) : [];
            hunks.Add(new ConflictHunk(IdOf(hunks.Count, ours, theirs), [.. before], Lines(ours), Lines(theirs), [.. after]));
        }

        return Answered(new FileConflict(path, faces.Head, Stage(1), Stage(2), Stage(3), faces.Worktree, hunks.Count == 0 ? $"`{path}` has no conflicting hunk to choose: the stages merge without one." : null, hunks));
    }

    private static GitAnswer<FileConflict> Answered(FileConflict value) => new(value, GitReadRefusal.None, null) { Commands = Commands };

    /// <summary>The file's text as <paramref name="faces"/>' stages merge, or why they cannot be read as text.</summary>
    internal static async Task<(Merged? Merged, string? Why)> MergeAsync(string root, Faces faces, CancellationToken ct)
    {
        var (ours, basis, theirs) = (faces.Stages.First(entry => entry.Stage == 2), faces.Stages.First(entry => entry.Stage == 1), faces.Stages.First(entry => entry.Stage == 3));
        var (code, text, err) = await WorkingTree.GitAsync(
            root, ["-c", "merge.conflictStyle=merge", "merge-file", "--object-id", "-p", $"--marker-size={MarkerSize}", "-L", "ours", "-L", "base", "-L", "theirs", ours.Id, basis.Id, theirs.Id],
            environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        // merge-file's exit is the number of conflicts, up to 127; more is an error (a binary file among them).
        if (code is < 0 or > 127) return (null, err.Contains("binary", StringComparison.OrdinalIgnoreCase) ? "It is a binary file, which is resolved by keeping one side whole." : $"git could not merge its stages ({ContentHolds.Said(err)}).");
        if (text.Contains((char)0xFFFD) || text.Contains((char)0)) return (null, "It is not UTF-8 text, so it is resolved by hand, not hunk by hunk.");
        if (text.StartsWith((char)0xFEFF)) return (null, "It begins with a byte-order mark, so it is resolved by hand, not hunk by hunk.");
        return Parse(text) is { } parsed ? (parsed, null) : (null, "Its conflict markers could not be told from its text, so it is resolved by hand.");
    }

    /// <summary>The marked output as pieces: text between the markers, and each conflict's two sides; null where the markers do not pair.</summary>
    internal static Merged? Parse(string marked)
    {
        var pieces = new List<Piece>();
        var text = new StringBuilder();
        StringBuilder? ours = null;
        StringBuilder? theirs = null;
        var inTheirs = false;
        foreach (var line in marked.SplitAfter('\n'))
        {
            var bare = line.TrimEnd('\n', '\r');
            if (ours is null && bare == Open + " ours")
            {
                pieces.Add(new Piece(text.ToString(), null, null));
                text.Clear();
                (ours, theirs, inTheirs) = (new StringBuilder(), new StringBuilder(), false);
            }
            else if (ours is not null && !inTheirs && bare == Middle)
            {
                inTheirs = true;
            }
            else if (ours is not null && inTheirs && bare == Close + " theirs")
            {
                pieces.Add(new Piece("", ours.ToString(), theirs!.ToString()));
                (ours, theirs, inTheirs) = (null, null, false);
            }
            else if (ours is not null)
            {
                (inTheirs ? theirs! : ours).Append(line);
            }
            else
            {
                text.Append(line);
            }
        }

        if (ours is not null) return null;
        pieces.Add(new Piece(text.ToString(), null, null));
        return new Merged(pieces);
    }

    /// <summary>The text with each conflict replaced by the side or sides its pick takes; null where a conflict has no pick or one is stray.</summary>
    internal static string? Resolved(Merged merged, IReadOnlyList<ResolvePick> picks)
    {
        var result = new StringBuilder();
        var next = 0;
        foreach (var piece in merged.Pieces)
        {
            if (piece.Ours is not { } ours)
            {
                result.Append(piece.Text);
                continue;
            }

            var pick = picks.FirstOrDefault(each => each.Id == IdOf(next, ours, piece.Theirs!));
            next++;
            if (pick is null) return null;
            result.Append(pick.Take switch
            {
                ResolveTakes.Ours => ours,
                ResolveTakes.Theirs => piece.Theirs,
                ResolveTakes.Both => ours + piece.Theirs,
                _ => null,
            } ?? throw new InvalidOperationException(pick.Take));
        }

        return result.ToString();
    }

    /// <summary>How many conflicts a merge holds.</summary>
    internal static int Count(Merged merged) => merged.Pieces.Count(piece => piece.Ours is not null);

    private static string IdOf(int at, string ours, string theirs) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{at}\0{ours}\0{theirs}")))[..12];

    private static List<string> Lines(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    private static IEnumerable<string> SplitAfter(this string text, char separator)
    {
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf(separator, start);
            var stop = end < 0 ? text.Length : end + 1;
            yield return text[start..stop];
            start = stop;
        }
    }
}
