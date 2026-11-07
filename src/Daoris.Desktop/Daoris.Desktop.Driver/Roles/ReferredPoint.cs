namespace Daoris.Driver;

/// <summary>
/// What the supervisor's course act is kept against (CONTEXTDRIVE1j): the line's repository, the point a <c>refer</c> holds it at (none
/// where none is open), and the counts the keeping read, which a reason's <c>count:</c> source names.
/// </summary>
public sealed record ReferredPoint(string Repository, RuleActed? Open, IReadOnlyList<CountFact>? Counts);

/// <summary>
/// The release side of a referred point, as the look reads it at a course decision's release: why the point no longer takes the
/// supervisor's go-on (null where it does), whether the line's session runs a turn now, and the count's own door. Course reads both
/// (<c>ReferredPoints</c>), for the go-on marks the count keeps are its own.
/// </summary>
public sealed record CourseLine(Func<string, string, string?> Why, Func<string, bool> Running, Func<string, bool> GoOn);
