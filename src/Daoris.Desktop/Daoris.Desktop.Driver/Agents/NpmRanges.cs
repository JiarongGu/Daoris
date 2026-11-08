namespace Daoris.Driver;

/// <summary>
/// An npm version range read as npm reads it, so an installed version can be tested against what a server declared:
/// <c>^</c>, <c>~</c>, the comparators, x-ranges, <c>A - B</c> and <c>||</c>. Numbers only: a word (a tag) and a range
/// naming a prerelease or a build are no range here, and the caller keeps its own way for them.
/// </summary>
public static class NpmRanges
{
    /// <summary>Whether <paramref name="range"/> is one this reads.</summary>
    public static bool Reads(string range) => Parse(range) is not null;

    /// <summary>Whether the exact <paramref name="version"/> is inside <paramref name="range"/>; false where the range is not one this reads.</summary>
    public static bool Satisfies(string range, string version) =>
        Parse(range) is { } sets && Numbers(version) is { } at && sets.Any(set => set.All(bound => bound.Holds(at)));

    private sealed record Bound(Version? Low, Version? High)
    {
        public bool Holds(Version at) => (Low is null || at >= Low) && (High is null || at < High);
    }

    private static readonly string[] Operators = [">=", "<=", "~>", ">", "<", "=", "^", "~"];

    private static List<List<Bound>>? Parse(string range)
    {
        var sets = new List<List<Bound>>();
        foreach (var written in range.Split("||"))
        {
            var set = Set(written.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (set is null) return null;
            sets.Add(set);
        }

        return sets;
    }

    private static List<Bound>? Set(string[] words)
    {
        if (words.Length == 0) return null;
        if (words is [var from, "-", var to])
        {
            if (Partial(from) is not { } low || Partial(to) is not { } high) return null;
            return [new Bound(low[0] is null ? null : Fill(low), High(high))];
        }

        var bounds = new List<Bound>();
        for (var at = 0; at < words.Length; at++)
        {
            var word = words[at];
            if (Operators.Contains(word) && at + 1 < words.Length) word += words[++at];
            if (Comparator(word) is not { } bound) return null;
            bounds.Add(bound);
        }

        return bounds;
    }

    private static Bound? Comparator(string word)
    {
        var op = Operators.FirstOrDefault(word.StartsWith) ?? "";
        if (Partial(word[op.Length..]) is not { } part) return null;
        var any = part[0] is null;
        switch (op)
        {
            case "" or "=":
                return any ? new Bound(null, null) : new Bound(Fill(part), High(part));
            case ">=":
                return new Bound(any ? null : Fill(part), null);
            case ">":
                return any ? null : new Bound(High(part), null);
            case "<":
                return any ? null : new Bound(null, Fill(part));
            case "<=":
                return new Bound(null, High(part));
            case "~" or "~>":
                var (tildeMajor, tildeMinor) = (part[0] ?? 0, part[1]);
                return any ? new Bound(null, null) : new Bound(Fill(part), tildeMinor is { } m ? new Version(tildeMajor, m + 1, 0) : new Version(tildeMajor + 1, 0, 0));
            default:
                if (any) return new Bound(null, null);
                var (major, minor, patch) = (part[0]!.Value, part[1], part[2]);
                var high = major > 0 || minor is null ? new Version(major + 1, 0, 0)
                    : minor > 0 || patch is null ? new Version(0, minor!.Value + 1, 0)
                    : new Version(0, 0, patch.Value + 1);
                return new Bound(Fill(part), high);
        }
    }

    /// <summary>The first version above everything a partial version names: the next major, minor or patch, null for <c>*</c>.</summary>
    private static Version? High(int?[] part) =>
        part[0] is not { } major ? null
        : part[1] is not { } minor ? new Version(major + 1, 0, 0)
        : part[2] is not { } patch ? new Version(major, minor + 1, 0)
        : new Version(major, minor, patch + 1);

    private static Version Fill(int?[] part) => new(part[0] ?? 0, part[1] ?? 0, part[2] ?? 0);

    /// <summary>One to three numbers or wildcards, a wildcard only after the numbers; null where the word is anything else.</summary>
    private static int?[]? Partial(string word)
    {
        var parts = word.TrimStart('v').Split('.');
        if (parts.Length is 0 or > 3) return null;
        var numbers = new int?[3];
        var wild = false;
        for (var at = 0; at < parts.Length; at++)
        {
            if (parts[at] is "x" or "X" or "*") wild = true;
            else if (wild || parts[at] is not { Length: > 0 and <= 9 } digits || !digits.All(char.IsAsciiDigit)) return null;
            else numbers[at] = int.Parse(digits);
        }

        return numbers;
    }

    private static Version? Numbers(string version) =>
        Partial(version) is { } part && part[0] is not null && !version.Contains('x') && !version.Contains('X') && !version.Contains('*')
            ? Fill(part)
            : null;
}
