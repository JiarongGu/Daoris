namespace Daoris.Driver;

/// <summary>
/// What one <c>cat-file --batch</c> question names: an object's id or a name git resolves (<c>HEAD^{commit}</c>), or a
/// <paramref name="Path"/> at the revision <paramref name="Name"/>. A question is one line, so a name holding a line break is never
/// written as one: a path holding one is asked by the id git resolves for it (<see cref="GitObjectReads"/>).
/// </summary>
internal sealed record GitObjectName(string Name, string? Path = null)
{
    public static implicit operator GitObjectName(string name) => new(name);

    /// <summary>The file <paramref name="path"/>, from the top, at the revision <paramref name="revision"/>.</summary>
    internal static GitObjectName At(string revision, string path) => new(revision, path);

    /// <summary>Whether it is one line as git reads one: no line feed, and no carriage return, which git's line reading drops.</summary>
    internal bool Framed => Line(Name) && (Path is null || !Path.AsSpan().ContainsAny('\n', '\r'));

    /// <summary>The question's line; refused for a name that is not one line.</summary>
    internal string Question => Framed
        ? Path is null ? Name : $"{Name}:{Path}"
        : throw new InvalidOperationException("a name holding a line break would be read as two questions");

    internal static bool Line(string text) => text.Length > 0 && !text.AsSpan().ContainsAny('\n', '\r');
}
