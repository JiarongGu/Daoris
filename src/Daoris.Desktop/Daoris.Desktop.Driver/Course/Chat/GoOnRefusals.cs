using static Daoris.Driver.ChatGoOn;

namespace Daoris.Driver;

/// <summary>Where an ended conversation's go-on does not go: the agent refused to resume, its words cannot go on, or its conversation is lost. Built per call over the executor.</summary>
internal sealed class GoOnRefusals(IExecutorWiring wiring, ChatGoOn owner)
{
    private ChatContext _context => wiring.Context;
    private BriefedMove _move => wiring.Of<BriefedMove>();
    private LimitMove _limit => wiring.Of<LimitMove>();

    /// <summary>
    /// The agent would not resume the conversation (MSG1c): nothing went, so the record goes back to how it ended, its flags
    /// kept and its note saying why, and its words stay waiting, marked, as where it could not go on. Where the refusal proves the
    /// conversation lost (SESSCTX1c part B), the words are not marked: the runner goes on in a new part once this run let go
    /// (<see cref="LostAsync"/>), since the new part starts in the same tree.
    /// </summary>
    public async Task WentBackAsync(
        string sessionId, GoingOn goOn, ContinueReason why, string adapter, Func<string, string, Task>? onEnded)
    {
        // 🔴 LIMITRESUME1c (D206 §3.5, B9): Daoris's limit word off first, so a refusal that waits for the person leaves none of it.
        var record = await owner.LimitWordOffAsync(sessionId, goOn.Record).ConfigureAwait(false) ?? goOn.Record;
        try
        {
            await _context.Service.AdvanceAsync(
                    sessionId, record.State, Continuations.CannotNote(record, why), interrupted: record.Interrupted,
                    limit: record.Limit)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort, as every conclusion here is: the host may be gone on the same shutdown.
        }

        if (!why.Lost) CannotGoOn(record, why, adapter);
        if (onEnded is not null)
        {
            try
            {
                await onEnded(sessionId, record.State).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A listener's failure is its own; the record is already written.
            }
        }
    }

    /// <summary>
    /// Where a chat's words cannot go on in it (MSG1c, D137 §2.2): nothing carries a chat's words on by itself, so they stay
    /// waiting as said and are marked by their ids; its conversation says why with their ids and the reason's code, which the
    /// page words with its one press (MSG1f); and the machine log says it did not resume.
    /// </summary>
    /// <param name="said">Whether the machine log's line is still to be written: false where <see cref="LostAsync"/> wrote it.</param>
    internal ChatStart CannotGoOn(PriorSession record, ContinueReason why, string adapter, bool said = false)
    {
        // LIMITRESUME1c (D206 §3.5, B9): never said over Daoris's limit word, which leaves the record as the go-on ends.
        record = WithoutLimitWords(record);
        var words = record.Waiting.Select(word => word.Id).OfType<string>().ToList();
        _context.Marks.Mark(record.Session, words, why, DateTimeOffset.UtcNow);
        var note = Continuations.Cannot(words, why);
        _context.Output?.Append(record.Session, note.Text!);
        _context.Events.Keep(record.Session, note, say: null);
        if (!said) _context.Service.AccountSaid(Reopened(record, why, adapter, words));
        return new(null, $"it cannot go on in this conversation, because {why.Said}.");
    }

    private AccountLine Reopened(PriorSession record, ContinueReason why, string adapter, IReadOnlyList<string> words) =>
        Continuations.Reopened(record.Session, adapter, record.State, why, _context.Events.DoorOf(record.Session, words), kind: "chat");

    /// <summary>
    /// 🔴 SESSCTX1c part B (D189 point 3, the session-context design §2): a conversation whose own conversation is proven lost goes on by
    /// itself in a new part handed Daoris's brief, then the person's words, its record naming the one it continues, why <c>resume</c>
    /// (<see cref="BriefedMove"/>). Where nothing opens (no words of the person's, a tree gone, a start refused or failed) the words wait
    /// as anywhere a conversation cannot go on: marked, said why, the page offering *Go on with Daoris's brief* beside *Start a
    /// conversation with these words*. The machine log says once that it did not resume, and why.
    /// </summary>
    /// <param name="held">Whether the caller holds the session's going-on already (the judgement before a spawn).</param>
    internal async Task<ChatStart> LostAsync(
        PriorSession record, ContinueReason why, string adapter, DriverConfig choices, Func<string, string, Task>? onEnded, bool held,
        CancellationToken ct)
    {
        var restarted = record.Waiting.Any(word => word.Daoris && DaorisKeys.IsRestart(word.Key));
        // 🔴 LIMITRESUME1c (D206 §3.5, B9): Daoris's limit word off first, by its id; it is never handed on, nor the reason to say cannot.
        record = await owner.LimitWordOffAsync(record.Session, record).ConfigureAwait(false) ?? record;
        var words = record.Waiting.Select(word => word.Id).OfType<string>().ToList();
        _context.Service.AccountSaid(Reopened(record, why, adapter, words));

        // A conversation its limit held keeps the refused turns' words in its events alone: it goes on by the limit's own ended-record
        // move, its own account allowed since its conversation is lost, handed the brief, those words and the words that waited.
        if (!_context.Closing && (restarted || LimitHeld.Waiting(_context.Events.After(record.Session, 0).Events, record.Limit) is not null))
        {
            StartedFrom? moved;
            try
            {
                moved = await _limit.MoveEndedAsync(record.Session, why, choices, onEnded, ct, holding: held, restarted: restarted).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or IOException or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                moved = new StartedFrom(null, false, null, error.Message);
            }

            if (moved is { SessionId: { } limitTo }) return new(limitTo, moved.Message);
            // PICKLOST1: its account is the person's pick and cools; the brief's move would refuse the same account and mark the words as
            // unable to go on, where a picked conversation waits for it, its words with it, and the wake goes on there at its reset.
            if (moved is { Why: LimitMove.OnPick }) return new(null, moved.Message);
        }

        if (record.Waiting.Any(word => word.Persons) && !_context.Closing)
        {
            StartedFrom moved;
            try
            {
                moved = held
                    ? await _move.MovingAsync(record.Session, why, HandOverBy.Daoris, choices, onEnded, ct).ConfigureAwait(false)
                    : await _move.MoveAsync(record.Session, why, HandOverBy.Daoris, choices, onEnded, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or IOException or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                moved = new StartedFrom(null, false, null, error.Message);
            }

            if (moved is { SessionId: { } to, Sent: true }) return new(to, moved.Message);
        }

        return CannotGoOn(record, why, adapter, said: true);
    }
}
