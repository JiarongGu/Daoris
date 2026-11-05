namespace Daoris.Driver;

/// <summary>
/// The replacement of a conversation whose agent refused its entry's model at the open: the refusal kept and the order walked again
/// (<see cref="ReaskAsync"/>), and where the answer is another session, that session opened once the refused run let its process, files
/// and turns go, and the words moved to it (<see cref="ReplaceAsync"/>).
/// </summary>
[Executor("replacement", ExecutorPhase.Move)]
internal sealed class ChatReplacement(IExecutorWiring wiring) : IExecutor
{
    /// <summary>
    /// The refused record and the replacement that took its words, in the same press, so the window follows the line as it follows a new
    /// part.
    /// </summary>
    public event Action<string, string>? Replaced;

    /// <summary>
    /// The re-ask where the agent refused the entry's model at the open: the refusal kept as the account's reading first, then the pending
    /// start's order walked again as if it had been known before the spawn, the refusing account passed and no count taken. A later entry of
    /// the same agent on the same account is set on this session; anything else is kept as what the replacement opens on
    /// (<see cref="ChatPending.Next"/>), and null ends the open.
    /// </summary>
    public async Task<ActorEffective?> ReaskAsync(
        ChatPending pending, string sessionId, ChatAgent resolved, HarnessSelection selection, DriverConfig config, ModelRefusal refusal)
    {
        var context = wiring.Context;
        using var log = new MachineLog(context.Home, "driver");
        if (AgentOffers.Refused(context.Home, resolved.Name, selection.Version, selection.Profile, refusal.Value, DateTimeOffset.UtcNow) is { } unkept)
        {
            log.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
        }

        log.Write("info", "actor.refused", [("actor", pending.Actor), ("session", sessionId), ("agent", resolved.Name), ("model", refusal.Value),
            ("code", refusal.Code)]);
        var refused = pending.Entry;
        pending.Refusing = (refused, selection.Profile);
        pending.Pass(new BuildPass(refused, refusal.Code, $"`{selection.Profile ?? "its own sign-in"}` did not offer `{refusal.Value}`"));

        ConversationWalk again;
        try
        {
            again = await wiring.WalkAsync(
                    pending.Actor, pending.Order, config, pending.Workspace, pending.Pinned, CancellationToken.None, pending.Refusing,
                    counts: false, except: selection.Number)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or HttpRequestException)
        {
            again = new ConversationWalk(null, null, null, pending.Passes, error.Message);
        }

        var same = ReaskMove.Same(refused, selection.Profile, again.Chosen, again.Selection?.Profile);
        log.Write("info", "actor.reasked", [("actor", pending.Actor), ("session", sessionId), ("refused", refused.From),
            ("answer", again.Chosen?.From), ("same", same)]);
        if (!same)
        {
            pending.Next = again;
            return null;
        }

        // The same account: the record's choice moves with it, and its opening note says the refused entry passed.
        pending.Entry = again.Chosen!;
        RotatedOpening.SayActor(context.Events, sessionId, pending.Actor, again.Chosen!, pending.Passes);
        return again.Chosen;
    }

    /// <summary>
    /// A word said to a refused conversation after its words moved on: to the record its pending start runs as now, where that is another;
    /// false where none takes it.
    /// </summary>
    public bool Onward(ChatPending pending, string from, ChatMessage word) =>
        !string.Equals(pending.Current, from, StringComparison.OrdinalIgnoreCase)
        && wiring.Context.Turned.TryGetValue(pending.Current, out var now) && now.Turns.Take(word) is not null;

    /// <summary>
    /// Once <paramref name="watch"/> let the refused run's process, files and turns go: its count given back, then the walk's answer opened
    /// by its pending start, and the moved words said to it in the order said, each once, taken off the refused record by their ids as its
    /// turn takes them. With nothing left to open, the press is refused in the wait's sentence and the words wait on the refused record.
    /// Never while closing.
    /// </summary>
    /// <remarks>
    /// Against a host whose <c>opens</c> mark is 2 the open moves the words the refused record keeps, and each goes to the replacement as its
    /// own record's word, never taken off the refused record; a moved part's display goes with its message.
    /// </remarks>
    public async Task ReplaceAsync(Task<bool> watch, ChatPending pending, string sessionId, HarnessSelection selection)
    {
        var context = wiring.Context;
        try
        {
            await watch.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Its run concluded the record whatever failed there.
        }

        if (pending.Next is not { } next)
        {
            // Its door opened and carried its choice, or failed for another reason: refusals are no longer this start's.
            if (string.Equals(pending.Current, sessionId, StringComparison.OrdinalIgnoreCase)) pending.Dispatched();
            return;
        }

        pending.Next = null;
        // The display a moved part's first message was held with is the replacement's to write now, and nothing holds it for this record.
        (string Text, IReadOnlyList<SessionEvent> Events)? shown = context.Shown.TryRemove(sessionId, out var held) ? held : null;
        context.ShownMoved.TryRemove(sessionId, out _);
        // Its process has stopped and its record left Running, so its one count is given back first.
        context.Harnesses.Release(selection.Number, sessionId);
        if (context.Closing)
        {
            await WaitOnAsync(sessionId, pending.Exhausted()).ConfigureAwait(false);
            return;
        }

        if (next.Adapter is null)
        {
            context.Note(sessionId, LimitNotes.NotMoved(next.Refusal ?? "none of its choices can run now."));
            await WaitOnAsync(sessionId, pending.Exhausted()).ConfigureAwait(false);
            return;
        }

        ChatStart started;
        try
        {
            started = await pending.Open(pending, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            started = new ChatStart(null, error.Message);
        }

        if (started.SessionId is not { } to)
        {
            context.Note(sessionId, LimitNotes.NotMoved(started.Message));
            await WaitOnAsync(sessionId, pending.Exhausted()).ConfigureAwait(false);
            return;
        }

        var turns = context.Turned.TryGetValue(to, out var chat) ? chat.Turns : null;
        // 🔴 Where the replacement's keyed open moved the refused record's kept words, each is the replacement's own word from the open on,
        // under its id there: its files copied first, handed to the turns as that record's (TakeMoved), so it is marked attempted and taken
        // off there and a producer's retry the record answers with it goes no second time. Every other moving word goes as before (MovedTo).
        var moved = MovedWords.Moving(started)
            ? await MovedWords.OnAsync(context.Service, context.Home, sessionId, to, started.Moved, CancellationToken.None).ConfigureAwait(false)
            : null;
        var door = new SaidDoor(context.Service, to);
        var owned = new Dictionary<string, SaidWordView>(StringComparer.Ordinal);
        foreach (var word in moved?.Words ?? []) owned.TryAdd(word.Id!, word);
        bool Say(ChatMessage word)
        {
            if (turns is null) return false;
            if (word.Kept && word.Id is { } id && owned.Remove(id, out var own)) return TakeOwned(turns, door, Owned(word, own, to), own, to);
            if (shown is { } display && !word.Kept && string.Equals(word.Text, display.Text, StringComparison.Ordinal))
            {
                // Recorded by the replacement's door before its write, each moved word under its id, never one joined word.
                context.Shown[to] = display;
                shown = null;
            }

            return turns.Take(MovedTo(word, sessionId, to)) is not null;
        }

        var left = pending.Moved(to, Say);
        // A word of the person's kept on the refused record since, which the open moved and no moving word held: the replacement's, after them.
        foreach (var late in (moved?.Words ?? []).Where(word => owned.ContainsKey(word.Id!)))
        {
            if (turns is not null) TakeOwned(turns, door, Owned(new ChatMessage(late.Text, []) { Key = late.Key }, late, to), late, to);
        }

        if (left.Count > 0)
        {
            using var log = new MachineLog(context.Home, "driver");
            log.Warn("chat.replaced-unsaid", ("from", sessionId), ("to", to), ("words", left.Count));
        }

        Replaced?.Invoke(sessionId, to);
    }

    /// <summary>
    /// A moving word as the replacement whose open moved it holds it: under its id on that record, kept there, its files as the
    /// replacement's folder keeps them, with no mark or take-off of the refused record's, which its turns give it again through that record.
    /// </summary>
    private ChatMessage Owned(ChatMessage word, SaidWordView own, string to) => word with
    {
        Id = own.Id, Kept = true, Attempting = null, Went = null, Files = ChatFiles.Kept(wiring.Context.Home, to, own.Files),
    };

    /// <summary>
    /// A moved word handed to the replacement's turns as its own, its own <c>User</c> event under its id held for the door that writes its
    /// turn, which records it before the write; false where the turns took nothing, and nothing is held then.
    /// </summary>
    private bool TakeOwned(IChatTurns turns, ISaidDoor door, ChatMessage word, SaidWordView own, string to)
    {
        var context = wiring.Context;
        context.ShowMoved(to, own.Id!, MovedWords.Of(own));
        if (turns.TakeMoved(door, word) is not null) return true;
        if (context.ShownMoved.TryGetValue(to, out var held)) held.TryRemove(own.Id!, out _);
        return false;
    }

    /// <summary>
    /// A moved word as its replacement takes it: held there in this process, never kept on the replacement's record, and taken off the
    /// refused record by its id once its turn is on the wire, so its receipt follows it.
    /// </summary>
    private ChatMessage MovedTo(ChatMessage word, string from, string to)
    {
        if (!word.Kept || word.Id is not { } id) return word;
        return word with
        {
            Id = null, Kept = false, Attempting = null, Went = null,
            Delivered = () => _ = TakenOffAsync(from, [id], to),
        };
    }

    /// <summary>
    /// The moved words of a start with nothing left to open: each held in this process alone kept on the refused record, which has ended,
    /// so they wait there for its go-on, a fresh start of its actor (<see cref="ChatGoOn"/>). A word kept there already waits. Never faults.
    /// </summary>
    private async Task WaitOnAsync(string sessionId, IReadOnlyList<ChatMessage> words)
    {
        var context = wiring.Context;
        foreach (var word in words.Where(word => !word.Kept && !word.OnRecord && word.Persons))
        {
            try
            {
                // Kept at the head of the record's line where it went on, whole, its files carried to the head's folder.
                IReadOnlyList<string> names = [.. word.Files.Select(file => file.Name)];
                await WordsHead.FollowAsync(
                        sessionId, at => context.Service.SayAsync(at, word.Text, names.Count > 0 ? names : null, key: word.Key),
                        (from, to) => ChatFiles.Carry(context.Home, from, to, names))
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException or ObjectDisposedException)
            {
                // Nothing kept is nothing waiting: the record says it ended before any prompt.
            }
        }
    }

    /// <summary>Words off a record by their ids, named as taken by <paramref name="by"/>. Never faults: what it does not take stays there.</summary>
    private async Task TakenOffAsync(string sessionId, IReadOnlyList<string> ids, string by)
    {
        try
        {
            await wiring.Context.Service.TakenAsync(sessionId, ids, by).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or OperationCanceledException or ObjectDisposedException)
        {
            // The word stays on the refused record, where its go-on finds it.
        }
    }
}
