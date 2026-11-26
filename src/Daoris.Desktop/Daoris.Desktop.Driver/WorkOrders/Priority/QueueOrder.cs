namespace Daoris.Driver;

/// <summary>
/// Where in its work an item waits, the second key of the one order: a landing goes before a run back at its checks, that before
/// a resume, that before first checks, and a new start goes last, so work already begun finishes before more is begun.
/// </summary>
public enum QueueStage
{
    Landing,
    BackAtChecks,
    Resume,
    FirstChecks,
    NewStart,
}

/// <summary>An item's place in a queue, read from its own inputs at each look and never copied into a run.</summary>
/// <param name="Class">The class it is ordered by, aged where it waited past its bound (<see cref="QueueOrder.Age"/>).</param>
public sealed record QueuePlace(string Class, QueueStage Stage, DateTimeOffset Arrived);

/// <summary>A class as ordered: the class, and the one it is promoted from where its wait passed the bound.</summary>
public sealed record AgedClass(string Class, string? From)
{
    /// <summary>What a wait line says of it: <c>aged from normal</c>; null where it is not promoted.</summary>
    public string? Said => From is null ? null : $"aged from {From}";
}

/// <summary>
/// The one order of waiting items: effective class (<see cref="PriorityClasses.Rank"/>), then stage (<see cref="QueueStage"/>), then first
/// arrival, read at each look from the items' own inputs. Each queue keeps only its own eligibility and its own ties after these: the
/// planner's starts and resumes (<see cref="PlanRound"/>) and the check slots' queue (<see cref="VerifySlot.Look"/>, <see cref="HeavyExecutor"/>)
/// read it. Aging is by promotion: an item past its class's bound is ordered one class up and keeps its first arrival.
/// </summary>
public static class QueueOrder
{
    public static IOrderedEnumerable<T> Of<T>(IEnumerable<T> items, Func<T, QueuePlace> place) =>
        items.OrderBy(item => PriorityClasses.Rank(place(item).Class)).ThenBy(item => place(item).Stage).ThenBy(item => place(item).Arrived);

    /// <summary>
    /// The check slots' queue, whose items are all first checks until a run back at its checks is told apart from a first one there: class,
    /// then first arrival.
    /// </summary>
    public static IOrderedEnumerable<T> Of<T>(IEnumerable<T> items, Func<T, string?> @class, Func<T, DateTimeOffset> arrived) =>
        Of(items, item => new QueuePlace(PriorityClasses.All[PriorityClasses.Rank(@class(item))], QueueStage.FirstChecks, arrived(item)));

    /// <summary>
    /// The class an item is ordered by at <paramref name="now"/>: its own, or one up where it has waited past that class's bound
    /// (<see cref="PriorityRules.AgeNormal"/>, <see cref="PriorityRules.AgeLow"/>). High and urgent never age, so nothing ages into urgent,
    /// and an arrival not known never ages an item.
    /// </summary>
    public static AgedClass Age(string? @class, DateTimeOffset? arrived, DateTimeOffset now, PriorityRules rules)
    {
        var own = PriorityClasses.All[PriorityClasses.Rank(@class)];
        return arrived is { } since && rules.BoundOf(own) is { } past && now - since > past
            ? new AgedClass(PriorityClasses.All[PriorityClasses.Rank(own) - 1], own)
            : new AgedClass(own, null);
    }
}
