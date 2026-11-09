using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The ids a backlog row covers, read from the list its first bold token opens with: one id, or ids joined by commas, slashes and
/// <c>and</c>, whose short parts (a digit run and its tail) take the stem of the id before them, and ranges (<c>to</c>) of one trailing
/// letter or one trailing number. A list that begins with an id but goes on in a way that names no id for certain (a short part two
/// stems could take, a range of anything else) does not read: its ids are null, and whoever reads it refuses rather than guessing.
/// </summary>
/// <remarks>A twin of <c>tools/merge-branch/backlog-merge.mjs</c>'s <c>rowOf</c>, row for row of <c>fixtures/backlog-merges.json</c>.</remarks>
internal static class BacklogIds
{
    /// <summary>A row's list: the id it opens with, its key, and every id it covers; null ids where the list does not read.</summary>
    internal sealed record Listed(string Key, IReadOnlyList<string>? Ids)
    {
        /// <summary>Where the list stands in its line: its first character, and the one after its last.</summary>
        internal (int Start, int End) Span { get; init; }
    }

    /// <summary>A range covers at most this many ids; one wider does not read.</summary>
    internal const int RangeAtMost = 64;

    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private static readonly Regex Id = new(@"\G[A-Z][A-Z0-9-]*[0-9][A-Za-z0-9-]*", Options);

    private static readonly Regex Short = new(@"\G[0-9][A-Za-z0-9-]*", Options);

    private static readonly Regex Separator = new(@"\G(?:, and |, | / | and | to )", Options);

    private static readonly Regex RowStart = new(@"^[-*] (?:\[[ xX]\] )?\*\*", Options);

    private static readonly Regex Digits = new("[0-9]+", Options);

    /// <summary>The list a backlog line's first bold token opens with: null where the line is no row.</summary>
    internal static Listed? RowOf(string line) => RowStart.Match(line) is { Success: true } start ? ListAt(line, start.Length) : null;

    private static Listed? ListAt(string text, int at)
    {
        var first = Id.Match(text, at);
        if (!first.Success) return null;
        var ids = new List<string> { first.Value };
        var last = first.Value;
        var position = at + first.Length;
        while (Separator.Match(text, position) is { Success: true } separator)
        {
            var next = position + separator.Length;
            string? id;
            if (Id.Match(text, next) is { Success: true } full)
            {
                id = full.Value;
                position = next + full.Length;
            }
            else if (Short.Match(text, next) is { Success: true } part)
            {
                id = Expanded(last, part.Value);
                position = next + part.Length;
            }
            else
            {
                break;
            }

            if (id is null) return new Listed(first.Value, null);
            if (separator.Value == " to ")
            {
                if (Range(last, id) is not { } range) return new Listed(first.Value, null);
                ids.AddRange(range.Skip(1));
            }
            else
            {
                ids.Add(id);
            }

            last = id;
        }

        return new Listed(first.Value, ids.Distinct(StringComparer.Ordinal).ToList()) { Span = (at, position) };
    }

    /// <summary>
    /// <paramref name="ids"/> written as a list that reads back as them: each after the first as its shortest part on the stem before
    /// it, the last after <c>and</c>; else each in full; null where neither reads back.
    /// </summary>
    internal static string? Written(IReadOnlyList<string> ids)
    {
        foreach (var shortened in new[] { true, false })
        {
            var parts = ids.Select((id, at) => at > 0 && shortened ? Part(ids[at - 1], id) : id).ToList();
            var list = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
            if (RowOf($"- [ ] **{list}**")?.Ids is { } read && read.SequenceEqual(ids, StringComparer.Ordinal)) return list;
        }

        return null;
    }

    /// <summary>The shortest part of <paramref name="id"/> that the stem of <paramref name="before"/> gives back as it; the whole id where none does.</summary>
    private static string Part(string before, string id) =>
        Digits.Matches(id).Select(run => id[run.Index..]).Where(part => Expanded(before, part) == id).OrderBy(part => part.Length).FirstOrDefault() ?? id;

    /// <summary>A short part (<c>3d</c>) on the stem of <paramref name="before"/> whose digit run equals the part's own: null where none or two do.</summary>
    private static string? Expanded(string before, string part)
    {
        var digits = Digits.Match(part).Value;
        var stems = Digits.Matches(before).Where(run => run.Value == digits).ToList();
        return stems is [var only] ? before[..only.Index] + part : null;
    }

    /// <summary>The ids from <paramref name="from"/> to <paramref name="to"/>: one trailing letter apart, or one trailing number; null otherwise.</summary>
    private static IReadOnlyList<string>? Range(string from, string to)
    {
        var common = 0;
        while (common < from.Length && common < to.Length && from[common] == to[common]) common++;
        var (stem, a, b) = (from[..common], from[common..], to[common..]);
        if (a.Length == 1 && b.Length == 1 && char.IsAsciiLetterLower(a[0]) && char.IsAsciiLetterLower(b[0]) && a[0] < b[0])
        {
            return [.. Enumerable.Range(a[0], b[0] - a[0] + 1).Select(letter => stem + (char)letter)];
        }

        // A number's run is read whole: the common part gives back the digits it took from it.
        while (common > 0 && char.IsAsciiDigit(from[common - 1])) common--;
        (stem, a, b) = (from[..common], from[common..], to[common..]);
        if (a.Length is > 0 and < 6 && b.Length is > 0 and < 6 && a.All(char.IsAsciiDigit) && b.All(char.IsAsciiDigit)
            && a[0] != '0' && b[0] != '0' && int.Parse(a) is var low && int.Parse(b) is var high && low < high && high - low < RangeAtMost)
        {
            return [.. Enumerable.Range(low, high - low + 1).Select(number => stem + number)];
        }

        return null;
    }
}
