namespace Daoris.Driver;

/// <summary>
/// The harness and the account the start runs as, asked before its record opens: a record for a spawn that could never have happened
/// would hold the place and explain nothing. A missing binary, an account signed out and an agent the roster does not know are each
/// held with the sentence that names their fix, never thrown out of the look.
/// </summary>
[RunStage("account", RunPhase.Choose, "agent")]
internal sealed class AccountWalk(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var (start, quest, state, context) = (run.Start, run.Quest, run.State, wiring.Context);
        var (handedQuest, stepRole, policyRole) = (state.HandedQuest, state.StepRole, state.PolicyRole);
        string? passedAccount = null;
        ActorEffective? chosen = null;
        IReadOnlyList<BuildPass> buildPasses = [];
        HarnessSelection selection;
        ResumeChoice? resume = null;

        // A record an account's limit ended resumes in its own conversation where its own account is ready again, decided inside the
        // carry-on's own start, never beside the look: the same look would plan the carry-on and walk it onto the account just freed.
        // A hand-over made from it is the person's press and decides the start instead.
        var limitedLine = LimitResume.Line(start.Quest, start.Resumes, state.HandedFrom);

        // A line a limit cut off takes its agent from the role's order, never the machine's adapter key, which the order does not keep
        // in step: its own agent where the role lists it, else the role's first. Where the role lists more than one choice, or its one
        // choice is another agent than the line ran on, it walks the choices as a new start does. A handed quest keeps its agent and waits.
        var limitWalks = false;

        // The move the role's policy decided at that record's end: the line walks as at a limit, the choice it left passed, and its own
        // conversation is not resumed.
        var policyMove = start.Resumes is { } leaving && handedQuest is null
                         && context.Harnesses.StaffFor(context.Home).Moves.Moving().TryGetValue(quest.Id, out var decidedMove)
                         && string.Equals(decidedMove.Session, leaving.Session, StringComparison.Ordinal)
            ? decidedMove
            : null;
        var cutLine = limitedLine is { Limit: true } ? limitedLine : policyMove is not null ? start.Resumes : null;
        if (cutLine is { } cut && handedQuest is null)
        {
            try
            {
                var role = wiring.QuestOrder(start, role: stepRole);
                if (role.Count > 0)
                {
                    state.Agent = role.FirstOrDefault(entry => string.Equals(entry.Agent, cut.Adapter, StringComparison.OrdinalIgnoreCase))?.Agent
                                  ?? role[0].Agent;
                    limitWalks = policyMove is not null || role.Count > 1 || !string.Equals(role[0].Agent, cut.Adapter, StringComparison.OrdinalIgnoreCase);

                    // A role that waits on a limit, or a supervisor's wait on this part, keeps the choice until its `upTo` has passed.
                    if (limitWalks && policyMove is null && cut.Limit
                        && StaffCourse.LimitWait(
                            context.Harnesses.StaffFor(context.Home), policyRole, cut, context.Harnesses.Clock(),
                            new StaffPlace(quest.To, start.Workspace), quest.Id) is LineCourse.Wait)
                    {
                        limitWalks = false;
                    }
                }
            }
            catch (DriverException error)
            {
                return GoneAgentHold.Held(wiring, run) ?? run.Hold(error.Message);
            }
        }

        // A released transient record's words go on alone, on an account no limit cools; only Try again's start with no word waiting
        // takes the limit's go-on.
        var limitedWords = limitedLine is not null && !Planner.EndedTransient(limitedLine);

        // Whether this start resumes that record's own conversation on its own account: the limit's intent, apart from any words waiting.
        var limitResume = false;

        // A continuation whose record asked another agent's choice than this one asks this agent's own: the role's first entry for it,
        // else its default with no model asked. The part before's model is that agent's to refuse, and carried here it ended the part
        // before any prompt. Read from the record's asked agent, so a line a press already moved goes on at the next look.
        state.MovedTo = null;
        try
        {
            if (handedQuest is not null && start.Resumes?.Asked is { } wasAsked
                && !string.Equals(wasAsked.Agent, state.Agent, StringComparison.OrdinalIgnoreCase))
            {
                chosen = state.MovedTo = ApprovedChoice.Of(handedQuest, wiring.QuestOrder(start, role: stepRole))
                                         ?? wiring.QuestOrder(start, handed: state.Agent, role: stepRole)[0];
            }

            if (policyMove is null && wiring.GoOns.ResumesOwn(start, state.Agent) is { } words)
            {
                // A resume asks for its record's own account, whose configuration home holds its conversation, so the walk that chooses
                // a start's account never chooses a resume's. A limited record's words go where its line goes, never waiting on its
                // cooling account while another could take them; so do an ended record's of a line the driver carries on, a closed
                // quest's session's where its chat goes on in a new part and the person's words wait, and Daoris's own word that states a fact.
                var carried = quest.Status is "Open" or "Taken";
                resume = await context.Harnesses
                    .ResumeAsync(
                        state.Agent, context.Config, start.Workspace, words.Profile, StartKind.Driven, context.NewSessions.Covers(words), ct: ct,
                        limited: limitedWords, model: state.CarriedModel(words),
                        line: carried
                            ? LineKind.DrivenOpen
                            : LineKind.DrivenClosed(carries: context.GoesOnLost is not null && words.Waiting.Any(DaorisWords.NewPartCarries)))
                    .ConfigureAwait(false);
                selection = resume.Selection;
                limitResume = limitedWords && resume.Own == NextHold.Ready && selection.Allowed;
            }
            else if (policyMove is null && !start.GoesOn && limitedLine is not null && wiring.GoOns.LimitResumes(limitedLine, state.Agent)
                     && await context.Harnesses
                             .ResumeAsync(
                                 state.Agent, context.Config, start.Workspace, limitedLine.Profile, StartKind.Driven, line: LineKind.DrivenLimit,
                                 ct: ct, limited: true, model: state.CarriedModel(limitedLine))
                             .ConfigureAwait(false) is { } own
                     && await wiring.DecideLimitOwnAsync(limitedLine, own, ct).ConfigureAwait(false) is LineCourse.Resume)
            {
                // Its own account ready, its maker's reading asked, the judge passing and a door known to fail closed: its own
                // conversation resumes there, asked before the walk, so its own account wins where another is ready too.
                resume = own;
                selection = own.Selection;
                limitResume = true;
            }
            else if (handedQuest is null && start.Resumes is null)
            {
                // A new quest's first start walks the role's order, choice by choice, each choice's accounts before the next: its
                // agent is the entry the walk took.
                var walked = await wiring.WalkBuildPassesAsync(start, run.Registry, ct, role: stepRole).ConfigureAwait(false);
                (selection, chosen, buildPasses) = (walked.Selection, walked.Chosen, walked.Passes);
                state.Agent = chosen?.Agent ?? state.Agent;
            }
            else if (limitWalks)
            {
                // A line at a limit walks the same way, each choice asking its own model and effort. Where none can run it waits for
                // the earliest reset of any account of any choice: the walk's Cooling is that soonest, and the hold below says it.
                var walked = await wiring.WalkBuildPassesAsync(start, run.Registry, ct, role: stepRole).ConfigureAwait(false);
                (selection, chosen, buildPasses) = (walked.Selection, walked.Chosen, walked.Passes);
                state.Agent = chosen?.Agent ?? state.Agent;

                // A limit's move to another choice counts against the line's bound, and spends the yes that released it.
                if (limitedLine is { Limit: true } leftLimit && selection.Allowed)
                {
                    StaffCourse.LimitMoved(context.Harnesses.StaffFor(context.Home), policyRole, quest.Id, leftLimit, chosen);
                }
            }
            else
            {
                // A carry-on and a handed quest take the walk as any start does: no context stays with an account. A handed quest asks
                // first for the account its hand-over named, within the named agent's, its model the role's entry for that agent; a
                // carry-on keeps its record's asked choice. Each account is judged by its reading for that model.
                if (handedQuest is not null && start.Resumes is null)
                {
                    chosen = ApprovedChoice.Of(handedQuest, wiring.QuestOrder(start, role: stepRole))
                             ?? wiring.QuestOrder(start, handed: state.Agent, role: stepRole)[0];
                }
                var model = chosen is not null ? chosen.Model : state.CarriedModel(start.Resumes);
                (selection, passedAccount) = await context.Harnesses
                    .SelectHandedAsync(state.Agent, context.Config, start.Workspace, handedQuest?.Account, StartKind.Driven, ct, model)
                    .ConfigureAwait(false);
            }
        }
        catch (DriverException error)
        {
            return GoneAgentHold.Held(wiring, run) ?? run.Hold(error.Message);
        }

        if (resume is not null && wiring.DecideResume(start.Resumes!, quest.Status, resume) is LineCourse.Wait)
        {
            // The words wait for the account's reset, held as any start on a cooling account is, so the look says the wait once and a
            // screen shows the quest waiting for an account; the record's conversation says why, with the door out of the wait. Nothing
            // is spawned or probed to learn it.
            var why = DriverContinuations.WaitsFor(quest, start.Resumes!, resume.Selection);
            wiring.GoOns.HeldSaying(start.Resumes!, ResumeWords.NotYetOn(resume, DriverContinuations.DoorOf(quest, start.Resumes!)));
            return run.Hold(why) with { Cooling = resume.Selection.Cooling };
        }

        if (!selection.Allowed)
        {
            // A line a limit cut off, with no account it may run on ready, waits with a sentence of its own naming the earliest reset
            // and whose account, said on its record once per wait; the next look past it takes it.
            if (limitedLine is not null && selection.Cooling is { } earliest)
            {
                var waits = LimitResume.Waits(
                    earliest, context.Harnesses.Zone, AccountNames.Of(context.Home, earliest.Agent), signedOut: selection.SignedOut?.Accounts);
                wiring.GoOns.HeldSaying(limitedLine, waits);
                return run.Hold(waits.Note) with { Cooling = earliest, SignedOut = selection.SignedOut };
            }

            // The hold carries the cool-off and the accounts passed not signed in, so a screen shows both the wait and the sign-in.
            return run.Hold(selection.Refusal!) with { Cooling = selection.Cooling, SignedOut = selection.SignedOut };
        }

        if (run.MovedOff is not null && string.Equals(selection.Profile, run.MovedOff, StringComparison.OrdinalIgnoreCase))
        {
            return run.Hold("the walk gave the account that was refused for its sign-in, and no other is free");
        }

        state.Selection = selection;
        state.Chosen = chosen;
        state.BuildPasses = buildPasses;
        state.PassedAccount = passedAccount;
        state.LimitWalks = limitWalks;
        state.PolicyMove = policyMove;
        state.Resume = resume;
        state.LimitResume = limitResume;
        return null;
    }
}
