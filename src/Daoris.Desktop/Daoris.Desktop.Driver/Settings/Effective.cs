namespace Daoris.Driver;

/// <summary>
/// What a load of <c>driver.json</c> decided its choices from, as a version: the file's content key and, where the store's staffing is
/// laid over it, the staffing's revision read before it. Unversioned where the store failed and the file's staffing answered: the same as
/// no version, so a stamp holding it equals nothing and the look after it runs.
/// </summary>
/// <remarks>No record and no equality of its own: two are one by <see cref="Same"/> alone, so two unversioned never compare equal.</remarks>
public sealed class Effective
{
    private readonly string? _text;

    private Effective(string? text) => _text = text;

    /// <summary>The store's staffing failed at the load, so what it answered is a function of nothing.</summary>
    public static Effective Unversioned { get; } = new(null);

    /// <summary>The file's content key, or <c>absent</c> where no file stands.</summary>
    internal static Effective Of(string content) => new(content);

    internal bool Versioned => _text is not null;

    /// <summary>The same content with the staffing's revision laid over it.</summary>
    internal Effective Staffed(long revision) => _text is { } text ? new($"{text}+{revision}") : this;

    /// <summary>The version as a stamp holds it; an unversioned one has none and throws.</summary>
    internal string Text => _text ?? throw new InvalidOperationException("an unversioned load has no version");

    /// <summary>Whether two are one: false where either is missing or unversioned.</summary>
    public static bool Same(Effective? one, Effective? other) => one?._text is { } a && other?._text is { } b && a == b;
}
