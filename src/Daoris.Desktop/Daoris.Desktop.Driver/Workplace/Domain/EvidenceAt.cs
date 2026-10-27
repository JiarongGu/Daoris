namespace Daoris.Driver;

/// <summary>
/// Where Daoris reads a done's evidence, and why it reads it there (EVID1b, D144 §3): the tree, how the commit was chosen
/// (<see cref="EvidenceCodes.How"/>), the commit (the tree's HEAD where none is named), the tree's base, and the session
/// whose end it reads.
/// </summary>
/// <param name="Tree">The session's own tree, or the root checkout with trees off: where git is asked, and what is uncommitted.</param>
public sealed record EvidenceAt(string Tree, string How)
{
    /// <summary>The commit to read, as the person named it; null reads the tree's HEAD.</summary>
    public string? Commit { get; init; }

    /// <summary>The commit the tree stood at when its session began (SURF6): whether this work changed each path; null reads none.</summary>
    public string? Base { get; init; }

    /// <summary>The session whose end is read; null for the terminal's check.</summary>
    public string? Session { get; init; }
}
