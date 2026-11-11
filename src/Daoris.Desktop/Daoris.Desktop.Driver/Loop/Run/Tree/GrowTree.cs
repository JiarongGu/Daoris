namespace Daoris.Driver;

/// <summary>
/// The tree the session runs in, found or grown before its record, so a failure here holds the quest open and records nothing: a
/// resume or carry-on goes on in the tree its earlier session worked in, made again on its branch where its folder is gone (a tree
/// never runs gone; a branch gone too holds the start for the person), a further part of a session goes into the tree the planner
/// found free, and any other start grows a tree from the line where the repository opted in. The repository's trees stay held, beside
/// any other start, from here until the record is open, so bringing the repository up to date cannot rebase the tree a session is
/// starting in; the hold is the run's, let go with it.
/// </summary>
[RunStage("tree", RunPhase.Grown)]
internal sealed class GrowTree(IRunWiring wiring) : IRunStage
{
    public async ValueTask<StartRun?> RunAsync(RunRound run, CancellationToken ct)
    {
        var context = wiring.Context;
        var home = context.Home;
        var start = run.Start;
        var quest = run.Quest;
        var root = start.Root!;
        var isolated = start.Isolate is not null;

        var priorTree = start.Resumes is { Tree: { Length: > 0 } resumesIn } && context.Trees.Holds(resumesIn) ? resumesIn : null;
        var entryTree = isolated && start.Entry?.Into is { Tree: { Length: > 0 } partTree } ? partTree : null;
        TreeStanding? priorStands = null, entryStands = null;
        if (priorTree is not null || (entryTree is not null && context.Trees.Holds(entryTree)))
        {
            var returningIn = StartIn.In(priorTree ?? entryTree!);
            using var returning = TreeLock.TryStarting(home, root, returningIn);
            if (returning is null) return run.Hold(TreeLock.StartHeld(home, root, returningIn, quest.To).Why);
            if (priorTree is not null) priorStands = await context.Trees.EnsureAsync(root, priorTree, ct).ConfigureAwait(false);
            if (entryTree is not null && context.Trees.Holds(entryTree))
            {
                entryStands = await context.Trees.EnsureAsync(root, entryTree, ct).ConfigureAwait(false);
            }
        }

        if (priorStands is { Said: { } priorSaid } && start.Resumes is { } goneFrom)
        {
            var landedAway = priorStands.Stand == TreeStand.Gone && context.Trees.Recorded.Landing(goneFrom.Session) is not null;
            // Gone with nothing beyond the line, the part goes on in a fresh tree rather than holding for good.
            var leftNothing = priorStands.Stand == TreeStand.Gone && !landedAway && goneFrom.BaseCommit is { Length: > 0 } stoodAt
                && TreeReturns.LeftNothing(
                    goneFrom, await context.Trees.LineHoldsAsync(root, quest.To, RemoteTarget.Workspace(start.Workspace), stoodAt, ct).ConfigureAwait(false));
            if (priorStands.Stand == TreeStand.Remade) Said(goneFrom.Session, priorSaid);
            else if (leftNothing) Said(goneFrom.Session, TreeReturns.Fresh(priorStands.Branch ?? Path.GetFileName(priorTree!)));
            else if (!landedAway)
            {
                wiring.GoOns.HeldSaying(goneFrom, priorSaid);
                return run.Hold(priorSaid.Note);
            }
        }

        if (entryStands is { Stand: TreeStand.Remade, Said: { } entrySaid }) Said(start.Entry!.Into!.Session, entrySaid);
        else if (entryStands is { Stand: TreeStand.Unmade, Said: { } entryUnmade }) return run.Hold(entryUnmade.Note);

        static bool StandsNow(TreeStanding? stands, string tree) =>
            stands is null ? Directory.Exists(tree) : stands.Stand is TreeStand.Stands or TreeStand.Remade;
        var resumedIn = start.Resumes is { Tree: { Length: > 0 } resumedTree } && isolated && StandsNow(priorStands, resumedTree)
            ? resumedTree
            : null;
        // Words going on are never a resume, whatever question the quest still names: the planner plans them only once a resume began.
        var resuming = start.Resumes is not null && !start.GoesOn && quest.Awaits is { Length: > 0 };
        var carryingOn = start.Resumes is not null && !resuming;
        var joinedIn = entryTree is not null && StandsNow(entryStands, entryTree) ? entryTree : null;

        // Read before the record opens, like every hold above: a question the service no longer knows leaves nothing to resume with.
        QuestView? answered = null;
        if (resuming && quest.Awaits is { Length: > 0 } question)
        {
            answered = await context.Service.FindQuestAsync(question, ct).ConfigureAwait(false);
            if (answered is null)
            {
                return run.Hold($"`#{quest.Id}` waited on `#{question}`, which this service does not hold — "
                    + "there is no answer to resume with.");
            }
        }

        // The words kept on the session that asked while its quest waited, which it never took, go to the session this resume opens,
        // read as the record holds them now so one taken back never goes; so do the person's that waited for a run they ended.
        var handsOn = answered is not null || (carryingOn && Observation.IsQuietPressed(start.Resumes!.NoteParts))
            ? await wiring.GoOns.ResumeWordsAsync(start.Resumes!, ct).ConfigureAwait(false)
            : null;

        // The waited quest's result counts on the line this resume carries on, reserved once however often the start is tried.
        if (answered is not null) await wiring.GoOns.WaitedAsync(start.Resumes!, answered.Id, handsOn, ct).ConfigureAwait(false);

        // A session going on in a tree that stands starts from the line where the line came to hold some of its commits, and is told
        // what is done; a step whose source the person reopened is first brought up to date with the source's new submission. Asked
        // before this start's own hold below, since bringing a tree up to date holds the repository's trees alone.
        var startIn = (resumedIn ?? joinedIn) is { } goesIn ? StartIn.In(goesIn) : isolated ? StartIn.Fresh : StartIn.In(root);
        if (TreeLock.Reserved(home, startIn) is not null) return run.Hold(TreeLock.StartHeld(home, root, startIn, quest.To).Why);
        var inSync = resumedIn is { } reopenedIn && start.Resumes is { } goesFrom
                     && (ReopenTelling.BringsToSource(quest, goesFrom) || Planner.BringsForwardToSource(quest, start))
            ? await GoOnInSyncAsync(reopenedIn, quest, broughtForward: start.BroughtForward is not null, ct).ConfigureAwait(false)
            : (resumedIn ?? joinedIn) is { } goingOn ? await StartInSyncAsync(goingOn, ct).ConfigureAwait(false) : null;

        // A start in the checkout itself holds it too, so no writer moves it between the commit this start reads and the record.
        var starting = TreeLock.TryStarting(home, root, startIn);
        if (starting is null) return run.Hold(TreeLock.StartHeld(home, root, startIn, quest.To).Why);
        run.State.Starting = starting;

        // git's guess at the checkout's line, asked once for the tree's opening and the landing the session is told of.
        var oneLook = context.Trees.OneLook();

        TreeOpened? opened = null;
        if (isolated && resumedIn is null && joinedIn is null)
        {
            try
            {
                // A chain's next step grows from the branch its parent landed on, so a verify step sees the unmerged work it checks;
                // where a tidy took that branch at the landing, from the chain's landed branch, so its own done can move that on.
                opened = await context.Trees.OpenAsync(
                        root, quest.To, start.Workspace ?? "default", ct,
                        from: start.BuildsOn?.Tree is { Length: > 0 } parentTree
                            ? await context.Trees.BranchNowAsync(parentTree, ct).ConfigureAwait(false) ?? $"daoris/{Path.GetFileName(parentTree)}"
                            : null,
                        landed: start.BuildsOn is { } stepBefore
                                && context.Trees.Recorded.Landing(stepBefore.Session) is { GoneAt: null } chain
                                && string.Equals(chain.Repository, quest.To, StringComparison.OrdinalIgnoreCase)
                            ? chain.Branch
                            : entryTree is not null ? EntryLanded(start.Entry!, quest.To) : null,
                        partOf: entryTree is not null ? start.Entry!.Into!.Session : null)
                    .ConfigureAwait(false);
                run.Fresh.Opened(opened.Path);
            }
            catch (DriverException error)
            {
                return run.Hold(error.Message);
            }
        }

        run.State.WorkTree = opened?.Path ?? resumedIn ?? joinedIn ?? root;
        run.State.WorkFolder = new LinePlaces(home).Of(run.State.WorkTree).Folder;
        run.State.Tree = new TreeFound(
            entryTree, priorStands, entryStands, opened, resumedIn, joinedIn, resuming, carryingOn, answered, handsOn, inSync, oneLook);
        return null;
    }

    /// <summary>What became of a session's tree before it went on, said on that session's record: its console and its conversation.</summary>
    private void Said(string session, Noted said)
    {
        wiring.Context.Output?.Append(session, said.Note);
        wiring.Context.Events.Keep(
            session,
            new SessionEvent { Kind = SessionEventKind.Note, Text = said.Note, Parts = said.Parts.Count > 0 ? said.Parts : null },
            say: null);
    }

    /// <summary>Git or a disk that fails here is no reason to hold the start: it goes on in the tree as it stands, told nothing.</summary>
    private async Task<TreeStartSync?> StartInSyncAsync(string tree, CancellationToken ct)
    {
        try
        {
            return await wiring.Context.Trees.StartInSyncAsync(tree, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// A step's tree the person went on with after reopening its source, brought up to date with the source's new submission, read off
    /// the wire now, then with the line. A source the service cannot answer is read as one naming no commit, so only the line is synced.
    /// </summary>
    private async Task<TreeStartSync?> GoOnInSyncAsync(string tree, QuestView step, bool broughtForward, CancellationToken ct)
    {
        QuestView? source = null;
        try
        {
            if (step.Parent is { Length: > 0 } parent) source = await wiring.Context.Service.FindQuestAsync(parent, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException)
        {
            // Read as naming no commit: the session is told only the line is synced.
        }

        try
        {
            return await wiring.Context.Trees.GoOnInSyncAsync(tree, $"#{step.Parent}", Planner.SourceCommit(step, source), ct, broughtForward).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The branch a session's work last landed on here: the newest of its own run's parts' landings in the repository whose record stands.</summary>
    private string? EntryLanded(EntryParts entry, string repository) =>
        entry.Run
            .Select(part => wiring.Context.Trees.Recorded.Landing(part.Session))
            .FirstOrDefault(landing => landing is { GoneAt: null }
                                       && string.Equals(landing.Repository, repository, StringComparison.OrdinalIgnoreCase))
            ?.Branch;
}
