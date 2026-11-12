using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A conversation's session on the protocol door as its run holds it open: what it is handed on <c>session/new</c> and what is set on it before
/// its first prompt, the turns let go once it opened, and what its open's failure says. Its lines, what each turn renders, reach the transcript
/// and the console through the run, and what they mean the record: the driven path's split, held open for as many turns as the person takes.
/// </summary>
/// <remarks>
/// A session that cannot be opened is one nothing can be said to: the run ends its process once this has said why, and the failure is what the
/// record concludes with. The driver saw it, and it is not the person's.
/// </remarks>
internal sealed class ProtocolHeld(IExecutorWiring wiring, Action<string, IReadOnlyList<AcpConfigOption>> optionsChanged)
{
    /// <param name="cwd">The folder the session works in.</param>
    /// <param name="connectors">What its door is handed of the servers: the plan its open reads.</param>
    /// <param name="notUp">What its open past its bound says (<see cref="OpeningPastBound"/>); null says it as any failed open.</param>
    public TransportProtocol Of(
        TransportRun run, ProtocolChat chat, string sessionId, string cwd, string? posture, object? meta, ConnectorPlan connectors,
        ChatAgent agent, Func<Noted>? notUp = null)
    {
        void Record(SessionEvent e) => wiring.Context.Events.Keep(sessionId, e, run.Say);

        return new TransportProtocol(cwd)
        {
            Posture = posture,
            Meta = meta,
            Connectors = connectors,
            TypedFailures = agent.TypedFailures,
            // Its model and effort, told each time they change, so the page offers what the agent does.
            Options = options => optionsChanged(sessionId, ChatOptions.ThePersons(options)),
            Configure = session =>
            {
                chat.Configure(session);
                // The person's words said during a turn, as the record keeps them where the session took them.
                session.OnTaken(delivered =>
                {
                    var words = ChatMessage.FromAgent(delivered);
                    // Daoris's own go-on, told beside the person's words a transient try carries, is Daoris's, never the person's.
                    run.Say(words.Daoris is not null
                        ? $"— the conversation was told: {words.Text}"
                        : $"— the conversation took what the person added: {words.Text}");
                    Record(ChatMessage.Words(words));
                });
            },
            Opened = () => chat.Opened(run, run.Say, Record),
            Unopened = error =>
            {
                switch (error)
                {
                    case AcpResumeRefused refused when chat.GoOn is { } goOn:
                        // Not a turn that failed: nothing is prompted. The agent's own words stay in this machine's record of the conversation;
                        // the reason goes back to the record, which goes back to how it ended. Its reason by code, the agent's words its own.
                        goOn.Refused = refused.Why;
                        chat.Opened(null, run.Say, Record);
                        var refusal = DoorNotes.ResumeRefused(refused, agent.Name);
                        run.Say(refusal.Note);
                        Record(new SessionEvent { Kind = SessionEventKind.Note, Text = refusal.Note, Parts = refusal.Parts });
                        break;
                    case AcpChoiceNotOffered refused when chat.Pending is { } pending:
                        // Its agent refused the choice before any prompt, and the re-ask answered another session. Its words move to the pending
                        // start before the queue lets them go, each said meanwhile joining them, so the replacement takes each once; nothing is
                        // prompted, and the record ends `choice-not-offered`.
                        chat.NotOffered = refused.Refusal;
                        pending.Moving(chat.Turns.MoveOut(word => pending.Join(word) || wiring.Of<ChatReplacement>().Onward(pending, sessionId, word)));
                        chat.Opened(null, run.Say, Record);
                        break;
                    case OpeningPastBound when notUp is not null:
                        // What a harness waiting on a server that never comes up waits on is said; the words stay kept for the next send.
                        chat.Opened(null, run.Say, Record);
                        var past = notUp();
                        run.Say($"— {past.Note}");
                        Record(new SessionEvent { Kind = SessionEventKind.Note, Text = past.Note, Parts = past.Parts });
                        break;
                    default:
                        // Every failure ends the open: one that escaped, an unreadable answer or a pipe that broke mid-write, left the queue
                        // waiting on a session that never came.
                        chat.Opened(null, run.Say, Record);
                        var notOpen = DoorNotes.NotOpen(error.Message);
                        run.Say($"— {notOpen.Note}");
                        Record(new SessionEvent { Kind = SessionEventKind.Note, Text = notOpen.Note, Parts = notOpen.Parts });
                        // A refused sign-in may come at `session/new` as well as at a prompt: which one Claude Code's adapter answers with is
                        // not known, so both are read.
                        chat.ReadSignIn(error);
                        break;
                }
            },
        };
    }
}
