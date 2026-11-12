namespace Daoris.Driver;

/// <summary>
/// What the person keeps of one conversation (<see cref="ConversationFacts"/>): the name they gave it, when they pinned it, and the earlier
/// conversation it started from with what it is handed, its focus (<see cref="HelpFoci"/>), and when they archived it.
/// <see cref="None"/> where they kept nothing.
/// </summary>
public sealed record HelpKept(string? Name, DateTimeOffset? Pinned, string? From, string? Handed)
{
    public static HelpKept None { get; } = new(null, null, null, null);

    /// <summary>The place its last turn ran under; null until its first words, or one chosen before them.</summary>
    public HelpFocus? Focus { get; init; }

    /// <summary>The place chosen and not yet applied, which its next words apply; null for none.</summary>
    public HelpFocus? FocusNext { get; init; }

    /// <summary>Each place it held, first to last, none twice; null where it kept none.</summary>
    public IReadOnlyList<HelpFocus>? Foci { get; init; }

    /// <summary>Each message that carried a change of focus, first to last (<see cref="HelpFocusMark"/>).</summary>
    public IReadOnlyList<HelpFocusMark> Marks { get; init; } = [];

    /// <summary>When the person archived the conversation; null for one not archived.</summary>
    public DateTimeOffset? Archived { get; init; }

    public bool Equals(HelpKept? other) =>
        other is not null && Name == other.Name && Pinned == other.Pinned && From == other.From && Handed == other.Handed
        && Archived == other.Archived && Equals(Focus, other.Focus) && Equals(FocusNext, other.FocusNext)
        && (Foci ?? []).SequenceEqual(other.Foci ?? []) && Marks.SequenceEqual(other.Marks);

    public override int GetHashCode() => HashCode.Combine(Name, Pinned, From, Handed, Focus, FocusNext);
}
