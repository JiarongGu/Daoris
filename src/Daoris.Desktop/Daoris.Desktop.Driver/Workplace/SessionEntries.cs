namespace Daoris.Driver;

/// <summary>How a part of an entry began (D167, the co-working design §1.3), the words <see cref="EntryPart.Began"/> carries.</summary>
public static class PartBegan
{
    /// <summary>A first start of its quest, or a conversation's own.</summary>
    public const string Opened = "opened";

    /// <summary>Another start of a quest whose earlier session failed.</summary>
    public const string Retried = "retried";

    /// <summary>The quest's session went on once what it waited on closed (D79), or on its own words' record.</summary>
    public const string Resumed = "resumed";

    /// <summary>A start after a cut-off (D80), or a chain's next step (D145).</summary>
    public const string CarriedOn = "carried-on";

    /// <summary>A record that continues another by D163's <c>from</c>: another agent or account, or a conversation started from words.</summary>
    public const string HandedOver = "handed-over";

    /// <summary>
    /// A record that went on from one whose own conversation was proven lost (SESSCTX1b, <see cref="HandOverWhy.Resume"/>; the
    /// session-context design §7): a move of its own, never a hand-over.
    /// </summary>
    public const string Moved = "moved";

    /// <summary>A quest another session published, to this repository: a sibling's request.</summary>
    public const string Asked = "asked";

    /// <summary>How a part with a <c>from</c> link began, by the link's why (SESSCTX1b, the session-context design §7).</summary>
    internal static string ByLink(string? why) => why switch
    {
        HandOverWhy.CarryOn => CarriedOn,
        HandOverWhy.Retry => Retried,
        HandOverWhy.Resume => Moved,
        _ => HandedOver,
    };
}

/// <summary>One record of an entry (D167): a part, with where it is listed and how it began.</summary>
/// <param name="Session">The record's id.</param>
/// <param name="Quest">The quest it serves, or null.</param>
/// <param name="Shown">The word its row shows (<see cref="SessionGrouping.Shown"/>).</param>
/// <param name="Group">The group its own row is in, one of <see cref="SessionGroup"/>.</param>
/// <param name="Began">One of <see cref="PartBegan"/>.</param>
public sealed record EntryPart(string Session, string? Quest, string Shown, string Group, string Began)
{
    /// <summary>The earlier part it follows: what it retried, resumed or carried on, or the session a hand-over continues.</summary>
    public string? After { get; init; }

    /// <summary>For <see cref="PartBegan.Asked"/>: the repository that asked.</summary>
    public string? AskedBy { get; init; }
}

/// <summary>
/// One line of work in one repository (D167, the design §1.1): the unit the person counts, read from the records that share a
/// key.
/// </summary>
/// <param name="Key">The service's key, <c>&lt;origin&gt;/&lt;repository&gt;</c>, read as an opaque string.</param>
/// <param name="Origin">The text before the key's first <c>/</c>: <c>ask:&lt;id&gt;</c>, <c>quest:&lt;id&gt;</c>, <c>session:&lt;id&gt;</c>.</param>
/// <param name="Repository">The text after it: where the line ran, or <c>intake</c> for an ask's intake.</param>
/// <param name="Group">The first of D126's groups any part qualifies for.</param>
/// <param name="Leading">The part whose state put it there: the newest of that group's.</param>
/// <param name="Parts">Its parts, oldest first, so the newest is last.</param>
public sealed record SessionEntry(string Key, string Origin, string Repository, string Group, string Leading, IReadOnlyList<EntryPart> Parts);
