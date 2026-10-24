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
    /// (CHATFLOW1d) has no quest, and its run's key, <c>chat-&lt;first&gt;</c>, is the gate's key in place of a chain's.
    /// </summary>
    private sealed record Work(string Session, string Tree, string? Quest, string Chain, string Repository, string Workspace, IOpinionWorld World)
    {
        public string? Run { get; init; }

        /// <summary>The chat run whose work this is (CHATFLOW1d), where it serves no quest; null for a quest's.</summary>
        public string? ChatRun { get; init; }
    }

    /// <summary>The whole landing gate for a session's work, as every door reads it.</summary>
    public async Task<OpinionPressed> GateAsync(string session, CancellationToken ct = default)
    {
        var (work, refused) = await WorkAsync(session, ct).ConfigureAwait(false);
        if (work is null) return new(false, refused!);
        var gate = await GateOfAsync(work, ct).ConfigureAwait(false);
        return new(true, gate.Opinion.Says) { Gate = gate };
    }

    /// <summary>The whole gate for the work, keyed by its chat run where it has one, so every press here reads what the landing reads.</summary>
    private Task<LandingGate> GateOfAsync(Work work, CancellationToken ct) =>
        _trees.GateAsync(work.Tree, work.Quest, work.World, work.Session, ct, run: work.ChatRun);

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
    /// <param name="run">
    /// The chat run whose press this is (CHATFLOW1d, the chat-flow design §7), keyed by the run where no quest is, so its press answers a
    /// dispute and commits nobody read as a quest's does; null for a quest's.
    /// </param>
    /// <param name="door">The door the press was made at: the screen's, or the terminal's.</param>
    public async Task<LandingGate> PressedAsync(
        string session, LandingGate gate, string? token, string? tree, string? quest, CancellationToken ct = default, string? run = null,
        string door = ReviewDoors.Screen)
    {
        if (!gate.Answers(token) || tree is null || (quest is null && !ChatRuns.IsChatRun(run)) || gate.Opinion.Tip is not { } tip) return gate;
        var (work, _) = await WorkAsync(session, ct).ConfigureAwait(false);
        if (work is null) return gate;
        new OpinionGates(home).Said(work.Chain, work.Repository, new OpinionPersonWord(OpinionPersonSaid.Press, tip, DateTimeOffset.UtcNow)
        {
            Door = door,
        });
        return await _trees.GateAsync(tree, quest, work.World, session, ct, tip: gate.Tip, run: work.ChatRun).ConfigureAwait(false);
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

        // The look starts what is owed: one owed here at the rule's first occasion, if none was, so the request is read. A chat run's is owed
        // at the landing (CHATFLOW1d, the chat-flow design §5.2 point 3), never at a first occasion of `steps`, which would close it past at
        // once; it carries the tip and the person's turn count read at this press, captured with the conversation reserved where a runner
        // here holds it, so its findings are delivered by the press's rule.
        var dues = new OpinionDues(home);
        if (work.ChatRun is { } chatRun)
        {
            var captured = await CaptureAsync(work, ct).ConfigureAwait(false);
            if (captured.Refused is { } why) return new(false, why);
            dues.Due(new OpinionDue(session, "", work.Repository, work.Workspace, work.Tree, OpinionRules.Landing, DateTimeOffset.UtcNow)
            {
                Run = chatRun, Tip = captured.Tip, Turns = captured.Turns,
            });
        }
        else if (!dues.Open().Any(due => string.Equals(due.Session, session, StringComparison.OrdinalIgnoreCase)))
        {
            dues.Due(new OpinionDue(session, work.Quest!, work.Repository, work.Workspace, work.Tree, rule.On[0], DateTimeOffset.UtcNow));
        }

        new OpinionGates(home).Requested(work.Chain, work.Repository, new OpinionRequest(DateTimeOffset.UtcNow, session, occasion)
        {
            Reviewer = reviewer?.Trim(), SameAgent = sameAgent, Words = Words(words),
        });
        var gate = await GateOfAsync(work, ct).ConfigureAwait(false);
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

        var before = await GateOfAsync(work, ct).ConfigureAwait(false);
        if (before.Opinion.LetsGo)
        {
            return new(false, $"nothing waits for a second opinion on session {session}'s work: {before.Opinion.Says}") { Gate = before };
        }

        if (before.Opinion.Tip is not { } tip) return new(false, "git could not read the commit that would land, so nothing was kept.") { Gate = before };
        new OpinionGates(home).Said(work.Chain, work.Repository, new OpinionPersonWord(said, tip, DateTimeOffset.UtcNow) { Words = Words(words), Door = door });
        var after = await GateOfAsync(work, ct).ConfigureAwait(false);
        return new(true, after.Opinion.Says) { Gate = after };
    }

    /// <summary>
    /// The runner that may hold a chat in this process (CHATFLOW1d): <i>Ask now</i> on a chat run reserves it for the capture alone, as the
    /// person's Accept reserves it, so the tip and the turn count are read with no turn starting between them. Null where none does.
    /// </summary>
    public IChatPressing? Holder { get; init; }

    /// <summary>
    /// The tip and the person's turn count a chat run's <i>Ask now</i> captures (CHATFLOW1d, the chat-flow design §5.2 point 3, the Opus check's
    /// finding 5), read as the press reads them, the conversation reserved for the capture alone where a runner here holds it; or why not.
    /// </summary>
    private async Task<(string? Tip, int Turns, string? Refused)> CaptureAsync(Work work, CancellationToken ct)
    {
        var hold = Holder is null ? PressHold.NotHere : await Holder.HoldAsync(work.Session, ct).ConfigureAwait(false);
        if (hold is PressHold.Busy or PressHold.Ending) return (null, 0, ChatPresses.Busy);
        try
        {
            var records = Trace.ParseSessions(await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false));
            var line = records.FirstOrDefault(each => string.Equals(each.Id, work.Session, StringComparison.OrdinalIgnoreCase)) is { } record
                ? ChatRuns.LineOf(record, records).Select(each => each.Id)
                : [work.Session];
            var turns = ChatPresses.TurnsOf(new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)), line);
            return (await ReviewGit.HeadAsync(work.Tree, ct).ConfigureAwait(false), turns, null);
        }
        finally
        {
            if (hold == PressHold.Held) Holder!.Release(work.Session);
        }
    }

    /// <summary>A session's work as the gate reads it, or why there is none here to answer for.</summary>
    private async Task<(Work? Work, string? Refused)> WorkAsync(string session, CancellationToken ct)
    {
        var (tree, _) = await service.SessionGroundAsync(session, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tree) || TreeFolders.TreeGone(tree) || !_trees.Holds(tree))
        {
            return (null, $"session `{session}` names no working tree of its own on this machine, so no second opinion is asked of its work here.");
        }

        if (await service.SessionQuestAsync(session, ct).ConfigureAwait(false) is not { } quest)
        {
            // CHATFLOW1d (the chat-flow design §7): a chat in a tree of its own is its chat run's work, its gate keyed by the run.
            if (ChatRuns.ChatOf(session, await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false), _trees.Holds) is { } chatRun)
            {
                var (chatWorkspace, chatRepository) = _trees.Owner(tree);
                return (new Work(session, tree, null, chatRun, chatRepository, chatWorkspace, new ServiceReviewWorld(service))
                {
                    Run = chatRun, ChatRun = chatRun,
                }, null);
            }

            return (null, $"session `{session}` serves no quest: a conversation's work lands nothing by the driver's gate, so no second opinion holds it.");
        }

        var world = new ServiceReviewWorld(service);
        var chain = ReviewGate.ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), quest);
        var (workspace, repository) = _trees.Owner(tree);
        return (new Work(session, tree, quest, TreeGates.OpinionChain(chain, quest), repository, workspace, world)
        {
            Run = WorkflowRunBindings.RunOf(chain, repository) ?? quest.TrimStart('#'),
        }, null);
    }

    private static string? Words(string? words) => words?.Trim() is { Length: > 0 } said ? said : null;
}
