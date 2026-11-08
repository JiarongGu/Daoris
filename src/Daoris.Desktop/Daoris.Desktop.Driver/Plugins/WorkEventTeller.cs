namespace Daoris.Driver;

/// <summary>
/// The lease's teller of the quests' moves and the channel's messages: each <c>quests</c> row of the journal, which the replay's cache writes in the operation's transaction, and each message
/// kept, mapped to the kind it says (<see cref="ChannelPosts"/> for a message) and handed to its sink in position order. A take, and a move between <c>Open</c> and <c>Taken</c>, says nothing.
/// </summary>
/// <remarks>
/// <para><b>No kept cursor</b>: an event is a wake, never a replay, so the teller's place is its memory and a restart begins at the
/// journal's newest position; its gaps are <see cref="WorkEventNews.Gapped"/>'s, which wraps it.</para>
/// <para>Its sink never waits, so it is the first of its feed's tellers.</para>
/// </remarks>
/// <param name="tell">Where each event goes, in order.</param>
public sealed class WorkEventTeller(Action<WorkEvent> tell)
{
    private readonly object _gate = new();
    private JournalPosition? _place;

    /// <summary>The feed's teller: its batches told, its place loaded from and kept in memory.</summary>
    public JournalTeller Teller => new(TellAsync, Load, Keep);

    public Task TellAsync(JournalBatch batch)
    {
        foreach (var row in batch.Rows)
        {
            if (Of(row) is { } told) tell(told);
        }

        return Task.CompletedTask;
    }

    private JournalPosition? Load()
    {
        lock (_gate) return _place;
    }

    private void Keep(JournalPosition position)
    {
        lock (_gate) _place = position;
    }

    /// <summary>The event a journal row is, or null: a quest begun, closed, or out of its close.</summary>
    internal static WorkEvent? Of(JournalEntry row)
    {
        if (row.Family == "channel") return ChannelPosts.Of(row);
        if (row.Family != "quests") return null;
        var at = DateTimeOffset.FromUnixTimeMilliseconds(row.At);
        var quest = row.Quest ?? row.Record;
        if (row.Change == "opened") return new WorkEvent(WorkEventKinds.QuestPublished, at) { Quest = quest };
        if (row.Change != "state" || Code(row.StateNew) is not { } now) return null;
        if (Closed(now)) return new WorkEvent(WorkEventKinds.QuestClosed, at) { Quest = quest, State = now };
        return Code(row.StateOld) is { } before && Closed(before) ? new WorkEvent(WorkEventKinds.QuestCloseLost, at) { Quest = quest, State = now } : null;
    }

    // The statuses the replay writes, as closed codes; any other is no move of a quest's.
    private static string? Code(string? status) => status?.ToLowerInvariant() switch
    {
        "open" => "open",
        "taken" => "taken",
        "done" => "done",
        "declined" => "declined",
        _ => null,
    };

    private static bool Closed(string code) => code is "done" or "declined";
}
