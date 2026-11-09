namespace Daoris.Driver;

/// <summary>
/// Whether a replayed backlog holds what its rows' rules keep (<see cref="BacklogRows.Merge"/>), judged once the replay is made: the
/// rules run only where git stops, and a merge git makes clean can keep a row the line moved while the work archived it, or one id
/// twice. The made backlog must cover each id once and hold exactly the rows the rules keep from the base, the line and the work; a
/// row the rules leave for a person, or a side they refuse, refuses it too. Anything else refuses the replay, said.
/// </summary>
/// <remarks>A twin of <c>tools/merge-branch/backlog-merge.mjs</c>'s <c>rowsHeld</c>, row for row of <c>fixtures/backlog-merges.json</c>.</remarks>
internal static class BacklogHeld
{
    /// <summary>
    /// What the made backlog holds against the rules: the ids it covers twice, the rows it keeps that the rules leave out, those it lost that
    /// the rules keep, and the rules' own refusal where they refuse; it holds where all are empty.
    /// </summary>
    internal sealed record Held(IReadOnlyList<string> Twice, IReadOnlyList<string> Kept, IReadOnlyList<string> Lost, BacklogMerge.Merged? Refused)
    {
        /// <summary>Whether the made backlog holds what the rules keep.</summary>
        internal bool Holds => Twice.Count == 0 && Kept.Count == 0 && Lost.Count == 0 && Refused is null;
    }

    /// <summary>The made backlog <paramref name="made"/> held against the rules over <paramref name="base"/>, the line's and the work's.</summary>
    internal static Held Of(string @base, string ours, string theirs, string made)
    {
        var merged = BacklogRows.Merge(@base, ours, theirs);
        if (merged.Text is null) return new Held([], [], [], merged);
        var (twice, ids) = Covered(made);
        var (_, rules) = Covered(merged.Text);
        return new Held(twice, [.. ids.Where(id => !rules.Contains(id))], [.. rules.Where(id => !ids.Contains(id))], null);
    }

    /// <summary>What a made backlog that does not hold says, after the replay's refusal; nothing where it holds.</summary>
    internal static string Refusal(string path, Held held) =>
        (held.Refused is { } refused ? BacklogRows.Refusal(path, refused) : "")
        + string.Concat(held.Twice.Select(id => $" The replay would leave `{path}` covering the row `{id}` twice."))
        + string.Concat(held.Kept.Select(id => $" The replay would keep `{path}`'s row `{id}`, which one side removed while the other only moved it."))
        + string.Concat(held.Lost.Select(id => $" The replay would lose `{path}`'s row `{id}`, which its rows keep."));

    /// <summary>What holding a made backlog did: its refusal, said, where it does not hold; else what it settled, said; both empty where it held.</summary>
    internal sealed record Holding(string? Refused, string Said, string? Path = null);

    /// <summary>
    /// The backlog the line declares at <paramref name="declaredAt"/>, as made at <paramref name="made"/> (a commit or a tree) from the
    /// work's base, the line's <paramref name="ours"/> and the work's <paramref name="tip"/>, held to its rows in <paramref name="place"/>,
    /// whose index holds the made tree. Where it keeps only rows the rules take out (one side removed each while the other's copy is
    /// the base's or moved), they leave, staged, and committed where <paramref name="commit"/>; any other fault refuses it. Nothing where the
    /// line declares no backlog or a side's does not read, which the rules refused at the stop if they met it.
    /// </summary>
    internal static async Task<Holding> HoldAsync(
        string place, string declaredAt, string @base, string ours, string tip, string made, bool commit, CancellationToken ct)
    {
        var none = new Holding(null, "");
        if (await BacklogMerge.DeclaredAsync(place, declaredAt, ct).ConfigureAwait(false) is not { } backlog) return none;
        var texts = new List<string>(4);
        foreach (var side in new[] { @base, ours, tip, made })
        {
            if (await GitObjectReads.TextAsync(place, GitObjectName.At(side, backlog), ct).ConfigureAwait(false) is not { } read) return none;
            texts.Add(read);
        }

        var held = Of(texts[0], texts[1], texts[2], texts[3]);
        if (held.Holds) return none;
        var refused = new Holding(Refusal(backlog, held), "", backlog);
        // Only whole rows leave: a row that keeps an id the rules take out beside one they keep is the person's.
        var rows = texts[3].Split('\n').Select(line => (Line: line, Ids: BacklogIds.RowOf(line)?.Ids ?? [])).ToList();
        if (held.Refused is not null || held.Twice.Count > 0 || held.Lost.Count > 0
            || rows.Any(row => row.Ids.Any(held.Kept.Contains) && !row.Ids.All(held.Kept.Contains)))
        {
            return refused;
        }

        var text = string.Join('\n', rows.Where(row => row.Ids.Count == 0 || !row.Ids.All(held.Kept.Contains)).Select(row => row.Line));
        if (!await StageAsync(place, backlog, text, ct).ConfigureAwait(false)) return refused;
        if (commit)
        {
            var (committed, _, _) = await WorkingTree.GitAsync(
                place, ["commit", "--quiet", "-m", DaorisMaker.Of("Settle the backlog by its rows in the replay")], environment: null, SyncBounds.Replay, ct)
                .ConfigureAwait(false);
            if (committed != 0) return refused;
        }

        return new Holding(null, string.Concat(held.Kept.Select(id =>
            $" `{backlog}`'s row `{id}` left by its rows, where git had kept it: one side removed it while the other only moved it.")));
    }

    /// <summary><paramref name="text"/> staged at <paramref name="path"/> by its blob in its mode, the working file written from the index; false where git could not.</summary>
    private static async Task<bool> StageAsync(string place, string path, string text, CancellationToken ct)
    {
        var (listed, entry, _) = await WorkingTree.GitAsync(place, ["ls-files", "-s", "-z", "--", path], ct).ConfigureAwait(false);
        if (listed != 0 || entry.TrimEnd('\0').Split(' ') is not [var mode, _, ..]) return false;
        var (hashed, blob, _) = await WorkingTree.GitAsync(place, ["hash-object", "-w", "--stdin"], text, ct).ConfigureAwait(false);
        if (hashed != 0) return false;
        var (staged, _, _) = await WorkingTree.GitAsync(place, ["update-index", "--cacheinfo", $"{mode},{blob.Trim()},{path}"], ct).ConfigureAwait(false);
        if (staged != 0) return false;
        var (written, _, _) = await WorkingTree.GitAsync(place, ["checkout", "--", path], ct).ConfigureAwait(false);
        return written == 0;
    }

    /// <summary>A backlog's ids covered twice, and every id it covers in the order met; a row whose ids do not read covers its key alone.</summary>
    private static (IReadOnlyList<string> Twice, IReadOnlyList<string> Ids) Covered(string text)
    {
        var (twice, covered) = (new List<string>(), new List<string>());
        foreach (var id in text.Split('\n').Select(BacklogIds.RowOf).OfType<BacklogIds.Listed>().SelectMany(row => row.Ids ?? [row.Key]))
        {
            if (!covered.Contains(id)) covered.Add(id);
            else if (!twice.Contains(id)) twice.Add(id);
        }

        return (twice, covered);
    }
}
