using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Starts a prepared session and holds it to its conclusion: the part a quest's session, an intake, a pass, a helper, a partner's
/// piece, a brainstorm's turn and a continuation share, so a defect in it is fixed once.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The conclusion runs inside the scope that tracks the process</b>, and must. The stop flags it reads live in the
/// tracking entry, and a record still saying <c>working</c> with nothing tracked here is what the orphan sweep concludes for itself.</para>
///
/// <para>🔴 <b>Whatever ends this call ends the agent while it is still tracked</b>: the reaper is declared after the tracking, so it
/// runs first, never leaving an agent working untracked in a tree just unlocked.</para>
///
/// <para>The protocol door's task is held <b>as its own type</b>: assigning it to a bare <c>Task</c> compiles and silently discards
/// the outcome, which is where the usage measurement lives.</para>
/// </remarks>
internal sealed class SessionHold(IRunWiring wiring)
{
    private readonly DrivenProtocolDoor _door = new(wiring);

    /// <param name="conclude">
    /// The exit code (null when the timeout killed it), the usage the door reported (null when it reported none), the protocol door's
    /// failure, and what the harness itself said of its ending, to the caller's conclusion, which reads the stop flags and moves the record.
    /// </param>
    /// <param name="said">Told what the session's door says about its account's windows, on either door, as it says it.</param>
    /// <param name="resume">
    /// The conversation an answer continues: the run resumes it with the answer as its prompt, appends to the record's transcript and
    /// conversation, and says on it whether the agent would not resume. Null for a start.
    /// </param>
    /// <param name="workingNote">What the record says while it works, where a start's opening sentence is not it.</param>
    /// <param name="goesOnAs">
    /// What the harness is selected as, for a native run that goes on with what the person said while it worked: its own conversation
    /// resumed with the words through the run, before the record concludes. Null holds no words, as does a door the run admits no go-on on.
    /// </param>
    /// <param name="working">Told once the ledger has moved the record to working, and so holds its tree.</param>
    /// <param name="waitsOnBackground">
    /// On the protocol door, asked when a turn ends while the session's own background work runs: whether its quest stands as a park
    /// would leave it, so the session waits for that work instead. Null never waits.
    /// </param>
    /// <param name="carriesOn">
    /// On the protocol door, asked at a turn's end whether its quest stands closed with uncommitted work in its tree: yes carries the
    /// session on to commit it, bounded, before the run ends. Null never does.
    /// </param>
    /// <param name="minutes">How long it may run before it is stopped, where that is not the machine's session timeout: a reviewer's pass.</param>
    /// <param name="noConnector">What the protocol door's transcript says where this machine has no connector, when it is not a quest's or an intake's sentence.</param>
    /// <param name="stopMade">
    /// Told, as the run lets go of its process, the stop made of it, or null for none: what a driver's close that cuts the conclusion off
    /// records in place of itself.
    /// </param>
    /// <param name="movedFirst">
    /// That the caller moved the record to working before the spawn: a resume whose words are read once that move is made, so no word is
    /// taken back after its prompt is built. The move is not made again here.
    /// </param>
    /// <param name="spawned">
    /// Told once the process started, before anything else here can fail: what tells such a caller a run that never started, its record to
    /// be put back, from one that did.
    /// </param>
    /// <param name="piece">
    /// A partner's piece's job, which its harness joins beneath this process's agents' job and is never started outside; null for every
    /// other run.
    /// </param>
    /// <param name="transientWanted">
    /// On the protocol door, the driven check a transient retry asks before each try: given, the run is handed a <see cref="TransientRetry"/>;
    /// null, as for every run but a driven quest session's, ends at the first failure.
    /// </param>
    public async Task<T> HoldAsync<T>(
        string actor, ISessionAdapter adapter, ProcessStartInfo info, SessionTarget target, string sessionId, string transcript,
        string cwd, string? harnessNotice, (string? File, object? Meta, HandedSection? Handed) rules, string? handed, string? refusesInput,
        Func<int?, AcpUsage?, string?, HarnessEnding, Task<T>> conclude, CancellationToken ct,
        IReadOnlyDictionary<string, string>? scope = null, string? preamble = null,
        IReadOnlyList<AcpMcpServer>? handedServers = null, bool drivesBrowser = false, Action<JsonElement>? said = null,
        ResumeAsk? resume = null, Noted? workingNote = null, HarnessSelection? goesOnAs = null,
        Action? working = null, Func<CancellationToken, Task<bool>>? waitsOnBackground = null, int? minutes = null,
        string? noConnector = null, Action<SessionStop?>? stopMade = null, bool movedFirst = false, Action? spawned = null,
        string? piece = null, OpenOffers? offers = null, Func<CancellationToken, Task<bool>>? transientWanted = null,
        Func<CancellationToken, Task<TurnCarry>>? carriesOn = null, Action? written = null, (string Role, long Revision)? taken = null,
        string? moved = null)
    {
        var context = wiring.Context;
        var (service, home, output, log) = (context.Service, context.Home, context.Output, context.Log);

        // Through the start gate: a press that starts a session reaches here before its first wait, and the runtime's start lock is no
        // thread's to wait on but the pool's. A run that goes on with the person's words is the person's start; every other the loop's.
        // Its wait for a place is on the loop's token, so a driver closing ends a start still queued, which throws before `spawned` is
        // told: its caller puts back what it took, or closes its record as the driver's. Through the launcher, so the harness is born in
        // this process's agents' job, and a partner's in its piece's job beneath it.
        var whose = resume is null ? StartClass.Background : StartClass.Person;

        // The run holds the process, its capture and its wait; the steps below are this hold's own, between the spawn and the open. The
        // spawn keeps the downloads the start is handed and brackets its process's lineage; it is tracked so who drives Daoris's browser
        // lasts exactly as long as it, and reaped.
        var start = new TransportStart(info, whose)
        {
            RefusesInput = refusesInput, DrivesBrowser = drivesBrowser, Piece = piece, Moved = moved, Target = target, Selection = goesOnAs,
        };
        await using var run = new TransportRun(wiring.Transport, adapter, start);
        await run.SpawnAsync(sessionId, ct).ConfigureAwait(false);
        spawned?.Invoke();

        // What this start's record already says, and what it asked, kept as reporting alone: a service that does not answer here costs
        // the record its provenance, never the run.
        PriorSession? prior = null;
        if (target.Brainstorm is null)
        {
            try { prior = await service.RecordAsync(sessionId, ct).ConfigureAwait(false); }
            catch (Exception error) when (error is not OperationCanceledException) { log?.Write("warn", "actor.read-failed", [("session", sessionId)]); }
        }
        actor = prior?.Actor ?? actor;
        if (taken is { } role) context.Conversations.KeepRole(sessionId, role.Role, role.Revision);
        var asked = ActorAsked.ForSpawn(adapter.Name, info, target.Asked ?? prior?.Asked);

        // The entry a new start's walk took; a continuation keeps its record's own.
        var choice = target.Choice ?? prior?.Choice ?? "before";
        if (target.Brainstorm is null)
            await service.TryKeepActorAsync(sessionId, log, choice, asked, ct: ct).ConfigureAwait(false);
        UsageTurns.ActorBound(home, context.Events, sessionId, actor);
        if (choice != "before" && target.Brainstorm is null) UsageTurns.ChoiceBound(home, context.Events, sessionId, new StaffKey(asked.Agent, asked.NamedModel, asked.Effort.Value).Text);

        // A brainstorm pass's turns are the brainstorm's usage, kept naming it beside the account its walk chose.
        if (target.Brainstorm is { } brainstorm) UsageTurns.BrainstormBound(home, context.Events, sessionId, brainstorm);
        await using var actorRun = new ActorRun(context.Events, sessionId, actor, prior?.Ran,
            ran => target.Brainstorm is null ? service.KeepActorAsync(sessionId, ran: ran) : Task.CompletedTask, log);

        // A quest's session and an intake are watched while they run, never ended on time; a pass keeps minutes of its own and is not watched.
        using var watched = minutes is null && target.Opinion is null && target.Helper is null && target.Brainstorm is null && target.Piece is null
            ? wiring.Watch.Watch(
                sessionId,
                new WatchedRun(target.Ask is null ? SessionWatch.Quest : SessionWatch.Intake, target.Ask is null ? target.QuestId : null, adapter.Name)
                {
                    Tree = cwd,
                })
            : null;
        using var servers = new Disposer(() => SpawnServers.Remove(handed));
        using var ruled = new Disposer(() => SpawnSettings.Remove(rules.File));

        // 🔴 The stop made of it, told as the run lets go, whatever ends it: disposed before the run above untracks its processes, its
        // go-ons' among them, so it is read while they still are, for a close that cuts the conclusion off to record that stop.
        using var letGo = new Disposer(() => stopMade?.Invoke(context.Processes.StopMade(sessionId)));

        // Each door ends the same way: the record is concluded from the exit code and what the session was for, never from what the
        // session said about itself. A resume's prompt is the answer as it is, the conversation already holding the target; after it,
        // the answers to the go-aheads it asked since its start.
        var composed = resume is null ? TargetPrompt.Composed(TargetPrompt.ForStart(target, adapter)) : null;
        var prompt = resume?.Prompt ?? composed!.Text;

        // What the target was composed of, kept beside it on its event with the rules handed beside it. A resume composes nothing, so
        // its record keeps none; nor a reviewer's, whose instruction is its own composer's, which the account's sections do not name.
        var account = composed is null || target.Opinion is not null || target.Helper is not null || target.Piece is not null || target.Brainstorm is not null ? null
            : rules.Handed is { } rulesHanded ? composed.Account.Beside(rulesHanded)
            : composed.Account;

        // A quest's session keeps the id its harness names, which an answer to a park resumes. Never an intake's, answered through its
        // ask; nor a reviewer's, each pass a fresh conversation.
        var keepAs = target.OneTurn ? null : adapter.Name;

        // What the person tells a quest's session while it works, held for the protocol door to hand over between turns; on the native
        // door, held until it ends and gone on with in its own conversation. Never a one-turn run's, framed as one prompt.
        var held = run.GoesOn && !target.OneTurn ? context.Processes.OpenInbox(sessionId) : null;
        var inbox = adapter.Wire == SessionWire.Acp && !target.OneTurn ? context.Processes.OpenInbox(sessionId) : held;
        using var unheld = new Disposer(() => inbox?.Close());
        if (held is not null)
        {
            // In the record the moment they are said, with when they reach it: the run's own transcript is open in its capture, so the
            // line goes to the console and the record, and the transcript has it as the next run opens.
            held.OnSaid((message, _) =>
            {
                output?.Append(sessionId, $"— the person added, which reaches the session when its turn ends: {message.Text}");
                context.Events.Keep(sessionId, ChatMessage.Words(message) with { Reaches = "turn-end", Level = message.LevelSaid }, say: null);
            });

            // A word the person took back or reworded while it waited is settled, by its id.
            held.OnWithdrawn(message =>
            {
                output?.Append(sessionId, $"— the person took back what they added: {message.Text}");
                context.Events.Keep(sessionId, ChatTurns.Withdrawn(message.Id!), say: null);
            });
            held.Attach(interrupt: null);
        }

        // A driven session's permission prompts are read again from the driver's file at a turn's end where a word is held for the next,
        // so a change made while it runs applies at its next turn: the run ends there, its held words handed back to its record, and the
        // next look goes on in the same conversation under the setup standing then. Only a session that holds words; Ask Daoris's own
        // swap is its chat runner's.
        SetupSwap? swapped = null;
        var started = inbox is not null && wiring.ConfigPath is not null ? target.Access : null;
        Func<bool>? setupChanged = started is null ? null : () => (swapped = SetupNow(started, adapter, wiring.ConfigPath!)) is not null;

        // And held beside its inbox while it runs, so the page's press tells its console when the change applies.
        using var setupHeld = started is null ? null : context.Processes.OpenSetup(sessionId, now => started.SwapIn(now, adapter));

        // The timeout is said to the protocol door before the process is killed, so the stream's end it causes is said as the timeout's,
        // never as the agent having exited.
        var ending = adapter.Wire == SessionWire.Acp ? new AcpEnding() : null;

        // What the open lists is kept as its agent's offer reading on its account, and a choice it refuses is kept refused there before
        // the start's re-ask answers: a later choice set on this same session, its record's choice and asked moving with it, or the open
        // ending before its prompt.
        Action<IReadOnlyList<AcpConfigOption>>? offered = offers is null
            ? null
            : options =>
            {
                if (AgentOffers.Offered(home, adapter.Name, offers.Version, offers.Account, Values("model"), Values("thought_level"), DateTimeOffset.UtcNow)
                    is { } unkept)
                {
                    log?.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
                }

                IReadOnlyList<string>? Values(string category) =>
                    options.FirstOrDefault(option => option.Category == category) is { } option ? [.. option.Choices.Select(each => each.Value)] : null;
            };
        Func<ModelRefusal, Task<ActorAsked?>>? refusedAt = offers?.Refused is not { } reask
            ? null
            : async refusal =>
            {
                if (AgentOffers.Refused(home, adapter.Name, offers.Version, offers.Account, refusal.Value, DateTimeOffset.UtcNow) is { } unkept)
                {
                    log?.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
                }

                log?.Write("info", "actor.refused", [("actor", actor), ("session", sessionId), ("agent", adapter.Name), ("model", refusal.Value),
                    ("code", refusal.Code)]);
                if (await reask(refusal).ConfigureAwait(false) is not { } later) return null;

                // The same-account move: the record's choice and asked move through their later door, and the opening note says both,
                // the refused entry passed on this account.
                var refusedEntry = new ActorEffective(adapter.Name, refusal.Value, null, choice);
                var ranEntry = new ActorEffective(adapter.Name, later.Asked.NamedModel, later.Asked.Effort.Value, later.Choice);
                var why = $"`{offers.Account ?? "its own sign-in"}` did not offer `{refusal.Value}`";
                RotatedOpening.SayActor(context.Events, sessionId, actor, ranEntry, [new BuildPass(refusedEntry, refusal.Code, why)]);
                ActorMoves.Write(log, actor, sessionId, refusedEntry.From, later.Choice, "model-refused");
                if (target.QuestId is { } movedLine)
                {
                    wiring.CountMove(movedLine, actor, StaffConditions.ModelRefused, new ActorEffective(adapter.Name, refusal.Value, asked.Effort.Value, choice), later.Choice);
                }

                choice = later.Choice;
                UsageTurns.ChoiceBound(home, context.Events, sessionId, new StaffKey(adapter.Name, later.Asked.NamedModel, later.Asked.Effort.Value).Text);
                await service.TryKeepActorAsync(
                        sessionId, log, choice, later.Asked, ct: CancellationToken.None,
                        chosen: new ActorChosen(prior?.Chosen?.Rule ?? StaffRules.Order, StaffBy.Moved, "model-refused", refusedEntry.From))
                    .ConfigureAwait(false);
                return later.Asked;
            };

        // A driven quest session's protocol door is handed the transient retry, its waits on the driver's clock; its bound is its role's
        // `after` where the role moves on `transient`, else five.
        var retry = adapter.Wire == SessionWire.Acp && transientWanted is not null && target.QuestId is { } retried
            ? TransientRetries.For(
                context.Adapters, adapter, context.Harnesses, service, context.Events, output, wiring.Watch, log, sessionId, retried,
                prior?.Profile, wiring.RetryClock, transientWanted,
                tries: StaffCourse.Tries(context.Harnesses.StaffFor(home), context.Conversations.RoleOf(sessionId)?.Role ?? actor, new StaffPlace(target.Repository, prior?.Workspace, retried)))
            : null;
        var protocol = adapter.Wire == SessionWire.Acp;

        // What the native door's harness says in its own words, apart from its agent's, for its ending.
        var own = new HarnessWords();
        var structured = !protocol && adapter.StructuredOutput() is not null;

        // The person's words the protocol door handed back or never delivered, as its session's run said them.
        IReadOnlyList<ChatMessage> acpLeft = [];
        run.HandStart(start with
        {
            // A native harness's prompt goes on its input once its output is being read, then closed; the write's return is where a
            // carry-on's moved words are `sent`, never the process's start, which a crash can precede the write by.
            Prompt = protocol ? null : prompt,
            Written = protocol ? null : written,
            Minutes = minutes,
            TimingOut = ending is null ? null : bound => ending.Say(Observation.TimedOut(bound)),
            Quiet = watched?.Ending ?? default,
            QuietEnding = ending is null ? null : () => ending.Say(Observation.Quiet(watched!.EndedMinutes ?? 0)),

            // A native resume is told once its harness named its conversation, so its words went; its one retry marked on the resume.
            Named = resume is null ? null : () => resume.Named = resume.Prompted = true,
            Again = resume is null ? null : resume.Again,
            Protocol = !protocol ? null : _door.For(
                run, sessionId, cwd, prompt,

                // The posture rides with it because it is the adapter's: harnesses name the same boundary differently, and one does not
                // name it on the wire. A driven session's asking or unrestricted permission prompts name the adapter's mode, required.
                target.Access?.Posture ?? Driver.PassPosture(target, adapter) ?? adapter.AcpPosture, scope, rules.Meta,
                handedServers ?? context.Servers, inbox, said, resume, keepAs, waitsOnBackground, noConnector, ending,
                unoffered: target.Access is { } access ? access.Unoffered : Driver.PassPosture(target, adapter) is not null ? BrainstormPermissions.Unoffered : null,
                setupChanged: setupChanged,

                // What its actor asked, set on the session before the first prompt; what the agent would not take is kept as asked and
                // not carried, so the record never says a choice was taken that was not.
                asked: asked, carried: target.Brainstorm is not null ? null
                    : notCarried => _ = service.TryKeepActorAsync(sessionId, log, choice, notCarried, ct: CancellationToken.None),
                modelRefused: refusedAt, offerRead: offered, retry: retry, carriesOn: carriesOn, written: written,
                typedFailures: adapter.Toolchain?.ErrorWords?.TypedFailures == true, handedBack: words => acpLeft = words),
        });

        // What was asked, first: the composed target and the person's answer it carries on from, as theirs; a resumed run's own first
        // line and the answer, the target already in the conversation above.
        IReadOnlyList<SessionEvent> opening = resume?.Opening() ?? HarnessCapture.Opening(prompt, target.PersonSaid, account, target.Handed);
        run.HandRecords(new RunRecords(
            sessionId, transcript, output, context.Events, LineageHome: home, KeepAs: keepAs, Own: own, Said: said,

            // The protocol door's opening goes on with what its harness is doing that Daoris could not govern, said as a note; the
            // native door's transcript opens with how its run was set up.
            Opening: protocol && harnessNotice is { Length: > 0 } notice ? [.. opening, new SessionEvent { Kind = SessionEventKind.Note, Text = notice }] : opening,
            Preamble: protocol ? null : preamble));

        // The rules and servers a go-on runs under, the ones this start was handed.
        run.HandCarriers(new RunCarriers(rules.File, handed));
        await (resume is null ? run.OpenAsync(ct) : run.ResumeAsync(resume.Conversation, ct)).ConfigureAwait(false);

        if (!movedFirst)
        {
            try
            {
                await service.AdvanceAsync(
                    sessionId, "working", note: workingNote?.Note, transcript: transcript, ct: ct, parts: workingNote?.Parts).ConfigureAwait(false);
            }
            catch
            {
                // 🔴 The record would not become this run's, as where a second resume met working, so the run is ended here, and what
                // was reading its output is observed: its failure is written with its reason, never left unawaited.
                await run.AbandonAsync().ConfigureAwait(false);
                throw;
            }
        }

        working?.Invoke();

        // The exit, the capture bounded past it, the process's lineage end at its drain, and what it consumed where the door reported
        // it: a pipe with only text reports nothing, and a surface then says "not measured" rather than zero.
        var end = await run.EndAsync(ct).ConfigureAwait(false);
        var turnFailed = protocol ? end.Ending.Failure : null;
        List<ChatMessage> left = [.. acpLeft];

        // 🔴 A native resume whose harness ended on a failed exit before it named its conversation proves nothing of its loss, so it is
        // tried once more as the same resume, conversation, account and access, before its conclusion reads a second such end as
        // `exited`. Never after a stop.
        if (structured && resume is { Named: false, Refused: null, Retried: false } && end.Exit is int early && early != 0
            && !context.Processes.WasStopRequested(sessionId))
        {
            end = await run.RestartAsync(ct).ConfigureAwait(false);
        }

        // 🔴 A session never concludes while words are held: on the native door its process has exited, so its own conversation is
        // resumed with them through the run, under the same record, which stays working, until a run ends with none.
        if (held is not null)
        {
            end = await wiring.GoOns.GoOnAsync(run, held, adapter, sessionId, end, resume, left, ct, own, setupChanged, carriesOn).ConfigureAwait(false);
        }

        // The run Daoris ended for a changed setup says so on its console as it ends; its record's note says it once concluded.
        if (swapped is not null && left.Count > 0)
        {
            output?.Append(
                sessionId,
                $"— its permission prompts changed from `{swapped.From}` to `{swapped.To}` while it worked: Daoris ended it at its turn's end, "
                + $"never mid-turn, and the {left.Count} word(s) held for it go on in the same conversation under the new setup at the next look.");
        }

        // The ending is the last run's: the protocol door's, or a native run's retry's or go-on's, read of its own exit and the
        // harness's own words since.
        var concluded = await conclude(
                end.Exit, end.Usage, turnFailed,
                end.Ending with
                {
                    SetupChanged = left.Count > 0 ? swapped : null,
                    Transient = retry,
                })
            .ConfigureAwait(false);

        // The words its failed door never delivered wait on its ended record to go again: kept once the conclusion moved it, since the
        // say door refuses a running record. So do the words a changed setup handed back, to go on under it; unless a stop was made of
        // it since, which ends it with them unreached as any stop does, since words on its record would carry it on past the stop.
        if (left.Count > 0 && swapped is not null && turnFailed is null && context.Processes.StopMade(sessionId) is not null)
        {
            var unreached = WordsNotes.Unreached(left.Count, "never reached the session", UnreachedWhy.EndedFirst);
            output?.Append(sessionId, unreached.Note);
            context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = unreached.Note, Parts = unreached.Parts }, say: null);
        }
        else if (left.Count > 0)
        {
            await WordsLeft.KeepAsync(
                    service, context.Events, sessionId, left, CancellationToken.None,
                    why: swapped is not null && turnFailed is null ? Observation.WordsUnderNewSetup : null, home: home)
                .ConfigureAwait(false);
        }

        return concluded;
    }

    /// <summary>
    /// What a running driven session's permission prompts changed to, read from the driver's file now; null where they did not, or the
    /// file does not read: a write torn half-way is read again at the next turn's end, never taken for a change.
    /// </summary>
    private static SetupSwap? SetupNow(IAgentAccess started, ISessionAdapter adapter, string configPath)
    {
        try
        {
            return started.SwapIn(DriverConfig.Load(configPath), adapter);
        }
        catch (Exception error) when (error is DriverConfigUnreadableException or DriverException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
