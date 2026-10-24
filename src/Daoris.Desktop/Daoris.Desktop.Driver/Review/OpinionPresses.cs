namespace Daoris.Driver;

/// <summary>What a press at the second opinion's gate came to: whether it was taken, its sentence, and the gate after it.</summary>
public sealed record OpinionPressed(bool Done, string Message)
{
    /// <summary>The landing gate as it stands after the press; null where the session's work could not be read.</summary>
    public LandingGate? Gate { get; init; }
}

/// <summary>
/// The person's presses at the second opinion's gate, the driver's half (XAGENT1f, D155 point 10; the second-agent design §8.5,
/// §9): <i>Ask now</i>, <i>Try again</i>, <i>Ask again</i> and <i>Ask the same agent, fresh</i> (a pass the next look starts),
/// <i>Go on anyway…</i>, <i>I looked myself…</i>, <i>Stop</i>, and the answer an <i>Accept…</i> makes to what it showed. The
/// terminal's <c>daoris-driver opinion</c> and the screen's bridge both press here, so each says the same and keeps the same.
/// </summary>
/// <remarks>
/// <para><b>Only the person's.</b> No connector tool and no Ask Daoris card reaches these (§9, D110): asking spends an account at
/// their choice, and going on, or reading it themselves, is a judgement nobody else has made.</para>
///
/// <para><b>Kept on this machine</b> (D47 §4): an answer is kept at the gate for the chain's work in the repository, bound to the
/// commit it was given at, and on the landing record once the work lands. The opinion itself is the local host's, and no door here
/// writes the person's answer into it.</para>
/// </remarks>
public sealed class OpinionPresses(string home, ServiceClient service)
{
    private readonly SessionTrees _trees = new(home);

    /// <summary>
    /// What a session's work is, for the gate: its tree, its quest, the chain's key and repository, and its run (WORKFLOW1f). A chat run's
    /// (CHATFLOW1d) has no quest: its key is the run's, <c>chat-&lt;first&gt;</c>, and its line's records count the person's turns.
    /// </summary>
    private sealed record Work(string Session, string Tree, string? Quest, string Chain, string Repository, string Workspace, IOpinionWorld World)
    {
        public string? Run { get; init; }

        /// <summary>The chat run the work is, where it serves no quest; null for a quest's.</summary>
        public string? Chat { get; init; }

        /// <summary>A chat run's line in its tree, by its records' ids, oldest first.</summary>
        public IReadOnlyList<string> Line { get; init; } = [];
    }

    /// <summary>
    /// How a chat run's <i>Ask now</i> reaches its conversation (CHATFLOW1d, the chat-flow design §5.2 point 3): the bridge's shell's runner,
    /// which reserves the turns for the capture alone. Null reaches none, and the count is read from the line's records.
    /// </summary>
    public ChatPressDoor? Reach { get; init; }

    /// <summary>The whole landing gate for a session's work, as every door reads it.</summary>
    public async Task<OpinionPressed> GateAsync(string session, CancellationToken ct = default)
    {
        var (work, refused) = await WorkAsync(session, ct).ConfigureAwait(false);
        if (work is null) return new(false, refused!);
        var gate = await _trees.GateAsync(work.Tree, work.Quest, work.World, session, ct, run: work.Chat).ConfigureAwait(false);
        return new(true, gate.Opinion.Says) { Gate = gate };
    }

    /// <summary>
    /// <i>Go on anyway…</i> (§8.5): what is unsettled stays so, the person's answer is kept with their words at the commit that
    /// would land, and the gate's next item follows. Nothing lands here.
    /// </summary>
    public Task<OpinionPressed> AnywayAsync(string session, string? words, string door, CancellationToken ct = default) =>
        SayAsync(session, OpinionPersonSaid.Anyway, words, door, ct);

    /// <summary>
    /// <i>I looked myself…</i> (§8.5): the person's own reading, kept in place of another agent's, with their words. It settles the
    /// opinion's part as an absence is settled.
    /// </summary>
    public Task<OpinionPressed> MyselfAsync(string session, string? words, string door, CancellationToken ct = default) =>
        SayAsync(session, OpinionPersonSaid.Myself, words, door, ct);

    /// <summary>
    /// The answer an <i>Accept…</i> makes to what it showed beside it (§8.2–§8.3): kept only where the gate stands as the press
    /// drew it (<see cref="LandingGate.Answers"/>), so a press over a page drawn before the gate moved answers nothing.
    /// </summary>
    /// <returns>The gate after the answer, or the gate as read where nothing was answered.</returns>
    /// <param name="run">A chat run's key where the work serves no quest (CHATFLOW1d): its answer is kept by the run, as its gate is.</param>
    public async Task<LandingGate> PressedAsync(
        string session, LandingGate gate, string? token, string? tree, string? quest, CancellationToken ct = default, string? run = null)
    {
        if (!gate.Answers(token) || tree is null || (quest is null && !ChatRuns.IsChatRun(run)) || gate.Opinion.Tip is not { } tip) return gate;
        var (work, _) = await WorkAsync(session, ct).ConfigureAwait(false);
        if (work is null) return gate;
        new OpinionGates(home).Said(work.Chain, work.Repository, new OpinionPersonWord(OpinionPersonSaid.Press, tip, DateTimeOffset.UtcNow)
        {
            Door = ReviewDoors.Screen,
        });
        return await _trees.GateAsync(tree, quest, work.World, session, ct, run: work.Chat).ConfigureAwait(false);
    }

    /// <summary>
    /// <i>Ask now</i>, <i>Try again</i>, <i>Ask again</i> (§8.5) and the person's ask (§2.1): a first pass on the session's work,
    /// started at the driver's next look and never capped (§8.3). <paramref name="reviewer"/> names one of the rule's reviewers;
    /// <paramref name="sameAgent"/> asks the working agent in a fresh conversation, which is never an independent reading and says
    /// so on its face (§3.3).
    /// </summary>
    /// <param name="occasion"><c>asked</c>, or <c>failure</c> for a struck quest's <i>Ask another agent for help</i>.</param>
    public async Task<OpinionPressed> AskAsync(
        string session, string? reviewer, bool sameAgent, string? words, string occasion, CancellationToken ct = default)
    {
        var (work, refused) = await WorkAsync(session, ct).ConfigureAwait(false);
        if (work is null) return new(false, refused!);

        // WORKFLOW1f: the run's named workflow's opinion step, where it has one, names which of the declared reviewers may read it.
        var config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        var process = WorkflowProcesses.Read(home, config, work.Repository, work.Workspace, work.Run);
        if ((process.OpinionCannot ?? process.Problem ?? process.Unread) is { } cannot)
        {
            return new(false, $"no second opinion is asked for `{work.Repository}`: its workflow cannot start one: {cannot}");
        }

        if (process.Opinion is not { Rule: { IsNone: false } rule })
        {
            return new(false, $"no second opinion is asked for `{work.Repository}`: "
                + (process.Named
                    ? $"its workflow {process.Name} has no second opinion."
                    : $"no rule here names a reviewer. `daoris driver opinion {work.Repository} --reviewers <adapter>` names one."));
        }

        if (reviewer is not null && sameAgent)
        {
            return new(false, "name a reviewer or ask the same agent, not both.");
        }

        if (reviewer is not null && !rule.Reviewers.Contains(reviewer.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return new(false, $"`{reviewer.Trim()}` is not among `{work.Repository}`'s reviewers ({string.Join(", ", rule.Reviewers.Select(each => $"`{each}`"))}): "
                + "a press chooses among what the rule declares, never beyond it.");
        }

        // The look starts what is owed: one owed here at the rule's first occasion, if none was, so the request is read.
        var dues = new OpinionDues(home);
        if (work.Chat is { } chat)
        {
            // CHATFLOW1d (the chat-flow design §5.2 point 3): a chat run's Ask now captures the tree's HEAD and the person's turn count at
            // its press, the turns reserved for the capture alone, and its due is written at `landing`, never at the rule's first occasion.
            var reach = Reach ?? ChatPressDoor.Unheld((_, token) => Task.FromResult(TurnsOf(work)));
            var hold = await reach.Reserve(session, ct).ConfigureAwait(false);
            if (hold == ChatHold.Running) return new(false, "a turn is running; ask for the second opinion once it ends.");
            if (hold == ChatHold.Elsewhere) return new(false, "the conversation is held by another window; ask for the second opinion there.");
            try
            {
                if (await ReviewGit.HeadAsync(work.Tree, ct).ConfigureAwait(false) is not { } tip)
                {
                    return new(false, "git could not read the tree's tip, so no second opinion was asked.");
                }

                dues.Due(new OpinionDue(session, null, work.Repository, work.Workspace, work.Tree, OpinionRules.Landing, DateTimeOffset.UtcNow)
                {
                    Run = chat, Tip = tip, Turns = await reach.Turns(session, ct).ConfigureAwait(false),
                });
            }
            finally
            {
                if (hold == ChatHold.Reserved) reach.Release(session);
            }
        }
        else if (!dues.Open().Any(due => string.Equals(due.Session, session, StringComparison.OrdinalIgnoreCase)))
        {
            dues.Due(new OpinionDue(session, work.Quest, work.Repository, work.Workspace, work.Tree, rule.On[0], DateTimeOffset.UtcNow));
        }

        new OpinionGates(home).Requested(work.Chain, work.Repository, new OpinionRequest(DateTimeOffset.UtcNow, session, occasion)
        {
            Reviewer = reviewer?.Trim(), SameAgent = sameAgent, Words = Words(words),
        });
        var gate = await _trees.GateAsync(work.Tree, work.Quest, work.World, session, ct, run: work.Chat).ConfigureAwait(false);
        var who = sameAgent ? "the same agent, in a fresh conversation," : reviewer is not null ? $"`{reviewer.Trim()}`" : "the rule's first reviewer that can";
        return new(true, $"A second opinion on session {session}'s work is asked: {who} reads it from the driver's next look.") { Gate = gate };
    }

    /// <summary><i>Stop</i> (§8.5): the reviewer's session ended as the person's stop; the opinion it was reading then fails, and says so.</summary>
    public async Task<OpinionPressed> StopAsync(string opinion, SessionProcesses processes, CancellationToken ct = default)
    {
        var view = await service.Opinions.ReadAsync(opinion, ct).ConfigureAwait(false);
        if (view is null) return new(false, $"there is no second opinion `{opinion}` on this machine.");
        if (view.State != OpinionViews.Reading) return new(false, $"second opinion `{view.Id}` is not being read: it is {view.State}.");
        var stopped = await SessionMoves.StopAsync(processes, service, view.Session, ct).ConfigureAwait(false);
        return stopped.Stopped
            ? new(true, $"Stopped {view.Who}'s reading, second opinion `{view.Id}` (session {view.Session}): it gave no opinion.")
            : new(false, stopped.Elsewhere
                ? $"session {view.Session} reading second opinion `{view.Id}` runs on this machine under another Daoris, which stops it."
                : $"nothing on this machine runs session {view.Session} reading second opinion `{view.Id}`.");
    }

    private async Task<OpinionPressed> SayAsync(string session, string said, string? words, string door, CancellationToken ct)
    {
        var (work, refused) = await WorkAsync(session, ct).ConfigureAwait(false);
        if (work is null) return new(false, refused!);

        var before = await _trees.GateAsync(work.Tree, work.Quest, work.World, session, ct, run: work.Chat).ConfigureAwait(false);
        if (before.Opinion.LetsGo)
        {
            return new(false, $"nothing waits for a second opinion on session {session}'s work: {before.Opinion.Says}") { Gate = before };
        }

        if (before.Opinion.Tip is not { } tip) return new(false, "git could not read the commit that would land, so nothing was kept.") { Gate = before };
        new OpinionGates(home).Said(work.Chain, work.Repository, new OpinionPersonWord(said, tip, DateTimeOffset.UtcNow) { Words = Words(words), Door = door });
        var after = await _trees.GateAsync(work.Tree, work.Quest, work.World, session, ct, run: work.Chat).ConfigureAwait(false);
        return new(true, after.Opinion.Says) { Gate = after };
    }

    /// <summary>A session's work as the gate reads it, or why there is none here to answer for.</summary>
    private async Task<(Work? Work, string? Refused)> WorkAsync(string session, CancellationToken ct)
    {
        var (tree, _) = await service.SessionGroundAsync(session, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tree) || TreeFolders.TreeGone(tree) || !_trees.Holds(tree))
        {
            return (null, $"session `{session}` names no working tree of its own on this machine, so no second opinion is asked of its work here.");
        }

        var world = new ServiceReviewWorld(service);
        if (await service.SessionQuestAsync(session, ct).ConfigureAwait(false) is not { } quest)
        {
            // CHATFLOW1d (the chat-flow design §7): a chat in a tree of its own is its chat run's work, its gate kept by the run's key.
            var records = Trace.ParseSessions(await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false));
            if (records.FirstOrDefault(each => string.Equals(each.Id, session, StringComparison.OrdinalIgnoreCase)) is { } record
                && ChatRuns.ChatOf(record, records, _trees.Holds) is { } chat)
            {
                var (chatWorkspace, chatRepository) = _trees.Owner(tree);
                return (new Work(session, tree, null, chat, chatRepository, chatWorkspace, world)
                {
                    Run = chat, Chat = chat, Line = [.. ChatRuns.LineOf(record, records).Select(each => each.Id)],
                }, null);
            }

            return (null, $"session `{session}` serves no quest and runs no chat run of its own here, so no second opinion holds its work.");
        }

        var chain = ReviewGate.ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), quest);
        var (workspace, repository) = _trees.Owner(tree);
        return (new Work(session, tree, quest, TreeGates.OpinionChain(chain, quest), repository, workspace, world)
        {
            Run = WorkflowRunBindings.RunOf(chain, repository) ?? quest.TrimStart('#'),
        }, null);
    }

    /// <summary>The person's turn count over a chat run's line, read from its records' events (§6.1 (b)).</summary>
    private int TurnsOf(Work work) => ChatPresses.TurnsOf(new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)), work.Line);

    private static string? Words(string? words) => words?.Trim() is { Length: > 0 } said ? said : null;
}
