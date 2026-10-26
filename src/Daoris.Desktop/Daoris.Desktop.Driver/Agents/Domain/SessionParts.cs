namespace Daoris.Driver;

/// <summary>
/// One earlier part of the session a further part continues (SESSONE1d, D167 point 4 as amended), as its record and this machine's
/// trees say it: what it worked on and how it stands, and where its work is.
/// </summary>
/// <param name="Session">Its record's id.</param>
/// <param name="Quest">The quest it served.</param>
/// <param name="State">Its record's state, in its public spelling.</param>
public sealed record EarlierPart(string Session, string? Quest, string State)
{
    /// <summary>What its record says of how it stands (its note), or null where it says nothing.</summary>
    public string? Note { get; init; }

    /// <summary>Its tree's name under this machine's trees, where that tree still stands; null where it is gone or ran in the root.</summary>
    public string? Tree { get; init; }

    /// <summary>The branch its work is on: its tree's, or, where its tree is gone, the one its work landed on; null for neither.</summary>
    public string? Branch { get; init; }
}

/// <summary>
/// What a further part of a session is handed of the parts before it (SESSONE1d; the co-working design §1.5): each one's record,
/// newest first, and how its own tree stands to theirs. Agent-neutral facts, never a transcript (design §5).
/// </summary>
/// <param name="Earlier">The earlier parts on this machine, newest first.</param>
public sealed record SessionParts(IReadOnlyList<EarlierPart> Earlier)
{
    /// <summary>The part whose tree this one went into, free (D51); null where it is in a tree of its own.</summary>
    public string? Into { get; init; }

    /// <summary>The live part holding the session's tree, so this one runs beside it; null where none does.</summary>
    public string? HeldBy { get; init; }

    /// <summary>The part whose tree this one would have gone into, gone since (after a landing), so its tree grew again.</summary>
    public string? Gone { get; init; }

    /// <summary>Where <see cref="Gone"/> is set: the branch its tree grew from, the one that work landed on (D82); null for the line.</summary>
    public string? GrewFrom { get; init; }
}
