namespace Daoris.Driver;

/// <summary>What an entry of a session's context is about: the five kinds a session carried from one agent to the next needs.</summary>
public enum ContextKind
{
    /// <summary>What was asked, in the host's words.</summary>
    Work,

    /// <summary>A choice that was made, and why.</summary>
    Decided,

    /// <summary>Something that was learned, and where, where that is known.</summary>
    Found,

    /// <summary>Where the work stands now, and why it moves.</summary>
    Standing,

    /// <summary>What the tree holds right now.</summary>
    Tree,
}

/// <summary>One entry of a session's context as Daoris writes it from the session's records, which a seed tells after the conversation.</summary>
/// <param name="Kind">What it is about.</param>
/// <param name="Text">The entry, not blank.</param>
public sealed record ContextEntry(ContextKind Kind, string Text)
{
    /// <summary>A few words saying what it is about, such as a path; null where there is none.</summary>
    public string? Subject { get; init; }

    /// <summary>An agent's own words, told as its claim; false for Daoris's own record.</summary>
    public bool Claim { get; init; }
}
