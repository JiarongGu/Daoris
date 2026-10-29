using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a union merge joins that a reader takes for one paragraph, set apart in a landing's replay (INTEGRATE1c; the merge tool's
/// MERGEJOIN1 and UNIONGLUE1): two branches that each append a note to one decision, or an entry to the task archive, both open it with a
/// blank line; git's union merge keeps that line once and then both, so the second's label or heading lands straight under the first's last
/// line. The merge tool sets one blank line before it; the replay of a landing did not, so a landing that rode a union record left the join
/// for whoever merged it, and <c>doc-duplicates</c> refuses a note label so joined.
/// </summary>
/// <remarks>
/// <para><b>A twin</b> of the merge tool's <c>setApart</c>, <c>setApartHeadings</c>, <c>gluedLabels</c>, <c>gluedHeadings</c> and the files it
/// sets them in (<c>setApartJoins</c>): no code is shared, both hold one table, the driver's tests' <c>fixtures/union-joins.json</c>, row for
/// row (<c>UnionJoinsTests</c>, <c>merge-branch.test.ts</c>), and a change to one is a change to both.</para>
/// <para><b>Which files.</b> The decisions folder <c>daoris.json</c> declares and every path <c>.gitattributes</c> marks <c>merge=union</c> or
/// <c>merge=rows</c>, both read at the line's commit the replay builds on, never the session's copy; and only those the replay changed
/// against the line. A join either side already held (the line's file, the session's own tip's) is not the replay's and stays as it was.</para>
/// </remarks>
internal static partial class UnionJoins
{
    /// <summary>One blank line set before a label or heading: the file, the line it now stands on (1-based) and its text.</summary>
    internal sealed record Set(string Path, int Line, string Label);

    private static readonly Regex[] NoteLabels =
    [
        new(@"^\*\*(As built|Built|Fixed|Read|Amended)\b", RegexOptions.ECMAScript),
        new(@"^\*(As built|Built|Amended)\b", RegexOptions.ECMAScript),
        new(@"^\*\*?(Corrected|Measured|Landed|Proved|Proven|Probed|Reviewed|Superseded|Noted|Note|Revised|Decided|Extended|Accepted)\b.*\d{4}-\d{2}-\d{2}", RegexOptions.ECMAScript),
        new(@"^\*\*[^*]*\b(built|landed|amended)\b[^*]*\d{4}-\d{2}-\d{2}", RegexOptions.ECMAScript),
    ];

    private static readonly Regex Heading = new(@"^#{1,6} ", RegexOptions.ECMAScript);

    /// <summary>Whether a fenced block holds each line, its fences too (<c>doc-duplicates</c>' <c>fenced</c>, as CommonMark reads a fence).</summary>
    internal static bool[] Fenced(IReadOnlyList<string> lines)
    {
        var marker = '\0';
        var length = 0;
        var inFence = new bool[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            var run = trimmed.Length > 0 && trimmed[0] is '`' or '~' ? trimmed[..trimmed.TakeWhile(c => c == trimmed[0]).Count()] : "";
            if (marker == '\0')
            {
                if (run.Length < 3 || (run[0] == '`' && trimmed.IndexOf('`', run.Length) >= 0)) continue;
                (marker, length) = (run[0], run.Length);
                inFence[i] = true;
                continue;
            }

            inFence[i] = true;
            if (run.Length > 0 && run[0] == marker && run.Length >= length && trimmed[run.Length..].Trim().Length == 0) marker = '\0';
        }

        return inFence;
    }

    /// <summary>The lines, by index, that are a decision note's label straight after a non-blank line, outside a fence.</summary>
    internal static IReadOnlyList<int> GluedLabels(IReadOnlyList<string> lines)
    {
        var inFence = Fenced(lines);
        return [.. Enumerable.Range(0, lines.Count).Where(i => i > 0 && !inFence[i] && lines[i - 1].Trim().Length > 0 && NoteLabels.Any(form => form.IsMatch(lines[i])))];
    }

    /// <summary>The lines, by index, that are a markdown heading straight after a non-blank line, outside a fence.</summary>
    internal static IReadOnlyList<int> GluedHeadings(IReadOnlyList<string> lines)
    {
        var inFence = Fenced(lines);
        return [.. Enumerable.Range(0, lines.Count).Where(i => i > 0 && !inFence[i] && lines[i - 1].Trim().Length > 0 && Heading.IsMatch(lines[i]))];
    }

    private static string JoinKey(IReadOnlyList<string> lines, int i) => lines[i - 1].TrimEnd() + "\n" + lines[i].TrimEnd();

    /// <summary>The text with one blank line set before each line of <paramref name="indices"/>, its line ending the line above's.</summary>
    private static (string Text, IReadOnlyList<(int Line, string Label)> Set) Spaced(string text, string[] lines, IReadOnlyList<int> indices)
    {
        var glued = indices.ToHashSet();
        if (glued.Count == 0) return (text, []);
        var output = new List<string>();
        var set = new List<(int, string)>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (glued.Contains(i))
            {
                output.Add(lines[i - 1].EndsWith('\r') ? "\r" : "");
                set.Add((output.Count + 1, lines[i].TrimEnd('\r')));
            }

            output.Add(lines[i]);
        }

        return (string.Join('\n', output), set);
    }

    /// <summary>A decision's text with one blank line set before each note label glued to the line above (MERGEJOIN1).</summary>
    internal static (string Text, IReadOnlyList<(int Line, string Label)> Set) SetApart(string text)
    {
        var lines = text.Split('\n');
        return Spaced(text, lines, GluedLabels(lines));
    }

    /// <summary>
    /// A union record's text with one blank line set before each heading the merge joined to the line above, a join either of
    /// <paramref name="sides"/> already held excepted (UNIONGLUE1).
    /// </summary>
    internal static (string Text, IReadOnlyList<(int Line, string Label)> Set) SetApartHeadings(string text, IReadOnlyList<string> sides)
    {
        var lines = text.Split('\n');
        var held = sides.SelectMany(side =>
        {
            var its = side.Split('\n');
            return GluedHeadings(its).Select(i => JoinKey(its, i));
        }).ToHashSet(StringComparer.Ordinal);
        return Spaced(text, lines, [.. GluedHeadings(lines).Where(i => !held.Contains(JoinKey(lines, i)))]);
    }

    /// <summary>A <c>.gitattributes</c> pattern as git reads it: one with no slash matches at any depth. The merge tool's <c>attributePattern</c>.</summary>
    internal static Regex PatternOf(string pattern) => pattern.Contains('/')
        ? Glob(pattern.TrimStart('/'))
        : new Regex("^(?:.*/)?" + Glob(pattern).ToString()[1..], RegexOptions.CultureInvariant);

    /// <summary>A path glob: <c>*</c> stays in a folder, <c>**</c> crosses folders, <c>{a,b}</c> is either. The merge tool's <c>globToRegExp</c>.</summary>
    internal static Regex Glob(string glob)
    {
        var source = new StringBuilder();
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*')
            {
                if (i + 1 < glob.Length && glob[i + 1] == '*')
                {
                    i++;
                    if (i + 1 < glob.Length && glob[i + 1] == '/')
                    {
                        i++;
                        source.Append("(?:.*/)?");
                    }
                    else
                    {
                        source.Append(".*");
                    }
                }
                else
                {
                    source.Append("[^/]*");
                }
            }
            else if (c == '?')
            {
                source.Append("[^/]");
            }
            else if (c == '{' && glob.IndexOf('}', i) > i)
            {
                var end = glob.IndexOf('}', i);
                source.Append("(?:").Append(string.Join('|', glob[(i + 1)..end].Split(',').Select(Regex.Escape))).Append(')');
                i = end;
            }
            else
            {
                source.Append(Regex.Escape(c.ToString()));
            }
        }

        return new Regex("^" + source + "$", RegexOptions.CultureInvariant);
    }

    /// <summary>The patterns a <c>.gitattributes</c> text marks <c>merge=union</c> or <c>merge=rows</c>, comments aside.</summary>
    internal static IReadOnlyList<string> UnionPatternsOf(string? attributes) =>
        attributes is null ? [] : [.. attributes.Replace("\r\n", "\n").Split('\n')
            .Select(line => UnionLine().Match(line))
            .Where(match => match.Success && !match.Groups[1].Value.StartsWith('#'))
            .Select(match => match.Groups[1].Value)];

    [GeneratedRegex(@"^(\S+)\s(?:.*\s)?merge=(?:union|rows)(?:\s|$)")]
    private static partial Regex UnionLine();

    /// <summary>
    /// The decisions folder a manifest's text declares, without a trailing slash; null where it declares none or is unreadable. Whether it
    /// is a folder is for the caller to ask of the tree (<c>doc-duplicates</c>' <c>records</c> asks the disk).
    /// </summary>
    internal static string? DecisionsOf(string? manifest)
    {
        if (manifest is null) return null;
        try
        {
            using var document = JsonDocument.Parse(manifest);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("documents", out var documents)
                || documents.ValueKind != JsonValueKind.Object || !documents.TryGetProperty("decisions", out var entry))
            {
                return null;
            }

            var path = entry.ValueKind switch
            {
                JsonValueKind.String => entry.GetString(),
                JsonValueKind.Object when entry.TryGetProperty("path", out var named) && named.ValueKind == JsonValueKind.String => named.GetString(),
                _ => null,
            };
            return path?.Replace('\\', '/').TrimEnd('/');
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The joins a replay left, set apart in <paramref name="tree"/> (a replay's own, standing at its new tip) and committed on top as
    /// one commit of Daoris's, so the session's commits stay as replayed: in each file the replay changed against
    /// <paramref name="onto"/> that is a decision of the declared folder, a note label glued to the line above; in each other union
    /// record, a heading glued to it that neither <paramref name="onto"/>'s copy nor <paramref name="session"/>'s (the work's own tip)
    /// held. Nothing where there is none, or git could not say: a join left as it was is what the replay made before.
    /// </summary>
    internal static async Task<IReadOnlyList<Set>> SetApartAsync(string tree, string onto, string session, CancellationToken ct)
    {
        var (listed, changed, _) = await WorkingTree.GitAsync(
            tree, ["--no-optional-locks", "-c", "core.quotePath=false", "diff", "--name-only", "-z", "--diff-filter=d", onto, "HEAD"], ct).ConfigureAwait(false);
        if (listed != 0) return [];
        var paths = changed.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0) return [];

        var union = UnionPatternsOf(await ShowAsync(tree, onto, ".gitattributes", ct).ConfigureAwait(false)).Select(PatternOf).ToList();
        var folder = DecisionsOf(await ShowAsync(tree, onto, "daoris.json", ct).ConfigureAwait(false));
        if (folder is not null)
        {
            // A folder, as `doc-duplicates` asks the disk: a decisions record that is one file holds no note a folder's rule reads.
            var (typed, kind, _) = await WorkingTree.GitAsync(tree, ["cat-file", "-t", $"{onto}:{folder}"], ct).ConfigureAwait(false);
            if (typed != 0 || kind.Trim() != "tree") folder = null;
        }

        var set = new List<Set>();
        foreach (var path in paths)
        {
            var decision = folder is not null && path.StartsWith(folder + "/", StringComparison.Ordinal) && !path[(folder.Length + 1)..].Contains('/')
                && path.EndsWith(".md", StringComparison.Ordinal);
            if (!decision && !union.Any(pattern => pattern.IsMatch(path))) continue;
            var file = Path.Combine(tree, path);
            if (!File.Exists(file)) continue;
            var before = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false));
            var sides = decision ? [] : (await Task.WhenAll(new[] { onto, session }.Select(commit => ShowAsync(tree, commit, path, ct)))).OfType<string>().ToList();
            var (text, apart) = decision ? SetApart(before) : SetApartHeadings(before, sides);
            if (apart.Count == 0) continue;
            await File.WriteAllBytesAsync(file, new UTF8Encoding(false).GetBytes(text), ct).ConfigureAwait(false);
            var (added, _, _) = await WorkingTree.GitAsync(tree, ["add", "--", path], ct).ConfigureAwait(false);
            if (added != 0) return [];
            set.AddRange(apart.Select(each => new Set(path, each.Line, each.Label)));
        }

        if (set.Count == 0) return [];
        var (made, _, _) = await WorkingTree.GitAsync(
            tree, ["commit", "--quiet", "-m", "Set apart what union joined in the replay"], environment: null, SyncBounds.Replay, ct).ConfigureAwait(false);
        return made == 0 ? set : [];
    }

    /// <summary>A file as <paramref name="commit"/> holds it, its bytes read as UTF-8; null where it holds none or git could not say.</summary>
    private static async Task<string?> ShowAsync(string tree, string commit, string path, CancellationToken ct)
    {
        var (found, sha, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", $"{commit}:{path}"], ct).ConfigureAwait(false);
        if (found != 0) return null;
        var (read, text, _) = await WorkingTree.GitAsync(tree, ["cat-file", "blob", sha.Trim()], ct).ConfigureAwait(false);
        return read == 0 ? text : null;
    }
}
