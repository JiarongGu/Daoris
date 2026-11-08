namespace Daoris.Driver;

/// <summary>
/// The session's record, opened through the ledger, which judges the open as any other client's: a refusal is an answer (someone else
/// got there first), not an error. Once asked, the ledger may hold the tree even where a close cuts its answer off, so a close from here
/// leaves it. The tree's base is read before anything runs in it, so the review can read the range back; a carry-on that keeps its move
/// record decides it before the open and asks it under the key the open is sent with, so a crash between the old part's end and the new
/// part's words is taken up and no word is sent twice. The record is open, so the repository's trees go and the ledger holds the tree.
/// </summary>
[RunStage("open", RunPhase.Opened, "continue")]
internal sealed class OpenRecord(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var context = wiring.Context;
        var service = context.Service;
        var home = context.Home;
        var state = run.State;
        var quest = run.Quest;
        var fresh = run.Fresh;
        var (start, fellBack, answerKept, written, switched) = (state.GoneOn.Start, state.GoneOn.FellBack, state.GoneOn.AnswerKept, state.GoneOn.Written, state.GoneOn.Switched);
        var (agent, selection, access, chosen) = (state.Agent, state.Selection, state.Access, state.Chosen);
        var (carryingOn, handsOn, workTree) = (state.Tree.CarryingOn, state.Tree.HandsOn, state.WorkTree);

        // A resume tried let go of the starting hold when the park held the tree; the carry-on's open needs it again, until
        // its own record holds the tree.
        using var restarting = answerKept ? TreeLock.TryStarting(home, start.Root!) : null;
        if (answerKept && restarting is null) return run.Hold(HeldWords.Holding(quest.To, TreeLock.HolderOf(home, start.Root!)).Why);

        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);

        // The session this start continues, why and who asked: the one the quest is handed over from, or one a limit cut off when this
        // runs on another account; every other carry-on names its link too, `retry` where its resume fell back.
        var continues = MovedParts.ContinuesFrom(
            state.HandedQuest, carryingOn ? start.Resumes : null, selection.Profile, selection.Steps?.FirstOrDefault()?.Why,
            retried: fellBack is not null,
            pressed: start.BroughtForward is not null || (start.Resumes?.PersonStopped == true && !state.HandedFrom),
            agent: state.LimitWalks ? agent : null, decided: wiring.Decided,
            policy: state.PolicyMove is { } policied
                ? new CoursePolicy(new StaffAction(policied.Condition, StaffActions.Next, null, null, false, false), DateTimeOffset.UtcNow)
                : null);

        // A carry-on that hands the new session the words of the record it goes on from opens it through the keyed door, which moves
        // them in the open's own step, so no word said between this start's read and its open stays behind on a record the line has left.
        // Against a host whose `opens` mark is below 2 the key goes with no words to move, or none goes, and they are taken as before.
        var handing = fellBack is not null ? written : handsOn;
        var moves = handing is null
            ? null
            : MovedWords.Asked(
                handing.Session, handing.Waiting.Where(DriverContinuations.Hands(resumed: fellBack is null, switched)).Select(word => word.Id),
                handing.Waiting.Select(word => word.Id));

        // The move record is decided before the open: one the quest's hand-over starts goes on from the hand-over's own move. A move
        // already standing for the session it goes on from that is not that one, or a store that will not take the decision, holds the
        // start for the next look: nothing is opened and nothing is written.
        CarryOnRecord? carry = null;
        var openKey = MoveWords.DrivenKey();
        if (CarryOnRecord.Decision(continues, agent, selection.Profile) is { } decision)
        {
            var begun = await wiring.Moves.BeginAsync(decision, ct).ConfigureAwait(false);
            if (CarryOnRecord.Of(wiring.Moves, begun, state.HandedFrom, agent) is not { } decided)
            {
                await fresh.LetGoAsync().ConfigureAwait(false);
                return run.Hold(begun.Refused is { } unwritten
                    ? $"its move to a new part could not be written, so it waits: {unwritten}"
                    : $"a move of `{decision.Left}` to a new part is still standing, so `#{quest.Id}` waits for it to end.");
            }

            carry = decided;
            if ((carry.OldEnded() ?? carry.Opening(openKey)) is { } unasked)
            {
                await carry.EndAsync().ConfigureAwait(false);
                await fresh.LetGoAsync().ConfigureAwait(false);
                return run.Hold($"its move to a new part could not be written, so it waits: {unasked}");
            }
        }

        // Let go on every way out below, ended by its phase unless it is delivered.
        state.Carry = carry;

        fresh.Asked();
        // A carry-on that keeps its record is opened through the keyed door whether or not it hands a word, under the key its record names.
        var keyed = moves is null && carry is null
            ? null
            : await service
                .OpenSessionMovingAsync(
                    quest.Id, agent, selection.Version, selection.Profile, workTree, before, continues, moves, ct,
                    setup: SetupQuests.IsSetup(quest.Title), access: access.Values, opens: carry is null ? null : openKey)
                .ConfigureAwait(false);
        var (sessionId, message) = keyed is not null
            ? (keyed.SessionId, keyed.Message)
            : await service
                .OpenSessionAsync(
                    quest.Id, agent, selection.Version, selection.Profile, workTree, before, ct,
                    setup: SetupQuests.IsSetup(quest.Title), from: continues, access: access.Values)
                .ConfigureAwait(false);
        if (sessionId is null)
        {
            // A fresh tree for a session that never opened has no work in it, so the clean removal path takes it.
            await fresh.LetGoAsync().ConfigureAwait(false);

            // The words of a record the person wrote to are the start's own to say: carried as the hold's sentence, the consideration
            // reads Blocked and the record's row says its words wait and why, not only going on.
            return new StartRun(
                $"refused  #{quest.Id} → {quest.To}: {message}", false, null, start.GoesOn ? $"the service refused the open: {message}" : null);
        }

        // The open's answer with every word it moved, by its id on the new session, then `delivering` and the prompt's mark before the
        // take. A write the store refuses stops the take of the moved words, which wait on the new session for its own go-on.
        var fenced = carry is null ? null : await carry.FenceAsync(sessionId, keyed?.Moved ?? []).ConfigureAwait(false);

        // Its own tree locked in its name while it holds it, so no tool that prunes merged branches takes it; never a reason to hold the start.
        var locksTree = context.Trees.Holds(workTree);
        if (locksTree && await SessionLocks.HoldAsync(workTree, sessionId, ct).ConfigureAwait(false) is { } unlocked)
        {
            context.Log?.Warn("tree.unlocked", ("session", sessionId), ("why", unlocked));
        }

        // The record is open, so the ledger holds the tree and a replay's own look sees it in use.
        state.Starting.Dispose();
        restarting?.Dispose();
        // Marked as this process's start until its spawn's marker replaces it, so a sweep ends it only if this driver goes.
        context.Processes.MarkOpening(sessionId);
        run.OnOpened();
        var openedAt = DateTimeOffset.UtcNow;
        // No question stands while a run of its record works; one the disk keeps is logged.
        wiring.Questions(sessionId, new QuestionFiles(home).Clear(sessionId));

        // Which account it opened on and why: its record's first line names the step that chose it, and, for a carry-on, the cut-off
        // session and the turn a limit refused; a start moved off where its scope begins is `account.rotated` in the log.
        var prior = carryingOn ? start.Resumes : null;
        RotatedOpening.Say(
            service, context.Events, sessionId, agent, selection,
            prior is null
                ? null
                : new RotatedOpening.Carried(
                    prior.Session,
                    prior.Limit ? context.Events.TurnsEnded(prior.Session) + 1 : null,
                    prior.Limit ? RotatedOpening.ContextOf(context.Usage, context.Events, prior.Session) : null));
        // Where a later choice of Build's ran, its actor line beside it, naming each choice passed.
        if (run.MovedOff is not null) RotatedOpening.SayMovedOff(context.Events, sessionId, AccountNames.Said(AccountNames.Of(home, agent), run.MovedOff));
        if (run.MovedOffModel is { } refusedModel) RotatedOpening.SayMovedOffModel(context.Events, sessionId, refusedModel.Agent, refusedModel.Model);
        // A model its agent refused, or a sign-in that walked to another agent, is a move to another choice, counted against `most`.
        var movedRole = state.PolicyRole;
        if (run.MovedOffModel is { } countedModel)
        {
            wiring.CountMove(quest.Id, movedRole, StaffConditions.ModelRefused, new ActorEffective(countedModel.Agent, countedModel.Model, null, ""), chosen?.From);
        }
        else if (run.MovedOff is not null && run.MovedFrom is { } wasAgent && !string.Equals(wasAgent, agent, StringComparison.OrdinalIgnoreCase))
        {
            wiring.CountMove(quest.Id, movedRole, StaffConditions.SignedOut, new ActorEffective(wasAgent, null, null, ""), chosen?.From);
        }

        if (chosen is not null) RotatedOpening.SayActor(context.Events, sessionId, "build", chosen, state.BuildPasses);
        await RotatedOpening.SayStaffAsync(context.Events, context.Harnesses.Staff(), service, context.Log, sessionId, selection, "build").ConfigureAwait(false);
        // A part the role's policy moved here says so, on the record and the opening note, after the start's own why.
        if (state.PolicyMove is { } movedByPolicy && start.Resumes is { } leftPart)
        {
            await SayPolicyMoveAsync(sessionId, state.PolicyRole, quest.Id, movedByPolicy, leftPart, chosen?.From).ConfigureAwait(false);
        }
        else
        {
            // A yes releases one move at the start that follows it: a start that moved nothing closes it, so it releases no later one.
            context.Harnesses.StaffFor(home).Moves.SpendApproval(quest.Id, state.PolicyRole);
        }

        // A quest marked design or exact says whose order it ran on: the role taking that work, Build's while none does.
        var marked = ParticipantWork.OfWeight(quest.Weight);
        if (start.Resumes is null && marked is ParticipantWork.Design or ParticipantWork.Exact && wiring.WeightedRoleName(start, marked, state.StepRole) is { } weighted)
        {
            RotatedOpening.SayWeighted(context.Events, sessionId, marked, weighted);
        }

        // Its setup in one line, after the account's, and the mode it got on its harness, after the change from the session it goes on
        // from where its permission prompts moved since: a start under today's behaviour going on from none that ran otherwise notes
        // nothing, so its transcript reads as it always did.
        if (DrivenAccess.StartNote(access, state.Permissions, start.Resumes) is { } accessNote)
        {
            context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = accessNote }, say: null);
        }

        state.Opened = new RecordOpened(sessionId, keyed, fenced, continues, before, openedAt, locksTree);
        return null;
    }

    /// <summary>
    /// A part the role's policy moved to another choice: the record keeps its <c>chosen</c> (moved, with the condition and the choice
    /// left), the machine log writes <c>actor.moved</c>, and the opening note says it. A service that will not answer costs the record
    /// this, never the run.
    /// </summary>
    private async Task SayPolicyMoveAsync(string sessionId, string role, string line, StaffMoving moving, PriorSession left, string? to)
    {
        var context = wiring.Context;
        var staff = context.Harnesses.StaffFor(context.Home);
        var read = staff.Moves.Of(line);
        var (policy, said) = StaffCourse.MoveSaid(staff, role, line, left, moving.Condition, read.Moves);
        ActorMoves.Write(context.Log, role, line, left.Choice, to, moving.Condition, StaffCourse.ActionOf(policy, moving.Condition), read.Moves);
        await context.Service.TryKeepActorAsync(
                sessionId, context.Log, chosen: new ActorChosen(policy?.Rule ?? StaffRules.Order, StaffBy.Moved, moving.Condition, left.Choice))
            .ConfigureAwait(false);
        RotatedOpening.SayMovedChoice(context.Events, sessionId, StaffCourse.ChoiceOf(left)?.Said ?? left.Adapter ?? "", said);
    }
}
