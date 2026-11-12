namespace Daoris.Driver;

/// <summary>
/// How a handed session's run concludes once its process has ended: what the quest and the harness say it is, the record advanced to
/// it and the line the look reports. Built with the facts of the start it concludes, and handed to the hold as its conclusion.
/// </summary>
internal sealed class RunConclusion(
    IRunWiring wiring, RunRound run, RunEndings endings, AgentDoor adapter, SessionTarget target, string transcript)
{
    public async Task<StartRun> ConcludeAsync(int? exitCode, AcpUsage? used, string? turnFailed, HarnessEnding ended, CancellationToken ct)
    {
        var reserved = new List<StartReservation>();
        try
        {
            return await ConcludeReservingAsync(exitCode, used, turnFailed, ended, reserved, ct).ConfigureAwait(false);
        }
        catch
        {
            // The reservation is the start's move to another account, which a conclusion that throws never reaches: nothing else lets it go.
            foreach (var held in reserved) wiring.Context.Runs.Release(held);
            throw;
        }
    }

    private async Task<StartRun> ConcludeReservingAsync(
        int? exitCode, AcpUsage? used, string? turnFailed, HarnessEnding ended, List<StartReservation> reserved, CancellationToken ct)
    {
        bool Reserve(StartReservation moving)
        {
            if (!wiring.Context.Runs.TryReserve(moving)) return false;
            reserved.Add(moving);
            return true;
        }

        var context = wiring.Context;
        var service = context.Service;
        var state = run.State;
        var start = state.GoneOn.Start;
        var quest = run.Quest;
        var selection = state.Selection;
        var (sessionId, before, openedAt) = (state.Opened.SessionId, state.Opened.Before, state.Opened.OpenedAt);
        var workTree = state.WorkTree;

        if (used is not null)
        {
            context.Usage?.Record(new UsageEntry(
                sessionId, quest.To, adapter.Id.Value, selection.Profile,
                used.Used, used.Size, DateTimeOffset.UtcNow));
        }

        // The person's stop outranks the observation: a killed session leaves the same signals as a crashed one, and only this flag
        // knows whose decision the end is.
        var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
        var status = after?.Status ?? "Open";
        var stop = context.Processes.StopMade(sessionId);
        // A clean end holding its quest, on its agent's own open helper ask and no question of the person's, waits on that helper.
        var awaitsHelper = stop is null && exitCode == 0 && status == "Taken"
                           && await wiring.WaitsOnOwnHelperAsync(sessionId, quest, openedAt, ct).ConfigureAwait(false);
        var conclusion = stop is not null
            // The driver's own stop, for a take that lost, stands it down: the quest is someone else's, and the reason says who. The
            // person's, a pause's naming the pause, is theirs, never interrupted. One reading, which a close cutting this off records too.
            ? stop.Conclusion
            : exitCode is int code
                // What it waited on before and after: a NEW question is this session asking and waiting, which is a good ending, not a
                // stand-down.
                ? Observation.Conclude(
                    code, status, quest.Awaits, after?.Awaits, turnFailed, resumed: start.Resumes is not null,
                    // Whose take it is, and what it last said: a session holding the quest it took and stopping is waiting on the person.
                    took: status == "Taken" && await service.TookAsync(sessionId, ct).ConfigureAwait(false),
                    lastWords: SessionLastWords.ParkedWords(context.Events, sessionId, transcript),
                    // The person's reopen of the work it grew from holds its quest as it ends; or they reopened it and went on with the
                    // step while it ran, so it ends as the reopen stopped it, and is carried on with its branch brought up to date.
                    reopenedFrom: Planner.ReopenedFrom(quest, after),
                    // The native door's harness ended its own background work with its turn.
                    backgroundCut: ended.BackgroundCut,
                    // A closure graph's submission is its done, so its session is due, never parked.
                    submitted: QuestResults.Submitted(after),
                    awaitsHelper: awaitsHelper,
                    // Its provider refused its turn, so it failed, and never parks on the person.
                    providerRefused: ended.ProviderRefused,
                    departed: QuestLeft.Departed(after).Count)
                // A done this run reached outranks the timeout, as it outranks a messy exit. The watcher's end (a headless --quiet-end)
                // reads as its own cause, never as a bound's.
                : wiring.Watch.EndedMinutes(sessionId) is { } quietMinutes
                    ? Observation.AfterQuiet(quietMinutes, quest.Status, status)
                    : Observation.AfterTimeout(context.Config.TestBound ?? 0, quest.Status, status);
        // A park whose turn its agent cut asked the person nothing, and says so.
        if (stop is null) conclusion = Observation.Cut(conclusion, ended.Cut);

        // A run Daoris ended at its turn's end for a changed setup is neither a park nor a failure, and goes on.
        var swappedOn = stop is null && ended.SetupChanged is not null;
        if (swappedOn) conclusion = Observation.SetupChanged(conclusion, ended.SetupChanged!);

        // Its agent's ask to hand over, where the person's rule or their yes lets it go, is taken at this turn's end and never mid-turn:
        // the quest is held on the next agent with the agent's note, written before the record says it stopped. Not over the person's
        // stop, nor a run ended for a changed setup.
        if (stop is null && !swappedOn && exitCode is int && status == "Taken"
            && await wiring.HandOverAtTurnEndAsync(sessionId, quest.Id, ct).ConfigureAwait(false) is { } handedTo)
        {
            conclusion = SessionConclusion.Of("stopped", HandOverNotes.QuestHandedOver(handedTo));
        }

        // Its agent refused the choice at the open, before any prompt, so it ends `choice-not-offered`: no strike, its quest planned
        // again, no Ended notice, the reading kept before it ended.
        var notOffered = stop is null && ended.NotOffered is not null;
        if (notOffered) conclusion = Observation.ChoiceNotOffered(adapter.Id.Value, ended.NotOffered!);

        // Past its transient bound a failed end is `ended.transient`, no strike and no cool-off, its quest held for the person; and
        // Daoris's go-on comes off the record by its key where its take-off did not land.
        if (stop is null && ended.Transient?.Episode is { Exhausted: true } exhausted && conclusion.State == "failed")
        {
            conclusion = Observation.Transient(exhausted);
        }

        if (ended.Transient?.Opened == true) await TransientRetries.GoOnOffAsync(service, sessionId, ct).ConfigureAwait(false);

        conclusion = wiring.AccountRefused(conclusion, adapter, selection, ended);
        var beforeSignIn = conclusion;
        conclusion = wiring.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
        // The account this start is refused for, where it has a profile another can stand in for.
        var signedOutOn = !ReferenceEquals(beforeSignIn, conclusion) && stop is null && run.MovedOff is null && start.Resumes is null
            ? selection.Profile : null;
        // The quest is reserved again before this record fails, so a look between this end and the next run's open plans nothing for it;
        // the caller lets it go when that run has opened or come to nothing. Not reserved, it is not moved.
        if (signedOutOn is not null && !Reserve(wiring.MovingReservation(start))) signedOutOn = null;
        // The pipe door refused the model its choice named on a silent turn, so the account's reading is kept and the part ends with its
        // line, no strike; the next look starts what the walk then answers.
        var refusedModel = wiring.ModelRefused(conclusion, adapter.Id.Value, selection, target.Asked?.NamedModel, ended, sessionId);
        var modelRefused = !ReferenceEquals(refusedModel, conclusion);
        conclusion = refusedModel;
        // A model its agent refused, at the open or on a silent pipe turn, moves the same start to the walk's next answer as an account's
        // sign-in does, under the same reservation; the reading it kept makes the walk pass it.
        (string Agent, string Model)? modelRefusedOn = null;
        if ((notOffered || modelRefused) && stop is null && run.MovedOff is null && run.MovedOffModel is null && start.Resumes is null
            && (ended.NotOffered?.Value ?? target.Asked?.NamedModel) is { Length: > 0 } refusedValue
            && Reserve(wiring.MovingReservation(start)))
        {
            modelRefusedOn = (adapter.Id.Value, refusedValue);
        }

        (conclusion, var limited) = wiring.AccountLimited(
            conclusion, adapter, selection, turnFailed, sessionId, used,
            // A limit read by its code on the door, with no sentence: cooled for an unstated hour.
            byCode: ended.Transient?.Last is { Kind: AgentErrorKind.Limit, Limit: { } coded } ? coded : null);

        var commits = await WorkingTree.CommitsSinceAsync(workTree, before, ct).ConfigureAwait(false);
        // A done that waits on its evidence is read here, after the quest and before the record, at the tree's HEAD now, and the verdict
        // goes to the evidence door and beneath the commits on the record.
        var evidence = await EvidenceCheck.AtEndAsync(
                service, after, new EvidenceAt(workTree, EvidenceCodes.SessionEnd) { Base = before, Session = sessionId },
                commits, ct)
            .ConfigureAwait(false);
        // Its question kept only for the park this run ends in, and only where its last turn asked it; settled before the record
        // moves, so a parked record never shows another run's question.
        wiring.Questions(sessionId, new QuestionFiles(context.Home).SettleRun(sessionId, conclusion.State, openedAt, context.Events));
        await service.AdvanceAsync(
            sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct, limit: limited,
            parts: conclusion.Parts).ConfigureAwait(false);
        // Its process has stopped and its record left Running, so its one count is given back, both halves, before the next look
        // selects what the walk now answers.
        if (notOffered) context.Harnesses.Release(selection.Number, sessionId);

        // A set-up step's said set-ups posted with the commit Daoris reads from its tree now, and its build served to its tab from then
        // until the person's verdict.
        await ReviewSetUps.PostAsync(service, context.Config, context.Events, after, sessionId, workTree, start.Workspace, openedAt, ct, wiring.Reviews)
            .ConfigureAwait(false);

        await wiring.ConcludedForOpinionAsync(sessionId, quest, QuestResults.Status(after), conclusion.State, workTree, start.Workspace, ct)
            .ConfigureAwait(false);

        // The tree stays, whole: nothing merges itself and nothing deletes itself. The person merges from the root and discards from a
        // surface that refuses to destroy work. The line names it so a terminal watcher knows where.
        var where = state.Tree.Opened is not null ? $" [own tree: {state.Tree.Opened.Path}]"
            : state.Tree.ResumedIn is not null ? $" [own tree, resumed: {state.Tree.ResumedIn}]"
            : "";
        // A session waiting on its own helper goes on in this tree with the answer, so it keeps it, as a park does.
        var waitsOnHelper = HelperWaits.Ended(conclusion.Parts);
        endings.HoldsTree = SessionStates.IsParked(conclusion.State) || swappedOn || waitsOnHelper;
        return new StartRun(
            $"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}){where}: {conclusion.Note}",
            true,
            // The stop flag is the "whose decision is this" answer, read once, used for both. A park is not an ending: the record
            // waits on the person, and the attention watch says so from the active list. Nor is a run ended for a changed setup, whose
            // record goes on at the next look, nor one waiting on its own helper.
            endings.HoldsTree
                ? null
                : new SessionEnded(
                    sessionId, quest.To, conclusion.State,
                    ByPerson: stop is { ByPerson: true }, conclusion.Note,
                    Quest: quest.Id, Adapter: adapter.Id.Value, Account: selection.Profile)
                {
                    Limit = limited,
                    NotOffered = notOffered || modelRefused,
                    Reason = SessionEnded.ReasonOf(conclusion.Parts),
                })
        {
            SignedOutOn = signedOutOn,
            ModelRefusedOn = modelRefusedOn,
        };
    }
}
