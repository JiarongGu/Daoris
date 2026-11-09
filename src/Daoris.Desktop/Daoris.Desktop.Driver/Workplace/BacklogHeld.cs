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
        var (twice, keys) = Keys(made);
        var (_, rules) = Keys(merged.Text);
        return new Held(twice, [.. keys.Where(key => !rules.Contains(key))], [.. rules.Where(key => !keys.Contains(key))], null);
    }

    /// <summary>What a made backlog that does not hold says, after the replay's refusal; nothing where it holds.</summary>
    internal static string Refusal(string path, Held held) =>
        (held.Refused is { } refused ? BacklogRows.Refusal(path, refused) : "")
        + string.Concat(held.Twice.Select(id => $" The replay would leave `{path}` covering the row `{id}` twice."))
        + string.Concat(held.Kept.Select(id => $" The replay would keep `{path}`'s row `{id}`, which one side removed while the other only moved it."))
        + string.Concat(held.Lost.Select(id => $" The replay would lose `{path}`'s row `{id}`, which its rows keep."));

    /// <summary>
    /// The refusal of a replay made at <paramref name="made"/> from the work's base <paramref name="base"/>, its tip <paramref name="tip"/>
    /// and the line's <paramref name="onto"/>, by the backlog the line declares there, read in <paramref name="place"/>; null where it holds,
    /// where the line declares none, or where a side's backlog does not read, which the rules refused at the stop if they met it.
    /// </summary>
    internal static async Task<string?> RefusedAsync(string place, string onto, string @base, string tip, string made, CancellationToken ct)
    {
        if (await BacklogMerge.DeclaredAsync(place, onto, ct).ConfigureAwait(false) is not { } backlog) return null;
        var texts = new List<string>(4);
        foreach (var side in new[] { @base, onto, tip, made })
        {
            if (await GitObjectReads.TextAsync(place, GitObjectName.At(side, backlog), ct).ConfigureAwait(false) is not { } text) return null;
            texts.Add(text);
        }

        var held = Of(texts[0], texts[1], texts[2], texts[3]);
        return held.Holds ? null : Refusal(backlog, held);
    }

    /// <summary>A backlog's ids covered twice, and its rows' keys; a row whose ids do not read is known by its key alone.</summary>
    private static (IReadOnlyList<string> Twice, HashSet<string> Keys) Keys(string text)
    {
        var (twice, covered, keys) = (new List<string>(), new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
        foreach (var row in text.Split('\n').Select(BacklogIds.RowOf).OfType<BacklogIds.Listed>())
        {
            keys.Add(row.Key);
            foreach (var id in row.Ids ?? [row.Key])
            {
                if (!covered.Add(id) && !twice.Contains(id)) twice.Add(id);
            }
        }

        return (twice, keys);
    }
}
