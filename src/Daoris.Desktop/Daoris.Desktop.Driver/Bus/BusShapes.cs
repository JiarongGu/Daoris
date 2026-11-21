namespace Daoris.Driver;

/// <summary>
/// The shapes a payload's field is declared with (<see cref="BusField"/>): ids, codes, counts and times, so words, diffs and output stay in
/// their records, referenced by id. Null fits every shape: absent evidence stays unknown.
/// </summary>
public static class BusShapes
{
    /// <summary>An id: short text (<see cref="Short"/>).</summary>
    public const string Id = "id";

    /// <summary>A code from a closed set, as short text (<see cref="Short"/>).</summary>
    public const string Code = "code";

    /// <summary>A whole number.</summary>
    public const string Count = "count";

    /// <summary>A finite number.</summary>
    public const string Number = "number";

    /// <summary>A moment, kept as milliseconds since the epoch.</summary>
    public const string Time = "time";

    /// <summary>Yes or no.</summary>
    public const string Flag = "flag";

    /// <summary>A short list of ids, at most <see cref="MostListed"/>.</summary>
    public const string Ids = "ids";

    public static readonly IReadOnlyList<string> All = [Id, Code, Count, Number, Time, Flag, Ids];

    /// <summary>The ids of one payload list kept, the rest counted by its writer.</summary>
    public const int MostListed = 16;

    /// <summary>The longest id or code a payload or an envelope's field carries.</summary>
    public const int Longest = 200;

    /// <summary>Whether <paramref name="value"/> has <paramref name="shape"/>; null has every shape.</summary>
    public static bool Fits(string shape, object? value) => value is null || shape switch
    {
        Id or Code => value is string text && Short(text),
        Count => value is int or long,
        Number => value is int or long || (value is double number && double.IsFinite(number)),
        Time => value is DateTimeOffset,
        Flag => value is bool,
        Ids => value is IReadOnlyList<string> ids && ids.Count <= MostListed && ids.All(id => Short(id)),
        _ => false,
    };

    /// <summary>
    /// What a value of <paramref name="shape"/> keeps and how many values it drops: a value that fits whole, or null and one dropped; a list
    /// keeps the ids that fit, at most <see cref="MostListed"/>, each other one dropped, and is null where it had some and none fit.
    /// </summary>
    public static (object? Value, int Dropped) Admit(string shape, object? value)
    {
        if (shape != Ids || value is not IReadOnlyList<string> ids) return Fits(shape, value) ? (value, 0) : (null, 1);
        var fit = ids.Where(id => Short(id)).Take(MostListed).ToList();
        return (fit.Count == 0 && ids.Count > 0 ? null : fit, ids.Count - fit.Count);
    }

    /// <summary>
    /// An id or code: well-formed UTF-16 no longer than <see cref="Longest"/>, with no control character and no whitespace (so no sentence,
    /// whose words have spaces between them), not shaped like a path (a backslash, <c>:/</c> as a drive or an address has, a leading
    /// <c>/</c> or <c>~</c>, two or more <c>/</c>, or a <c>.</c> or <c>..</c> step) and not like an e-mail address (a name, <c>@</c>, then a
    /// dotted host). Machine paths, addresses and words stay in their records, referenced by id. Null is none.
    /// </summary>
    public static bool Short(string? text) =>
        text is null
        || (text.Length <= Longest && WellFormed(text) && !text.Any(letter => char.IsControl(letter) || char.IsWhiteSpace(letter)) && !PathShaped(text)
            && !MailShaped(text));

    private static bool WellFormed(string text)
    {
        for (var at = 0; at < text.Length; at++)
        {
            if (char.IsHighSurrogate(text[at]) && at + 1 < text.Length && char.IsLowSurrogate(text[at + 1])) at++;
            else if (char.IsSurrogate(text[at])) return false;
        }

        return true;
    }

    private static bool PathShaped(string text) =>
        text.Contains('\\') || text.Contains(":/", StringComparison.Ordinal) || text.StartsWith('/') || text.StartsWith('~')
        || text.Count(letter => letter == '/') >= 2 || text.Split('/').Any(step => step is "." or "..");

    private static bool MailShaped(string text)
    {
        var at = text.IndexOf('@');
        if (at <= 0 || at + 2 >= text.Length) return false;
        var dot = text.IndexOf('.', at + 2);
        return dot > 0 && dot < text.Length - 1;
    }
}
