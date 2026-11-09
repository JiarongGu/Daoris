using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A heading union kept twice in a landing's replay, kept once: two branches that each add one entry word for word to a union record
/// (the task archive, the fix log) at places git cannot join leave its heading there twice, which <c>doc-duplicates</c> refuses on the
/// line. Only a heading both sides added since the base is judged, and only where the replay holds it more often than either side did;
/// its later copies go, in a commit on top. Every other line stays as made: a line that is no heading may stand twice in two entries
/// that each carry it (a separator, a shared bullet), so cutting it would cut an entry.
/// </summary>
internal static class UnionOnce
{
    /// <summary>A heading kept once: the file and the heading's text.</summary>
    internal sealed record Kept(string Path, string Line);

    /// <summary>
    /// <paramref name="made"/> with each heading both sides added over <paramref name="base"/> held no more often than the side that held
    /// it most, its later copies cut; and those headings, in the order first met.
    /// </summary>
    internal static (string Text, IReadOnlyList<string> Lines) KeepOnce(string made, string @base, string ours, string theirs)
    {
        var (was, mine, yours, now) = (Counts(@base), Counts(ours), Counts(theirs), Counts(made));
        var most = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (line, count) in now)
        {
            var (b, o, t) = (was.GetValueOrDefault(line), mine.GetValueOrDefault(line), yours.GetValueOrDefault(line));
            if (Heading.IsMatch(line) && o > b && t > b && count > Math.Max(o, t)) most[line] = Math.Max(o, t);
        }

        if (most.Count == 0) return (made, []);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var output = new List<string>();
        foreach (var line in made.Split('\n'))
        {
            var key = line.TrimEnd('\r');
            if (most.TryGetValue(key, out var allowed) && (seen[key] = seen.GetValueOrDefault(key) + 1) > allowed) continue;
            output.Add(line);
        }

        return (string.Join('\n', output), [.. most.Keys]);
    }

    /// <summary>
    /// The headings union kept twice in a replay standing at its new tip in <paramref name="tree"/>, kept once in each union record the
    /// replay changed against <paramref name="onto"/> (its <c>.gitattributes</c> read there, never the work's) and committed on top as one
    /// commit of Daoris's. The sides are the work's base <paramref name="base"/>, the line's <paramref name="onto"/> and the work's own tip
    /// <paramref name="session"/>. Nothing where there is none, or git could not say.
    /// </summary>
    internal static async Task<IReadOnlyList<Kept>> KeepOnceAsync(string tree, string onto, string @base, string session, CancellationToken ct)
    {
        var (listed, changed, _) = await WorkingTree.GitAsync(
            tree, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", "-z", "--diff-filter=d", onto, "HEAD"], ct).ConfigureAwait(false);
        if (listed != 0) return [];
        var attributes = await GitObjectReads.TextAsync(tree, GitObjectName.At(onto, ".gitattributes"), ct).ConfigureAwait(false);
        var union = UnionJoins.UnionPatternsOf(attributes).Select(UnionJoins.PatternOf).ToList();
        var kept = new List<Kept>();
        foreach (var path in changed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(path => union.Any(pattern => pattern.IsMatch(path))))
        {
            var file = Path.Combine(tree, path);
            if (!File.Exists(file)) continue;
            var sides = new List<string>(3);
            foreach (var side in new[] { @base, onto, session })
            {
                sides.Add(await GitObjectReads.TextAsync(tree, GitObjectName.At(side, path), ct).ConfigureAwait(false) ?? "");
            }

            var made = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false));
            var (text, lines) = KeepOnce(made, sides[0], sides[1], sides[2]);
            if (lines.Count == 0) continue;
            await File.WriteAllBytesAsync(file, new UTF8Encoding(false).GetBytes(text), ct).ConfigureAwait(false);
            var (added, _, _) = await WorkingTree.GitAsync(tree, ["add", "--", path], ct).ConfigureAwait(false);
            if (added != 0) return [];
            kept.AddRange(lines.Select(line => new Kept(path, line)));
        }

        if (kept.Count == 0) return [];
        var (committed, _, _) = await WorkingTree.GitAsync(
            tree, ["commit", "--quiet", "-m", DaorisMaker.Of("Keep once what union kept twice in the replay")], environment: null, SyncBounds.Replay, ct)
            .ConfigureAwait(false);
        return committed == 0 ? kept : [];
    }

    /// <summary>The landing's words for the headings it kept once, after the sentence; nothing where it kept none.</summary>
    internal static string Words(IReadOnlyList<Kept> kept) => kept.Count == 0 ? ""
        : " Kept once in a commit on top, where union had kept it twice: " + string.Join(", ", kept.Select(each => $"`{each.Path}`: “{each.Line.TrimEnd()}”")) + ".";

    private static readonly Regex Heading = new(@"^#{1,6} \S", RegexOptions.CultureInvariant);

    // A line is counted with its CR aside: the replay's file is written through the person's line-ending filters, the sides are git's.
    private static Dictionary<string, int> Counts(string text) =>
        text.Split('\n').GroupBy(line => line.TrimEnd('\r'), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}
