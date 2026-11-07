using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a word said now to a session would do (<c>SESSION_QUEUE</c>), for the page to offer the box by, or the line
/// saying why nothing takes words: known here where this machine runs the session, else read off its record.
/// </summary>
internal sealed class WordsReach(DriverLoop loop, Func<SessionRecord, bool> takeWaits)
{
    /// <summary>
    /// What a word said now to a session that runs here would do, known without reading its record: an open door's
    /// reach, or a conversation's turns. Null where only the record can say: nothing here runs it, or its door
    /// has closed and it winds up.
    /// </summary>
    public WordsAnswer? Here(string id)
    {
        // The replacement a refused start moved to answers for it; moving there, a word joins its words.
        if (loop.Chat is { } replaced)
        {
            id = replaced.CurrentOf(id);
            if (replaced.Replacing(id)) return new(true, null, null);
        }

        // What the door can do while a turn runs rides it: whether its agent steers, and whether it can stop a turn
        // keeping the session, which the page offers the levels and the keeping stop by.
        if (loop.Processes.InboxOf(id) is { State.Taking: true } inbox)
        {
            return new(true, Spelled(inbox.Reach), null) { Door = inbox.Reach is null ? null : new TurnDoor(inbox.Steers, inbox.KeepingStop) };
        }

        // Ask Daoris's conversation still starting: a word waits for its door, which opens with it as its first turn.
        if (loop.HelpStarting.Queue(id) is not null) return new(true, null, null);

        if (loop.Processes.Running.Contains(id, StringComparer.OrdinalIgnoreCase) && loop.Processes.RefusesInput(id) is null)
        {
            // A conversation this machine runs: a word goes into the running turn where its agent takes words at its
            // next step, waits for the turn to end elsewhere, or starts one now.
            return new(true, loop.Chat?.Reach(id), null) { Door = loop.Chat?.Door(id) };
        }

        return null;
    }

    /// <summary>
    /// What a word said now to this session would do, here or off its record. Never a refusal: a page asks of every conversation
    /// it shows, and a service that does not answer is nothing known, never a claim either way.
    /// </summary>
    public async Task<WordsAnswer> Async(string id, CancellationToken ct)
    {
        if (Here(id) is { } here) return here;
        if (id.Contains('/')) return WordsAnswer.Refused("teammate");

        try
        {
            if (await WordsRead.OfAsync(loop, id, ct).ConfigureAwait(false) is not { } read) return new(false, null, null);
            if (read.Record is not { } record) return WordsAnswer.Refused(WordsNever.NotFound);
            // The person's words waiting on it, which the queue lists where nothing here holds it.
            var waiting = WordsRead.WaitingOn(read.Json, id);
            // Being handed over or moved: a word said now is held for the take, as SayAsync holds it.
            if (takeWaits(record)) return new(true, "resume", null) { Waiting = waiting };
            return (read.Never(record) ?? new(true, "resume", null)) with { Waiting = waiting };
        }
        catch (Exception error) when (WordsRead.Unanswered(error))
        {
            return new(false, null, null);
        }
    }

    /// <summary>A driven inbox's reach in the wire's spelling, as the record's events spell it.</summary>
    public static string? Spelled(DrivenReach? reach) => reach switch
    {
        DrivenReach.NextStep => "next-step",
        DrivenReach.TurnEnd => "turn-end",
        _ => null,
    };
}
