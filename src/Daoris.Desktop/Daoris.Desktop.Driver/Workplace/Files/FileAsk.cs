namespace Daoris.Driver;

/// <summary>
/// An act of Daoris's in a tree that a person's <c>ask</c> held at a start, waiting for an allow: the repository whose rules held it, the
/// start that met it, <see cref="What"/> it is (<c>run</c>, <c>copy</c> or <c>share</c>) and its own text (<c>run(npm ci)</c>), never the
/// <c>ask</c> rule's pattern.
/// </summary>
/// <param name="Start">The moment of the first start that met the ask; a later start meeting it again leaves this alone, so the ask's name holds while it waits.</param>
public sealed record FileAsk(string Repository, string Start, string What, string Text)
{
    public const string Run = "run";
    public const string Copy = "copy";
    public const string Share = "share";

    /// <summary>The other repository a share reaches, so its allow stands in both repositories' layers.</summary>
    public IReadOnlyList<string> Others { get; init; } = [];

    /// <summary>Whether the <c>ask</c> that held it names the person, whose own press alone allows it.</summary>
    public bool Person { get; init; }

    /// <summary>The decision a supervisor's allow is being written under; null while the ask waits.</summary>
    public string? Decision { get; init; }

    /// <summary>The role that gave that decision's allow.</summary>
    public string? Role { get; init; }

    /// <summary>The repositories whose layers hold the allow: the one that met the ask and, for a share, the other.</summary>
    public IReadOnlyList<string> Repositories => [Repository, .. Others];

    /// <summary>Whether this is the ask for the act <paramref name="text"/> of kind <paramref name="what"/> in <paramref name="repository"/>, whichever start met it.</summary>
    public bool Is(string repository, string what, string text) =>
        string.Equals(Repository, repository, StringComparison.OrdinalIgnoreCase) && What == what && Text == text;
}
