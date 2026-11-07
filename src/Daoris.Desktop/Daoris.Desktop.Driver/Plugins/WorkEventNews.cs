namespace Daoris.Driver;

/// <summary>
/// The session kinds of <c>work/event</c> read from the news the lease's teller claims: a park and an end told once across restarts by the said-set, as the person is told them, and a quest's park from the look's
/// own news. Where that teller's place is a gap, one <see cref="WorkEventKinds.Missed"/> says so.
/// </summary>
public static class WorkEventNews
{
    /// <summary>The event a claimed piece is, with the record it is shown with (its quest and ask), or null for a piece of no such kind.</summary>
    public static WorkEvent? Of(NewsTold piece, SessionView? record) => piece.Kind switch
    {
        AttentionKind.Parked => new WorkEvent(WorkEventKinds.SessionParked, piece.At) { Session = piece.Session, Quest = record?.Quest, Ask = record?.Ask },
        AttentionKind.Ended => new WorkEvent(WorkEventKinds.SessionEnded, piece.At)
        {
            Session = piece.Session, State = piece.State, Quest = record?.Quest, Ask = record?.Ask, Adapter = record?.Adapter,
        },
        _ => null,
    };

    /// <summary>The event a piece of the look's news is: a quest's park, with its last session and its strikes; null for any other.</summary>
    public static WorkEvent? Of(AttentionEvent item, DateTimeOffset at) => item.Kind == AttentionKind.QuestParked
        ? new WorkEvent(WorkEventKinds.QuestParked, at) { Quest = item.Quest, Session = item.Session is { Length: > 0 } session ? session : null, Strikes = item.Strikes }
        : null;

    /// <summary>
    /// <paramref name="teller"/> as the lease's feed runs it, saying its gaps: one <see cref="WorkEventKinds.Missed"/> at the first place
    /// the feed hands it where it kept no cursor, and one before each batch whose history is incomplete. What lay there is not told.
    /// </summary>
    public static JournalTeller Gapped(JournalTeller teller, Action<WorkEvent> tell, Func<DateTimeOffset> clock)
    {
        var loaded = false;
        var unplaced = false;
        return new JournalTeller(
            async batch =>
            {
                if (batch.Missed) tell(new WorkEvent(WorkEventKinds.Missed, clock()));
                await teller.Told(batch).ConfigureAwait(false);
            },
            () =>
            {
                var at = teller.Load?.Invoke();
                if (!loaded) (loaded, unplaced) = (true, at is null);
                return at;
            },
            position =>
            {
                teller.Keep?.Invoke(position);
                if (!unplaced) return;
                unplaced = false;
                tell(new WorkEvent(WorkEventKinds.Missed, clock()));
            });
    }
}
