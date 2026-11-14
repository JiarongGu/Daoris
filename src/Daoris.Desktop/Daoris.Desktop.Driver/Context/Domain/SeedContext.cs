using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A session's context entries, written from the line a brief is composed from: what was asked, where the work stands and why it moves, what
/// the tree holds, the files the line read and the memory it was handed, and the outgoing agent's note as its claim. A seed tells them after
/// the conversation as the host's record; the brief's text is the floor's.
/// </summary>
internal static class SeedContext
{
    /// <summary>The most one entry weighs, in characters, the start kept.</summary>
    private const int ItemCap = 4_000;

    /// <summary>The most items a list names: plan steps, waits, files, commits; the rest counted.</summary>
    private const int ListCap = 20;

    /// <summary>The entries of <paramref name="line"/>, oldest kind first.</summary>
    public static IReadOnlyList<ContextEntry> Of(BriefLine line)
    {
        var events = line.Parts.SelectMany(part => part.Events).ToList();
        List<ContextEntry> entries = [];
        if (events.FirstOrDefault(e => e.Kind == SessionEventKind.User && !string.IsNullOrWhiteSpace(e.Text)) is { Text: { } asked })
        {
            entries.Add(new ContextEntry(ContextKind.Work, Clip(asked)));
        }

        var standing = new StringBuilder($"It goes on in a new part because {HandOverWhy.Noted(line.Why)}.");
        if (events.LastOrDefault(e => e.Kind == SessionEventKind.Plan && e.Entries is { Count: > 0 }) is { Entries: { } plan })
        {
            standing.Append("\nIts last plan:").Append(List(plan.Select(step => $"[{step.Status ?? "pending"}] {step.Content}")));
        }

        if (line.Open.Count > 0) standing.Append("\nIt waits on:").Append(List(line.Open.Select(wait => $"{wait.To}: {wait.Detail ?? wait.Outcome}")));
        entries.Add(new ContextEntry(ContextKind.Standing, Clip(standing.ToString())));

        if (line.State is { } tree) entries.Add(new ContextEntry(ContextKind.Tree, Clip(Tree(tree))));
        if (Read(events, line.Tree) is { Count: > 0 } read)
        {
            entries.Add(new ContextEntry(ContextKind.Found, Clip("The line read these files:" + List(read))) { Subject = "files read" });
        }

        entries.AddRange(line.Memory.Take(ListCap).Select(memory => new ContextEntry(ContextKind.Found, Clip(memory)) { Subject = "memory" }));
        if (line.Note?.Trim() is { Length: > 0 } note) entries.Add(new ContextEntry(ContextKind.Standing, Clip(note)) { Claim = true });
        return entries;
    }

    private static string Tree(BriefTree tree)
    {
        var said = new StringBuilder(tree.Head is { } head ? $"Its head is {head}." : "Its head could not be read.");
        said.Append(tree.Uncommitted.Count == 0 ? "\nNothing is uncommitted." : "\nUncommitted:" + List(tree.Uncommitted));
        if (tree.Commits.Count > 0) said.Append("\nCommitted since the line began, newest first:").Append(List(tree.Commits));
        return said.ToString();
    }

    /// <summary>Each file the line's read calls name, relative to its tree (one outside it by its name alone), with how often it was read.</summary>
    private static List<string> Read(IEnumerable<SessionEvent> events, string? tree)
    {
        var reads = events
            .Where(e => e.Kind == SessionEventKind.Tool && e.ToolKind == "read" && e.Locations is { Count: > 0 })
            .SelectMany(e => e.Locations!.Select(path => (Call: e.Id ?? e.Seq.ToString(System.Globalization.CultureInfo.InvariantCulture), Path: Named(path, tree))))
            .Distinct()
            .GroupBy(read => read.Path, StringComparer.Ordinal);
        return [.. reads.Select(group => group.Count() > 1 ? $"{group.Key} ({group.Count()} times)" : group.Key)];
    }

    private static string Named(string path, string? tree)
    {
        if (tree is { Length: > 0 } && Path.IsPathRooted(path))
        {
            var relative = Path.GetRelativePath(tree, path);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative)) return relative.Replace('\\', '/');
        }

        return Path.IsPathRooted(path) ? Path.GetFileName(path) : path.Replace('\\', '/');
    }

    private static string List(IEnumerable<string> items)
    {
        var all = items.ToList();
        var said = string.Concat(all.Take(ListCap).Select(item => $"\n- {item}"));
        return all.Count > ListCap ? said + $"\n- and {all.Count - ListCap} more" : said;
    }

    private static string Clip(string text) => text.Length <= ItemCap ? text : text[..ItemCap] + " …";
}
