namespace Daoris.Driver;

/// <summary>
/// The one order of waiting items: class first (<see cref="PriorityClasses.Rank"/>), then first arrival, read at each
/// look from the items' own inputs. Each queue keeps its own eligibility and only its ties after these; the check slots' queue
/// (<see cref="VerifySlot.Look"/>, <see cref="HeavyExecutor"/>) reads it so far.
/// </summary>
public static class QueueOrder
{
    public static IOrderedEnumerable<T> Of<T>(IEnumerable<T> items, Func<T, string?> @class, Func<T, DateTimeOffset> arrived) =>
        items.OrderBy(item => PriorityClasses.Rank(@class(item))).ThenBy(arrived);
}
