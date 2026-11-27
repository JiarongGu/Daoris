namespace Daoris.Driver;

/// <summary>
/// The order the asks for a helper take a place under the cap in: the class of the asker's quest, aged from the ask's arrival
/// (<see cref="QueueOrder.Age"/>), then the order asked. An asker the look does not hold is normal and never held.
/// </summary>
internal static class HelperOrder
{
    public static IReadOnlyList<HelperAsk> Of(IReadOnlyList<HelperAsk> asks, Snapshot snapshot)
    {
        if (asks.Count < 2) return asks;
        var look = snapshot.Priority;
        return [.. QueueOrder.Stable(asks, ask =>
        {
            var @class = Of(ask, snapshot) is { } asker ? PriorityBook.OfRun(look.Events, asker.Quest, asker.Ask, look.Now).Class : PriorityClasses.Normal;
            return new QueuePlace(QueueOrder.Age(@class, ask.At, look.Now, look.Rules).Class, QueueStage.Resume, ask.At);
        })];
    }

    /// <summary>The hold that stands the asker's quest or its ask aside, so its ask for a helper is not begun until it is lifted; null where none holds.</summary>
    public static HoldEvent? HoldOf(HelperAsk ask, Snapshot snapshot)
    {
        if (snapshot.Priority.Holds.Count == 0 || Of(ask, snapshot) is not { } asker) return null;
        var word = HoldBook.Reading(snapshot.Priority.Holds, asker.Quest, asker.Ask);
        return word is { Held: true } ? word : null;
    }

    public static bool Held(HelperAsk ask, Snapshot snapshot) => HoldOf(ask, snapshot) is not null;

    private static (string Quest, string? Ask)? Of(HelperAsk ask, Snapshot snapshot)
    {
        var session = snapshot.Active.FirstOrDefault(each => string.Equals(each.Id, ask.Asker, StringComparison.OrdinalIgnoreCase));
        if (session?.Quest is not { Length: > 0 } quest) return null;
        var view = snapshot.Quests.FirstOrDefault(each => string.Equals(each.Id, quest, StringComparison.OrdinalIgnoreCase));
        return (quest, view is null ? null : AskWords.AskOf(view.From));
    }
}
