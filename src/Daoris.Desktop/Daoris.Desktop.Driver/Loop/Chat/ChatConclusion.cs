namespace Daoris.Driver;

/// <summary>How a conversation ends on its record: the state and the note it concludes in, the limit a last turn met, and the ending note of a run that simply ended.</summary>
internal sealed class ChatConclusion(IExecutorWiring wiring)
{
    private ChatContext Context => wiring.Context;

    /// <summary>
    /// Ends a conversation's record in <paramref name="state"/>, saying why, and tells <paramref name="onEnded"/>. Best-effort by construction:
    /// the host, or the client that reaches it, may already be gone on the same shutdown, and the first failure is the report. Every failure,
    /// a client timeout included, is swallowed: one escaping here never reached <paramref name="onEnded"/>, and the terminal door waited on it
    /// forever.
    /// </summary>
    public async Task ConcludeAsync(string sessionId, string state, Noted note, Func<string, string, Task>? onEnded)
    {
        // Its agent has ended: an Ask Daoris conversation whose access changed is pending no more once its end is told.
        wiring.Of<HelpAccessSwap>().Forget(sessionId);
        (state, note, var limit) = LimitedEnd(sessionId, state, note);
        try
        {
            await Context.Service.AdvanceAsync(sessionId, state, note, limit: limit).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        if (onEnded is not null)
        {
            try
            {
                await onEnded(sessionId, state).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A listener's failure is its own; the record is already written.
            }
        }
    }

    /// <summary>
    /// How a conversation that simply ended says so: its stop's reason where the driver made one, else the plain ending, with a word added
    /// where the conversation is spoken in and Daoris keeps no id for its harness conversation.
    /// </summary>
    public Noted EndedNoteFor(string session, ChatWatched watched, bool stopped)
    {
        var note = Context.Processes.StopReason(session) ?? ChatEnded.EndedNote(stopped, notKept: false);
        if (!watched.Keeps || Context.Conversations.Read(session) is not null || !Spoken(session)) return note;
        using var log = new MachineLog(Context.Home, "driver");
        log.Warn("conversation.unkept", ("session", session), ("door", watched.Adapter), ("error", "MissingId"));
        return note.Then(" ", Noted.Of(ChatNoteCodes.ChatNotKept, ChatEnded.NotKept));
    }

    /// <summary>Whether the person has spoken in a conversation and been heard by it, rather than the driver alone.</summary>
    public bool Spoken(string session) =>
        Context.Events.After(session, 0).Events.Any(e => e.Kind == SessionEventKind.User && e.Reaches is null);

    /// <summary>
    /// A conversation whose last turn its account's limit refused ends as a driven end a limit made does (<c>Driver.AccountLimited</c>),
    /// <c>failed</c> with the ledger's <c>limited</c> flag and that turn's cooling line after its own note, so its state, flag and note's code
    /// say <i>limited</i> alike, and every reader decides by the flag. The person's stop stays theirs, as a driven one's does, and an agent's
    /// refusal of the choice at the open keeps its own end.
    /// </summary>
    private (string State, Noted Note, bool Limit) LimitedEnd(string sessionId, string state, Noted note)
    {
        var personStopped = state == "stopped" && Context.Processes.WasStopRequested(sessionId) && Context.Processes.StopReason(sessionId) is null;
        if (personStopped || note.Parts.Any(part => part.Code == EndNoteCodes.EndedChoiceNotOffered.Code)) return (state, note, false);
        if (LimitHeld.Of(Context.Events.After(sessionId, 0).Events) is not { } held) return (state, note, false);

        var cooled = held.Cooled is { } line ? Noted.From(line.Text, line.Parts) : null;
        return ("failed", cooled is { Note.Length: > 0 } ? note.Then(" ", cooled) : note, true);
    }
}
