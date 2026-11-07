namespace Daoris.Driver;

/// <summary>
/// A child's label as its lineage keeps it: a name to 120 characters, a task's first line to 200. The readings carry the
/// wire's own words; the lineage bounds them as it writes.
/// </summary>
public static class LineageLabels
{
    public const int NameLimit = 120;
    public const int TaskLimit = 200;

    public static string? Name(string? said) => Bounded(said?.Trim(), NameLimit);

    public static string? Task(string? said) => Bounded(said?.Split('\n', 2)[0].Trim(), TaskLimit);

    private static string? Bounded(string? text, int limit)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length <= limit) return text;
        // Never half a surrogate pair.
        return text[..(char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit)];
    }
}
