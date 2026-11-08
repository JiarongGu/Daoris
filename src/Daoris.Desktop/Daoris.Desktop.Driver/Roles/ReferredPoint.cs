namespace Daoris.Driver;

/// <summary>
/// What the supervisor's course act is kept against: the line's repository, the point a <c>refer</c> holds it at (none
/// where none is open), and the counts the keeping read, which a reason's <c>count:</c> source names.
/// </summary>
public sealed record ReferredPoint(string Repository, RuleActed? Open, IReadOnlyList<CountFact>? Counts)
{
    /// <summary>The ids of the line's referred points the rules recorded, the open one among them: a decide naming another is late.</summary>
    public IReadOnlyList<string> Passed { get; init; } = [];

    /// <summary>The person stopped the line or paused its quest (<c>OwnerHolds</c>): no supervisor's act releases it.</summary>
    public bool OwnerHeld { get; init; }
}

/// <summary>
/// The release side of a referred point, as the look reads it at a decision's release: why the point no longer takes the supervisor's
/// act (null where it does), whether the line's session runs a turn now, and the count's own door for the act, given the session and the
/// act. Work orders read both (<c>ReferredPoints</c>), for the go-on marks and the planner's considerations they read are above Roles.
/// </summary>
public sealed record CourseLine(Func<string, string, string?> Why, Func<string, bool> Running, Func<string, string, bool> GoOn);
