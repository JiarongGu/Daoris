using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The watch over a conversation's run from its open to its end: records it, opens or resumes its agent, reads how it ended, concludes its
/// record, and lets go of everything the run held.
/// </summary>
internal sealed class ChatWatch(IExecutorWiring wiring, ChatTold told, ChatConclusion conclusion, AttentionWatch answers)
{
    private ChatContext Context => wiring.Context;

    /// <param name="run">The run over its process, spawned and tracked, its start completed and not yet open.</param>
    /// <param name="measured">Told the conversation's high-water context at its end, where its door reported one.</param>
    /// <param name="rules">The conversation's rules file, which goes when the conversation does.</param>
    /// <param name="mapper">The harness's structured-output reader, or null where its door is text.</param>
    /// <param name="chat">The conversation's session on the protocol door, or null on the pipe.</param>
    /// <param name="native">The conversation's turns on the native door's structured wire, or null.</param>
    /// <param name="servers">The plugins' servers file handed on the pipe door, which goes when the conversation does.</param>
    /// <param name="said">Told what its door says about its account's windows, on either structured door.</param>
    /// <param name="watched">Its adapter, whether its conversation is kept, and the ended chat it goes on with.</param>
    /// <param name="account">
    /// The account's part of its sink: the native door's failed result read for a limit, concluded at its turn's end as a protocol refused
    /// turn is, its cooling line before that end and the end moving it to the next account; and an open's refused model kept in the offers
    /// store.
    /// </param>
    /// <returns>
    /// Whether the chat it goes on with is to be tried once more as the same resume: its native harness ended on a failed exit before it named
    /// its conversation, the first time. The record is left working, and nothing is concluded.
    /// </returns>
    public async Task<bool> WatchAsync(
        string sessionId, TransportRun run, string transcript, Func<string, string, Task>? onEnded,
        Action<AcpUsage>? measured, string? rules, IStreamMapper? mapper, ProtocolChat? chat, NativeChat? native,
        string? servers, Action<JsonElement>? said, ChatWatched watched, ActorRun actorRun, MachineLog actorLog,
        IReadingSink account)
    {
        var goOnDoor = wiring.Of<ChatGoOn>();
        var processes = Context.Processes;

        // Declared first, so disposed last: a word shown to its ended record while this run still held it goes on once everything of this
        // run is let go.
        using var letGo = new Disposer(() => goOnDoor.LetGo(sessionId));
        using var actorLogging = actorLog;
        await using var actorWatching = actorRun;
        using var actorUnbound = new Disposer(() => Context.ActorRuns.TryRemove(sessionId, out _));
        var goOn = watched.GoOn;

        // Declared first, so it is let go last: its process ended, untracked and disposed after every map that talks to it has let go.
        await using var held = run;
        using var talking = new Disposer(() =>
        {
            Context.Runs.TryRemove(sessionId, out _);
            Context.Talking.TryRemove(sessionId, out _);
            Context.Turned.TryRemove(sessionId, out _);
            Context.Rooms.TryRemove(sessionId, out _);
            Context.Finishing.TryRemove(sessionId, out _);
            wiring.Of<HelpAccessSwap>().Forget(sessionId);
            Context.Handing.TryRemove(sessionId, out _);
            Context.Browsers.TryRemove(sessionId, out _);
            Context.Drafts.TryRemove(sessionId, out _);
            Context.Palettes.TryRemove(sessionId, out _);
            told.Ended(sessionId);
            // Nothing waiting will be sent now, and the page is told its queue emptied. Words kept on its record wait there for it to go on,
            // unless the person stopped it, which ends them with it.
            var personStopped = processes.StopMade(sessionId) is { Reason: null };
            chat?.Turns.Gone(personStopped);
            native?.Gone(personStopped);
            // Last, so the queue told empty above judges nothing it still waited on.
            answers.Forget(sessionId);
        });
        // Its background work stoppable from its tab for as long as the conversation lasts, on the session the run holds.
        using var stops = chat is null || Context.Output is null
            ? null
            : processes.OpenTaskStops(sessionId, (task, ct) => run.Session is { } session ? session.StopTaskAsync(task, ct) : Task.FromResult(false));
        // Whether its end was asked of the run: before it, a failure lets the run go with its process ended.
        var ending = false;
        try
        {
            // The conversation's messages are recorded as the person sends them, so the capture opens with no composed prompt of its own, its
            // first line how the run was set up; one going on appends to the transcript of the run before it, its own conversation resumed,
            // its id kept for its record the moment its door names it.
            run.HandStart(run.Start with { Session = sessionId, Transcript = transcript, LineageHome = Context.Home, Preamble = watched.Preamble });
            // The account's part first: a native limit's cooling line is recorded before the turn's end it is told at.
            run.HandSink(ReadingSinks.Of(
                account,
                new RecordReadings(sessionId, Context.Output, Context.Events),
                !watched.Keeps ? null
                    : ReadingSinks.On<WireReading.ConversationNamed>(named => Context.Conversations.Keep(sessionId, watched.Adapter, named.Conversation)),
                said is null ? null : ReadingSinks.On<WireReading.WindowsSaid>(windows => said(windows.Windows))));
            await (goOn is null ? run.OpenAsync(CancellationToken.None) : run.ResumeAsync(goOn.Ask.Conversation, CancellationToken.None))
                .ConfigureAwait(false);

            // A conversation going on is working already: the ledger moved it out of its ended state before the spawn.
            if (goOn is null) await Context.Service.AdvanceAsync(sessionId, "working", transcript: transcript).ConfigureAwait(false);

            // Its exit, then its capture read to its end, then its process's lineage end.
            ending = true;
            var end = await run.EndAsync(CancellationToken.None).ConfigureAwait(false);

            // The native door's harness ended on a failed exit before naming its conversation: it never opened it, which a crash, a failed
            // sign-in or a flag it rejected ends the same way, so it proves nothing of its loss. The same resume is tried once more, once this
            // watch has let its process go; a second such end is `exited`, never `gone`. Read from the structure, never from its words.
            if (goOn is { Ask.Refused: null } && mapper is not null && chat is null && !goOn.Ask.Named
                && end.Exit != 0 && !processes.WasStopRequested(sessionId))
            {
                if (!goOn.Ask.Retried)
                {
                    goOn.Ask.Again();
                    return true;
                }

                goOn.Ask.Refused = ContinueWhy.Of(ContinueWhy.Exited);
            }

            if (goOn?.Ask.Refused is { } refused)
            {
                await goOnDoor.WentBackAsync(sessionId, goOn, refused, watched.Adapter, onEnded).ConfigureAwait(false);
                return false;
            }

            // What it consumed, where its door reported it, measured before the record moves, so a page told of the ending finds the
            // account's usage already counting it.
            if (end.Usage is { } used) measured?.Invoke(used);

            // The person's stop outranks the observation, exactly as for a driven session: a killed process leaves the same signals as one that
            // ended on its own, and only this flag knows whose decision it was. Otherwise a conversation simply ended, `completed`, with no
            // evidence claim beyond whatever its commits already say.
            var stopped = processes.WasStopRequested(sessionId);
            // The driver's own reason when it was the driver, closing say, and the person's otherwise. Said before anyone writes to it.
            var note = conclusion.EndedNoteFor(sessionId, watched, stopped);
            await conclusion.ConcludeAsync(sessionId, stopped ? "stopped" : "completed", note, onEnded).ConfigureAwait(false);
            return false;
        }
        catch (Exception error)
        {
            // The harness first, while it is still tracked: a record that could not move to `working`, a stop pressed during `starting` ends
            // it so, used to conclude here and leave the agent running, untracked and unmarked, until the application exited. The run let go
            // before its end ends it, and writes its process's end unclean once its exit is read; one whose end was asked has exited.
            if (!ending) await run.AbandonAsync().ConfigureAwait(false);

            // An agent that would not resume the conversation: nothing went, so the record goes back to how it ended.
            if (goOn?.Ask.Refused is { } refused)
            {
                await goOnDoor.WentBackAsync(sessionId, goOn, refused, watched.Adapter, onEnded).ConfigureAwait(false);
                return false;
            }

            if (processes.WasStopRequested(sessionId))
            {
                await conclusion.ConcludeAsync(sessionId, "stopped", conclusion.EndedNoteFor(sessionId, watched, stopped: true), onEnded).ConfigureAwait(false);
                return false;
            }

            // Its agent refused the choice at the open, before any prompt: `choice-not-offered`, no strike and no Ended notice, the reading
            // kept by the re-ask before it ended.
            if (chat?.NotOffered is { } notOffered)
            {
                var ended = Observation.ChoiceNotOffered(watched.Adapter, notOffered);
                await conclusion.ConcludeAsync(sessionId, ended.State, ended.AsNoted(), onEnded).ConfigureAwait(false);
                return false;
            }

            await conclusion.ConcludeAsync(sessionId, "failed", Observation.Failure(error.Message), onEnded).ConfigureAwait(false);
            return false;
        }
        finally
        {
            Context.Output?.Close(sessionId);
            SpawnFiles.Remove(rules);
            SpawnFiles.Remove(servers);
            SpawnFiles.RemoveFolder(watched.Skills);
        }
    }
}
