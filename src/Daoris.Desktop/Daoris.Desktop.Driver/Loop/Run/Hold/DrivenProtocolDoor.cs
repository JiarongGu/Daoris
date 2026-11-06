using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The protocol door's driven session as its run opens it: an ACP session the run holds over its process's stdio, the rendered updates
/// reaching the transcript and the console rather than the wire itself; what it sets on the session, the run that prompts it, and what
/// it says beside the run's lines through <see cref="TransportRun.Say"/>.
/// </summary>
/// <remarks>
/// <para><b>stdout belongs to the protocol; stderr is still text a person wants.</b> Agents log there, and its lines go to the same
/// two destinations through the same tee, so a transcript stays one readable stream.</para>
///
/// <para><b>The ending is end-of-input, not a kill.</b> When the turn is over the run closes stdin and the agent winds up and exits on
/// its own, the same ending the chat door records as <c>completed</c>, and the exit the conclusion is actually drawn from.</para>
///
/// <para><b>The wire's own ending is written down and nothing more.</b> It is a self-report, and the protocol flattens an aborted,
/// blocked or errored turn into <c>end_turn</c>, so a record moved by it could not tell a refusal from a success.</para>
/// </remarks>
internal sealed class DrivenProtocolDoor(IRunWiring wiring)
{
    /// <summary>
    /// What a session with no connector is told on its transcript: what it can and cannot do, and the two commands that lay one down,
    /// the install's first, as <see cref="KnowledgeConnector.Candidates"/> ranks them.
    /// </summary>
    internal static string NoConnectorForSession =>
        $"— no {KnowledgeConnector.ExecutableName} on this machine, so this session has no "
        + "connector: it can do the work but cannot take or close its own quest. "
        + "`npm run publish:desktop -- --to <install> --service` lays one beside the application, and "
        + "`npm run publish:service -- --install` lands one in the home's `bin/`.";

    /// <param name="scope">What this session's connector carries beyond the store: an intake's ask and session. Null for a quest's session.</param>
    /// <param name="meta">What <c>session/new</c> carries for the rules composed for this session.</param>
    /// <param name="resume">
    /// The conversation an answer continues: resumed rather than opened, the record's transcript and conversation going on, and an agent
    /// that would not resume it told to the resume, never as a turn that failed.
    /// </param>
    /// <param name="keepAs">The adapter a quest's session keeps its harness's conversation id under, which a refused resume names.</param>
    /// <param name="waitsOnBackground">Whether a turn that ended on its own background work waits for it; null never.</param>
    /// <param name="carriesOn">What a turn's end reads of a done quest's tree, so its session is carried on to commit; null never.</param>
    /// <param name="written">
    /// Told as the first prompt's write returns, and again as a transient try's does: a carry-on's moved words' <c>sent</c>. Null for
    /// every run that moved none.
    /// </param>
    /// <param name="noConnector">What is said where there is no connector, when it is neither a quest's nor an intake's sentence.</param>
    /// <param name="ending">Where the run's wait says the timeout before its kill, handed the door once it is made.</param>
    /// <param name="unoffered">
    /// What the open says where the agent does not offer a required posture, a driven start's permission prompts'; null says Ask Daoris's
    /// (<see cref="AcpSession.Unoffered"/>).
    /// </param>
    /// <param name="setupChanged">
    /// Asked at a turn's end before a held word is prompted, whether the setup the run started under changed
    /// (<see cref="AcpSession.SetupChanged"/>): yes ends the run there, the held words handed back. Null never asks.
    /// </param>
    /// <param name="handedBack">
    /// Told the person's words its run handed back or never delivered, which the run keeps on its record once it ended, to go again.
    /// </param>
    public TransportProtocol For(
        TransportRun run, string sessionId, string cwd, string prompt, string? posture,
        IReadOnlyDictionary<string, string>? scope, object? meta, ConnectorPlan plan, DrivenInbox? inbox,
        ResumeAsk? resume, string? keepAs, Func<CancellationToken, Task<bool>>? waitsOnBackground, string? noConnector,
        AcpEnding? ending, Func<string, string, string>? unoffered, Func<bool>? setupChanged, ActorAsked? asked,
        Action<ActorAsked>? carried, IChoiceReask? modelRefused, Action<IReadOnlyList<AcpConfigOption>>? offerRead,
        TransientRetry? retry, Func<CancellationToken, Task<TurnCarry>>? carriesOn, Action? written, bool typedFailures,
        Action<IReadOnlyList<ChatMessage>> handedBack)
    {
        var context = wiring.Context;

        // The record's half: a record that cannot be written costs a console line, never the session.
        void Event(SessionEvent e) => context.Events.Keep(sessionId, e, run.Say);

        // A driver line that is also part of the conversation: a note in the record as well.
        void Said(string text)
        {
            run.Say(text);
            Event(new SessionEvent { Kind = SessionEventKind.Note, Text = text });
        }

        // The same by its code: the console keeps the English, the record its parts too.
        void SaidByCode(Noted noted)
        {
            run.Say(noted.Note);
            Event(new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts.Count > 0 ? noted.Parts : null });
        }

        return new TransportProtocol(cwd)
        {
            Posture = posture,
            Meta = meta,
            TypedFailures = typedFailures, // asked for only by a door declared to need it
            Ending = ending,
            Configure = session =>
            {
                session.Unoffered = unoffered;
                session.Permissions = HeldPermissions.Of(context.Events).For(sessionId); // held only under asking's mark
                session.SetupChanged = setupChanged;
                session.CarriesOn = carriesOn; // a done quest's uncommitted tree carries the session on before it ends
                session.BackgroundLook = wiring.BackgroundLook; // its quest read again while it waits on its own background work
                session.Carry = asked; // its actor's model and effort, set before the first prompt
                session.Carried = carried;
                session.ModelRefused = modelRefused; // a choice refused at the open, re-asked before any prompt
                session.OffersRead = offerRead; // and what the open listed, kept as its agent's offer reading
            },
            Run = (session, conversation, ct) => RunDrivenAsync(session, conversation, ct),

            // What the person said that never reached it: said, never dropped without a trace.
            Ended = () =>
            {
                if (inbox?.Close() is { Count: > 0 } left)
                {
                    SaidByCode(WordsNotes.Unreached(left.Count, "never reached the session", UnreachedWhy.EndedFirst));
                }
            },
        };

        async Task<AcpOutcome> RunDrivenAsync(AcpSession session, string? conversation, CancellationToken ct)
        {
            try
            {
                // The session's voice, located per run rather than once, because a machine can gain the host between looks; a machine
                // that has none still drives, without a connector.
                var connector = wiring.Connector(sessionId, scope, run.Proc);
                if (connector is null)
                {
                    Said(noConnector ?? (scope is null ? NoConnectorForSession : DriverIntakes.NoConnectorForIntake));
                }

                // The knowledge host first, then whatever the plugins hand every session: tools the session can reach, under the
                // repository's own posture like every other tool it has.
                var offered = plan with { Servers = connector is null ? plan.Servers : [connector, .. plan.Servers] };

                // Whether it waits on its own background work, and a wait between a transient episode's tries, which is Daoris's,
                // never quiet.
                wiring.Watch.OwnWork(sessionId, () => session.WaitsOnOwnWork, () => [.. session.BackgroundRunning.Select(task => task.Name)]);
                if (retry is not null) wiring.Watch.Transient(sessionId, () => session.WaitsOnTransient);

                using var stops = context.Output is null ? null : context.Processes.OpenTaskStops(sessionId, session.StopTaskAsync);

                // Under its run's light checks, a call or a background task past its bound is told to it once, as Daoris's urgent word,
                // and never ended; the checks its start kept, so every run of it reads the same, and the bound its graph version set.
                // The watcher's one look finds them; the quiet word below tells them.
                if (inbox is not null
                    && CallBound.Of(SessionChecks.Held(context.Home, sessionId), SessionChecks.HeldMinutes(context.Home, sessionId)) is { } callBound)
                {
                    wiring.Watch.LongCalls(
                        sessionId, callBound, () => session.BackgroundRunning, () => inbox.ReachAt(MessageLevel.Urgent) == DrivenReach.NextStep);
                }

                // A quest session the watcher marks quiet, or whose call it finds long, is told once, as Daoris's urgent word, the quiet
                // one taken off again when its quiet ends.
                using var quietWord = QuietWord.Tell(
                    wiring.Watch, sessionId, inbox, inbox is null ? null : new SaidDoor(context.Service, sessionId), RunningCalls.Of(context.Events),
                    failed: error => run.Say($"— Daoris's word to this quiet session was not kept: {error}"));

                // The person's words in the record the moment they are said, with when they reach the session: the page shows them
                // waiting in the turn they were said in, rather than nothing until they are handed over.
                inbox?.OnSaid((message, reach) =>
                {
                    // Another session's message on the ask's channel, or a helper's answer, is that agent's claim, never the person's;
                    // Daoris's own word is never said as the person's either.
                    run.Say(message.Channel is { ByThisPerson: false } claim
                        ? reach == DrivenReach.NextStep
                            ? $"— another session's message `{claim.Id}` on the ask's channel, which reaches the session at its next step"
                            : $"— another session's message `{claim.Id}` on the ask's channel, which goes beside what the session is told when its turn ends"
                        : message.Helped is { Helper: var helper }
                            ? $"— helper `{helper}`'s answer, which reaches the session {(reach == DrivenReach.NextStep ? "at its next step" : "when its turn ends")}"
                        : message.Daoris is not null
                            ? $"— Daoris said, which reaches the session {(reach == DrivenReach.NextStep ? "at its next step" : "when its turn ends")}: {message.Text}"
                        : reach switch
                        {
                            DrivenReach.NextStep => $"— the person added, which reaches the session at its next step: {message.Text}",

                            // The person's Send now: steered into the running turn, cutting the step in flight.
                            DrivenReach.Steered => $"— the person sent now, which the session takes at once: {message.Text}",
                            _ => $"— the person added, which reaches the session when its turn ends: {message.Text}",
                        });

                    // Its level beside its reach: what was asked, and what the door gave.
                    Event(ChatMessage.Words(message) with { Reaches = SendNowWhy.Spelled(reach), Level = message.LevelSaid });
                });

                // A word the person took back or reworded while it waited is settled, by its id.
                inbox?.OnWithdrawn(message =>
                {
                    run.Say($"— the person took back what they added: {message.Text}");
                    Event(ChatTurns.Withdrawn(message.Id!));
                });
                var outcome = await session
                    .RunAsync(
                        cwd, prompt, ct, offered, inbox,

                        // The same words as the turn's ask where the session took them: recorded before anything the agent did with
                        // them, so the answer never sits above the question.
                        asked: delivered =>
                        {
                            var message = ChatMessage.FromAgent(delivered);
                            run.Say(message.Channel is { ByThisPerson: false } claim
                                ? $"— the session took another session's message `{claim.Id}` on the ask's channel"
                                : message.Helped is { Helper: var helper }
                                    ? $"— the session took helper `{helper}`'s answer"
                                    : $"— the session took what the person added: {message.Text}");
                            Event(ChatMessage.Words(message));

                            // A channel message's entry acknowledges it taken, on the door's word.
                            message.Acknowledge?.Invoke(ChannelAcks.Taken);
                        },
                        resume: conversation,

                        // Each of the person's words its own block, taken once the prompt is on the wire.
                        blocks: resume?.Blocks,

                        // And a carry-on's moved words are `sent` as its first prompt is on the wire.
                        prompted: resume is null ? written : () =>
                        {
                            resume.Prompted = true;
                            written?.Invoke();
                        },

                        // Marked attempted on the record before the opening prompt carrying them is written: acknowledged, the opening
                        // narrowed to the words the record names marked, and none named, nothing prompted.
                        attempting: resume is null ? null : async () =>
                        {
                            var asked = resume.Ids.Count;
                            resume.Narrow(await SaidDoor.AttemptedAsync(context.Service, sessionId, resume.Words).ConfigureAwait(false));
                            return asked > 0 && resume.Ids.Count == 0 ? null : (resume.Blocks, resume.Files);
                        },

                        // The files said with them, a link each after the blocks.
                        files: resume?.Files,
                        waitsOnBackground: waitsOnBackground,
                        retry: retry)
                    .ConfigureAwait(false);

                // The words a changed setup handed back, kept on the record once it concluded, to go on under the new one.
                handedBack([.. session.HandedBack.Select(ChatMessage.FromAgent)]);
                return outcome;
            }
            catch (AcpResumeRefused refused) when (resume is not null)
            {
                // Not a turn that failed: nothing was prompted. The agent's own words stay in this machine's record of the
                // conversation, and the reason goes to the resume, which ends the park and carries the answer on. A resume keeps its
                // conversation under the adapter it ran on, so that is the agent a refusal names; a record that waits for the person
                // says no carry-on.
                resume.Refused = refused.Why;
                SaidByCode(refused.Why.Holds ? DoorNotes.ResumeRefused(refused, keepAs) : DoorNotes.AnswerCarriedOn(refused, keepAs));
                throw;
            }
            catch (DriverException error)
            {
                // A protocol failure is a fact about this run and belongs in its transcript, and it goes to the conclusion too, beside
                // the exit code and the quest's state. 🔴 The exit alone cannot carry it: the run closes stdin as it ends and the agent
                // exits 0, so an account limit that refused the turn mid-edit would read as a clean exit. A failed call is the door
                // saying the work was cut off, not the session describing its work. 🔴 A stream the timeout ended is said as the
                // timeout, by its own line.
                SaidByCode(error is AcpEnded ended ? ended.Why : DoorNotes.Failed(error.Message));

                // 🔴 The person's words it never took, sent behind the failed turn or held for its end, go back to the run, which keeps
                // them on the record once it ended, to go again; closed here, so its end says nothing of them.
                handedBack(
                [
                    .. session.Unreached.Select(ChatMessage.FromAgent), .. session.HandedBack.Select(ChatMessage.FromAgent), .. inbox?.Close() ?? [],
                ]);
                throw;
            }
        }
    }
}
