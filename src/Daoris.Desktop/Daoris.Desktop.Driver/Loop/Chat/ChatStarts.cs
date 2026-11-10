namespace Daoris.Driver;

/// <summary>
/// The start of a conversation in a repository: its role, harness and account asked before any record exists, the tree it runs in, the open of
/// its record, and the run over its process, which the runner makes. The record is the service's; the process is this machine's, and never
/// leaves it.
/// </summary>
internal sealed class ChatStarts(IExecutorWiring wiring, ChatTold told, ChatConclusion conclusion)
{
    /// <summary>Whether a conversation opens in a tree of its own: said, or else whether its repository's workflow sets its work apart.</summary>
    public static bool Isolates(bool? ownTree, IsolateStep? apart) => ownTree ?? apart is not null;

    /// <summary>Open a chat and put a harness behind it.</summary>
    /// <param name="onEnded">Called when the conversation ends, with the state the record took. The caller decides what that becomes.</param>
    /// <param name="profile">The credential profile this conversation runs as. Null takes the workspace's default, then the machine's, then the harness's own.</param>
    /// <param name="picked">
    /// The person named <paramref name="profile"/> for this start: the page's picker, the terminal's account or their hand-over press's. The
    /// conversation then keeps on its binding that they picked its account, and waits for it. Never an agent's ask, the curve's account or a
    /// limit's move, whose named account keeps today's reading.
    /// </param>
    /// <param name="ownTree">
    /// Open the conversation in a session tree of its own, beside the repository's standing opt-in in the config. Either says yes. Null leaves
    /// it to those two; false is the checkout, whatever the repository's standing choice.
    /// </param>
    /// <param name="tree">
    /// A conversation handed over from another agent runs in the tree the old one ran in: its own folder, neither grown nor removed here, and
    /// refused where it no longer exists. Null opens as every other conversation does.
    /// </param>
    /// <param name="from">The conversation this one continues, with why and who asked: named on the new record as it opens. Null for every other conversation.</param>
    /// <param name="order">
    /// Chat's effective order for a new conversation: one entry runs on its agent, asking its model; more are walked choice by choice, each
    /// choice's accounts before the next. Null for a conversation that continues another, which runs on <paramref name="adapter"/>.
    /// </param>
    /// <param name="leftBy">
    /// The part a conversation that continues another goes on from, as its record says it ran: where it ran on another maker and left work
    /// uncommitted in the tree, the new part is told that maker's trailer too. Null for every other start.
    /// </param>
    /// <param name="settled">
    /// The selection the caller's one walk judged and counted: the conversation opens on it, never walking again, so the account judged is the
    /// account opened and the start is counted once. Null for every other start, which walks here.
    /// </param>
    /// <param name="ownFirst">
    /// <paramref name="profile"/> is the account of the part this one continues, asked for first where it is ready and walked past, within its
    /// agent's accounts, where it is not. False pins <paramref name="profile"/> as a pick, refused while it cools.
    /// </param>
    /// <param name="moves">
    /// The words the open moves onto the new record in its own step, through the keyed door (<see cref="ServiceClient.OpenChatMovingAsync"/>),
    /// which sends them only to a host whose <c>opens</c> mark is 2. What moved comes back on <see cref="ChatStart.Moved"/>. Null for every
    /// other start, which opens as before.
    /// </param>
    /// <param name="carries">A limit's carry: the part runs as its conversation's resume (<see cref="ChatLimitCarry.RunAsync"/>); null for every other start.</param>
    public Task<ChatStart> StartAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded = null, string? profile = null, bool? ownTree = null,
        CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null, IReadOnlyList<ActorEffective>? order = null,
        EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false, bool ownFirst = false, MoveWords? moves = null,
        ChatLimitCarry? carries = null) =>
        StartAsync(repository, adapter, config, onEnded, profile, ownTree, ct, tree, from, order, leftBy, pending: null, settled, picked, ownFirst: ownFirst, moves: moves, carries: carries);

    /// <param name="pending">
    /// The pending start a replacement opens for: its order walked, the refused account passed, its passes said in the replacement's actor line.
    /// Null for every other start, which makes its own where its door opens on the protocol wire.
    /// </param>
    private async Task<ChatStart> StartAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded, string? profile, bool? ownTree,
        CancellationToken ct, string? tree, ContinuedFrom? from, IReadOnlyList<ActorEffective>? order,
        EarlierMaker? leftBy, ChatPending? pending, HarnessSelection? settled = null, bool picked = false,
        bool ownFirst = false, MoveWords? moves = null, ChatLimitCarry? carries = null)
    {
        var context = wiring.Context;
        if (context.Closing) return new(null, "Daoris is closing.");
        var key = Guid.NewGuid().ToString("N");
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Opening[key] = done.Task;
        try
        {
            if (context.Closing) return new(null, "Daoris is closing.");
            return await StartCoreAsync(repository, adapter, config, onEnded, profile, ownTree, ct, tree, from, order, leftBy, pending, settled, picked, ownFirst, moves, carries)
                .ConfigureAwait(false);
        }
        finally
        {
            done.TrySetResult();
            context.Opening.TryRemove(key, out _);
        }
    }

    // Asked before the record is opened: a session record for a conversation that could never have happened would hold the repository and
    // explain nothing.
    private static string NotInteractive(string adapter) =>
        $"the `{adapter}` adapter does not hold conversations — it spawns an agent that takes its target once and runs to completion.";

    private async Task<ChatStart> StartCoreAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded, string? profile, bool? ownTree,
        CancellationToken ct, string? tree, ContinuedFrom? from, IReadOnlyList<ActorEffective>? order, EarlierMaker? leftBy,
        ChatPending? pending, HarnessSelection? settled, bool picked, bool ownFirst, MoveWords? moves, ChatLimitCarry? carries)
    {
        var context = wiring.Context;
        var home = context.Home;
        var service = context.Service;
        var harnesses = context.Harnesses;

        // An order of one is today's start on its agent, asking its model; more are walked below, once the workspace whose accounts each choice
        // walks is known. A replacement always walks its pending start's order.
        if (pending is not null) order = pending.Order;
        var chosen = order is [var only] ? only : null;
        if (chosen is not null) adapter = chosen.Agent;
        var walking = order is { Count: > 1 } || pending is not null;

        // Through the roster's live set, not the one the runner is built with: the driver's tick hands the roster the build's adapters plus
        // whatever the plugins declare, so a harness declared since the shell started is a harness a conversation can run on now.
        var resolved = walking ? null! : harnesses.Adapters.Door(adapter);
        if (!walking && !resolved.Can.Interactive) return new(null, NotInteractive(resolved.Id.Value));

        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var known = snapshot.Repositories
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));
        var root = known?.Root;
        if (string.IsNullOrWhiteSpace(root))
        {
            return new(
                null,
                $"`{repository}` has no checkout on this machine — a conversation runs IN a working "
                + "tree. `daoris connect` from inside it, or add it from Repositories.");
        }

        // The role that takes chat at this workspace, found now that the workspace is known. The seeded catalogue answers Chat, whose order the
        // caller already holds, so nothing changes; an added role taking chat walks its own order, a person's pick (an agent named for this
        // start) kept as the pick it is. A conversation that continues another (no order) keeps its agent.
        var place = new RolePlace(known!.Workspace);
        var roleTaken = "chat";
        long roleRevision = 0;
        try
        {
            var roles = RoleCatalog.ReadHome(home);
            var found = roles.RoleFor(ParticipantWork.Chat, place);
            if (found.Held is { } held) return new(null, held);
            if (found.Role is not { } role) return new(null, "No role takes chat on this machine, so it starts no conversation until one does.");
            (roleTaken, roleRevision) = (role.Id, roles.RevisionOf(role.Id));
            // Only the seeded Chat order a caller holds is replaced: an order read from the role the line is bound to (a limit's other choices,
            // a refused open's) is that role's already, and is walked as given.
            if (order is not null && pending is null && roleTaken != "chat" && order is not [{ From: "pick" }]
                && order.SequenceEqual(ActorChoices.Effective("chat", config)))
            {
                order = roles.Order(ParticipantWork.Chat, place, config);
                if (order.Count == 0) return new(null, $"{role.Name.In("en")} is off on this machine, so it starts no conversation until it is given a choice.");
                chosen = order is [var sole] ? sole : null;
                if (chosen is not null) adapter = chosen.Agent;
                walking = order.Count > 1;
                resolved = walking ? null! : harnesses.Adapters.Door(adapter);
                if (!walking && !resolved.Can.Interactive) return new(null, NotInteractive(resolved.Id.Value));
            }
        }
        catch (DriverException error)
        {
            return new(null, error.Message);
        }

        // The harness and the account, asked before the record exists, the same place the `interactive` question is asked and for the same
        // reason. The circle comes from the registry row, which is the machine's own wiring and the only honest source for it.
        HarnessSelection selection;
        IReadOnlyList<BuildPass> passes = [];
        if (walking)
        {
            var walk = await wiring.WalkAsync("chat", order!, config, known!.Workspace, profile, ct, pending?.Refusing).ConfigureAwait(false);
            if (walk is not { Adapter: { } walked, Selection: { } taken, Chosen: { } entry }) return new(null, walk.Refusal!);
            (resolved, selection, chosen, passes) = (walked, taken, entry, walk.Passes);
            if (pending is not null) passes = [.. pending.Passes, .. walk.Passes];
        }
        else if (settled is not null)
        {
            // The caller's walk is the walk: asking the roster again could name another account, and counts a second start.
            selection = settled;
        }
        else
        {
            // The own account first where ready, else the walk of its agent's accounts, as a handed quest's named one is.
            selection = ownFirst && profile is not null
                ? (await harnesses
                    .SelectHandedAsync(resolved.Id.Value, config, known!.Workspace, profile, StartKind.Conversation, ct, chosen?.Model)
                    .ConfigureAwait(false)).Selection
                : await harnesses
                    .SelectAsync(resolved.Id.Value, config, known!.Workspace, profile, StartKind.Conversation, ct, chosen?.Model)
                    .ConfigureAwait(false);
            if (!selection.Allowed) return new(null, selection.Refusal!);
        }

        // A tree of the conversation's own, when asked for, per conversation or as the repository's standing choice. Grown before the record,
        // like every refusal above this line. Its repository's trees are held from here until the record is open, as a driven start holds
        // them, so bringing the repository up to date does not rebase the tree under it.
        if (tree is not null && !ConversationTrees.Stands(home, tree))
        {
            return new(null, "the tree the conversation ran in is gone, so there is nothing for a new one to go on in.");
        }

        var isolated = tree is null && Isolates(ownTree, ownTree is null ? PlaceIsolates.Of(home, config, repository, known?.Workspace) : null);
        // In the checkout itself too, so no writer moves it between the commit read below and the record that holds it.
        var startIn = tree is not null ? StartIn.In(tree) : isolated ? StartIn.Fresh : StartIn.In(root!);
        using var starting = TreeLock.TryStarting(home, root!, startIn);
        if (starting is null) return new(null, TreeLock.StartHeld(home, root!, startIn, known.Repository).Why);

        TreeOpened? opened = null;
        if (isolated)
        {
            try
            {
                opened = await new SessionTrees(home)
                    .OpenAsync(root!, known.Repository, known.Workspace, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return new(null, error.Message);
            }
        }

        var workTree = tree ?? opened?.Path ?? root!;

        // Where the tree stands before the conversation begins. A chat may open on a dirty tree, so this is the commit, not the working state,
        // and the review it feeds is committed work only, for exactly that reason.
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);

        // A move that hands the part words opens it through the keyed door, which moves them in the open's own step against a host whose mark
        // is 2; so does the replacement of a start refused at the open, its moving words kept there named; every other start opens as before.
        var keyed = moves is not null
            ? await service
                .OpenChatMovingAsync(repository, resolved.Id.Value, selection.Version, selection.Profile, workTree, before, from, moves, known.Workspace, ct)
                .ConfigureAwait(false)
            : null;
        var (sessionId, message) = keyed is not null
            ? (keyed.SessionId, keyed.Message)
            : await service
                // The tree the conversation runs in: its own where one is grown, the checkout found above otherwise, stated by the side that
                // is about to spawn into it.
                .OpenChatAsync(
                    repository, resolved.Id.Value, selection.Version, selection.Profile, workTree, before, ct, from)
                .ConfigureAwait(false);
        if (sessionId is null)
        {
            // Fresh and workless, so the clean path removes it; anything already in it is kept.
            if (opened is not null) await new SessionTrees(home).RemoveAsync(opened.Path, ct: ct).ConfigureAwait(false);
            return new(null, message);
        }

        ChatAgent agent;
        bool ours;
        var answers = false;
        IReadOnlyList<string> continued;
        // Everything from the record's open to the run is one stretch: a failure or a cancel in it concludes the record, which the run's own
        // failure does for what comes after, so no record is left queued with only this process going to end it.
        try
        {
            // The role this start took and its revision, on its binding beside its pick, which a continuation reads.
            context.Conversations.KeepRole(sessionId, roleTaken, roleRevision);
            carries?.Bound(sessionId);
            // A start the person named an account for keeps, on its binding in the desktop store and nowhere else, that they picked the account it
            // opened on, so the account rule holds the conversation to it. Never the walk's own choice.
            if (picked) context.Conversations.Pick(sessionId, selection.Profile);
            // A pick belongs to the conversation, so a new part any move opens on the account the person picked, on the same agent, keeps it and
            // the rule reads it there as on the part it came from. The same agent is the same provider (s-vii), so a move between one agent's two
            // doors keeps it, and one between two providers sharing a pool does not. A part on another account or agent is no pick of the
            // person's; an agent's pick and a record from before hold none.
            else if (from is not null && context.Conversations.PickedOn(from.Session, selection.Profile)
                && await service.RecordAsync(from.Session, ct).ConfigureAwait(false) is { Adapter: { Length: > 0 } pickedAgent }
                && new AgentOfDoor(harnesses.Adapters).Same(pickedAgent, resolved.Id.Value))
            {
                context.Conversations.Pick(sessionId, selection.Profile);
            }

            // The record is open, so the ledger holds the tree and a replay's own look sees it in use.
            starting?.Dispose();
            // Marked as this process's start until its spawn's marker replaces it, so a sweep ends it only if this process goes.
            context.Processes.MarkOpening(sessionId);

            // A new conversation holds no context yet, so it rotates like a start, and only here, at its opening: its account is its process's
            // home, set at spawn. Its first line says which account and why, and, where it goes on from a limit, the conversation it carries and
            // the turn the limit refused.
            RotatedOpening.Say(
                service, context.Events, sessionId, resolved.Id.Value, selection, from is { Why: HandOverWhy.Limit } ? wiring.LimitCarried(from.Session) : null);
            // Where a later choice of Chat's order ran, its actor line says which and why, in the local conversation only.
            if (chosen is not null) RotatedOpening.SayActor(context.Events, sessionId, "chat", chosen, passes);
            await RotatedOpening.SayStaffAsync(context.Events, harnesses.Staff(), service, null, sessionId, selection, "chat").ConfigureAwait(false);

            // A new conversation that asks its actor's entry on the protocol door owns its attempt from here until it is dispatched or exhausted;
            // a replacement goes on under the pending start it opens for, its issued id the press's.
            if (pending is not null)
            {
                pending.Entry = chosen!;
                context.Pending.Link(pending, sessionId);
            }
            else if (chosen is not null && resolved.Can.SetAtOpen)
            {
                // The replacement runs where the refused record ran: its own tree kept, or the checkout. Its open moves the moving words the
                // refused record keeps, which it names and read, and any of the person's kept there since, in its own step.
                var ranIn = tree ?? opened?.Path;
                context.Pending.Add(new ChatPending(
                    sessionId, "chat", ChatPending.Recovering("chat", order!, config, harnesses.StaffFor(home), roleTaken), chosen, profile, known.Workspace,
                    (owner, token) => StartAsync(
                        repository, adapter, config, onEnded, owner.Pinned, ownTree: ranIn is null ? false : null, token, ranIn,
                        new ContinuedFrom(owner.Current, HandOverWhy.NotOffered, HandOverBy.Daoris), order: null, leftBy, owner,
                        moves: MovedWords.Asked(owner.Current, owner.KeptIds, owner.KeptIds))));
            }

            // The agent the walk settled on, as the conversation reads it from here on: what it is told, and the run that holds it.
            agent = new ChatAgent(harnesses.Adapters, resolved);

            // A conversation in a tree it grew is told its process with its first message. A handed-over conversation also needs its new maker's
            // attribution: the record it continues may carry a different maker's instruction. Any other conversation, in the person's checkout or
            // Ask Daoris's, is told nothing ahead of the person's words.
            // Its run binds here, the record being its line's first in the tree it grew, and the landing it is told is planned with the process its
            // run is bound to, so it is told the landing its press makes. A chat run whose process names a second opinion is handed the tool that
            // answers its findings from its spawn.
            // The files step after its record, only in a tree of Daoris's, never the checkout a conversation may open in.
            ours = root is not null && new SessionTrees(home).Holds(workTree);
            var files = ours
                ? await TreeFilesStep.RunAsync(
                    home, root!, workTree, known.Repository,
                    line => context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null }, say: null),
                    ct).ConfigureAwait(false)
                : TreeFilesOutcome.None;
            // In the person's own checkout, read under the lease its open took, as a driven turn's start is.
            if (!ours && await ChatPlaceTold.ToldAsync(home, known.Workspace, known.Repository, workTree, sessionId, from?.Session, ct).ConfigureAwait(false) is { } inPlace)
            {
                told.HoldPlace(sessionId, inPlace);
            }

            if (opened is not null)
            {
                var bound = context.ChatRuns.Opened(sessionId, known.Repository, known.Workspace, config);
                answers = ChatRunBinds.AnswersFindings(bound);
                told.HoldProcess(sessionId, await told.ProcessAsync(opened.Path, sessionId, config, known, files.Told, agent, ct, bound).ConfigureAwait(false));
            }
            else if (from is not null)
            {
                answers = await context.ChatRuns.AnswersAsync(sessionId, workTree, config).ConfigureAwait(false);
                var commits = CommitRules.Resolve(config, known.Repository, known.Workspace);
                // A part on another maker than the one before it, which left work uncommitted in the tree, is told that maker's trailer too, as a
                // driven carry-on is.
                var left = leftBy is not null && await WorkingTree.UncommittedAsync(workTree, ct: ct).ConfigureAwait(false) is { Count: > 0 } ? leftBy : null;
                told.HoldProcess(sessionId, new ProcessBriefing(agent.Trailers(left), commits.Rule.Mode, commits.Source));
            }

            // A conversation that continues another reads the kept folders of its line's parts, whose files its brief names.
            continued = from is null ? [] : await ContextBriefs.LineIdsAsync(service, from.Session, ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            told.Forget(sessionId);
            var cancelled = error is OperationCanceledException && ct.IsCancellationRequested;
            await conclusion.ConcludeAsync(
                sessionId, "failed",
                cancelled
                    ? Noted.Of(ChatNoteCodes.ChatCancelled, "the request that started it was cancelled before its process started.")
                    : Observation.Failure(error.Message),
                onEnded).ConfigureAwait(false);
            if (cancelled) throw;
            return new(null, error.Message);
        }

        ChatStart? started = null;
        try
        {
            // The conversation's own step before its first turn: the tree's unworked mark off.
            if (ours) TreeFilesStep.Hand(home, workTree);
            var placed = wiring.RepositoryPlace(config, snapshot.Repositories, known, repository, workTree, continued, answers);
            started = carries is not null
                ? await carries.RunAsync(sessionId, message, agent, config, placed, onEnded, keyed?.Moved ?? [], ct).ConfigureAwait(false)
                : await wiring.RunAsync(
                    "chat", sessionId, message, agent, selection, config, placed, onEnded, ct, chosen: chosen, pending: context.Pending.For(sessionId))
                    .ConfigureAwait(false);
            // What the open moved, for the move that asked it to hand the new part.
            return started = keyed is null ? started : started with { Moves = keyed.Moves, Moved = keyed.Moved };
        }
        finally
        {
            // A start that fails, or is cancelled, has no first message to tell it with.
            if (started?.SessionId is null)
            {
                told.Forget(sessionId);
            }
        }
    }
}
