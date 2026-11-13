namespace Daoris.Knowledge;

/// <summary>Why a plan is not kept.</summary>
public enum TeamRefusal
{
    /// <summary>It is kept.</summary>
    None,

    /// <summary>No such ask.</summary>
    NotFound,

    /// <summary>The ask is closed.</summary>
    Closed,

    /// <summary>The plan moved since the writer read it.</summary>
    Moved,

    /// <summary>The plan has no shape to keep, or names a part that is not there.</summary>
    Unfit,
}

/// <summary>One part's publish: the quest it is, or why it has none yet.</summary>
public sealed record TeamPublished(string Part, string? Quest, string? Note);

/// <summary>What a plan door answered: its sentence, the revision it kept, and each part's quest.</summary>
public sealed record TeamOutcome(TeamRefusal Refusal, string Message, TeamPlan? Plan = null)
{
    /// <summary>Each part's quest, for the plan's own parts.</summary>
    public IReadOnlyList<TeamPublished> Published { get; init; } = [];
}
