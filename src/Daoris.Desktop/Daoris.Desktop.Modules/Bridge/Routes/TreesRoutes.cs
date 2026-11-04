using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What a session's tree holds and what becomes of it, the page's `bridge/trees.ts`: the review's
/// diff, the files a composer offers, a file's preview, discard, landing
/// and the hand-off.
/// </summary>
public sealed class TreesRoutes(DriverLoop loop, ReadLeaves reads)
{
    private readonly DriverLoop _loop = loop;
    private readonly ReadLeaves _reads = reads;

    /// <summary>
    /// What a landing changes: the session, every plan and gate (the line it merged into is what each
    /// session's plan judges against, LANDSYNC1, so `landing` is never only its own), its review, its hand-off, the clean-up's
    /// list, and which repositories hold Daoris's branches.
    /// </summary>
    private const StateScopes Lands = StateScopes.Sessions | StateScopes.Landing | StateScopes.Diff | StateScopes.HandOff
        | StateScopes.Sweep | StateScopes.TreesSync;

    /// <summary>What discarding a session's tree changes: as a landing, but for the branch a plugin could take, which it keeps.</summary>
    private const StateScopes Discards = Lands & ~StateScopes.HandOff;

    /// <summary>What a press at the second opinion's gate changes: every plan and gate, and the sessions whose reviewer it moved.</summary>
    private const StateScopes Gates = StateScopes.Sessions | StateScopes.Landing;

    /// <summary>
    /// One session's work, as a diff read off its tree, and nothing written: the acts that change it are their own routes. Unreviewable
    /// is information with its own sentence (no tree here, no range, a tree gone). A landed session reads as landed: once its tree
    /// is gone, from the landed branch's refs and objects, never touching the person's checkout. One read per session, shared by the
    /// session alone; <c>READ_LEAVE</c> or a window's end stops its caller, and the last to leave stops the read.
    /// </summary>
    [DriverRoute("SESSION_DIFF", PipeClass.Open)]
    [OneFlight("SESSION_DIFF", By = "id")]
    private async Task<object?> DiffAsync(SessionDiff press, CancellationToken cancellationToken)
    {
        var id = press.Id;

        var service = _loop.Service ?? throw NotReady();

        var (tree, baseCommit) = await service.SessionGroundAsync(id, cancellationToken);
        // a git of this read that outlived its kill is said in the machine log as `git.unended`.
        using var unended = GitUnended.SaidTo(_loop.Log);
        // The reads, their order and their fallbacks are the driver's, so the terminal's `git review` reads through the same code
        //, each start counted and the read said as `review.read`.
        var review = await SessionReviews.ReadAsync(
            id, tree, baseCommit, new SessionTrees(_loop.Home), (repository, ct) => CheckoutOfAsync(service, repository, ct), _loop.Log,
            cancellationToken);
        if (review is null) throw TreesReviewWires.Unreviewable(id, tree, baseCommit);
        return review.FromBranch
            ? TreesReviewWires.ReviewAnswer(id, review.Diff, TreesReviewWires.ReviewSource.Branch, review.Landed, treeGone: review.TreeGone)
            : TreesReviewWires.ReviewAnswer(
                id, review.Diff, TreesReviewWires.ReviewSource.Tree, review.Landed, treeGone: false, uncommitted: review.Uncommitted, behind: review.Behind,
                treeBranch: review.TreeBranch, offer: review.Offer);
    }

    /// <summary>
    /// The page left a review it asked: <c>{ read }</c>, the id its
    /// <c>SESSION_DIFF</c> carried. That call stops waiting, and its read stops only where no other window waits on it. Answers
    /// <c>{ left }</c>: false for an id already answered, or never asked, which changes nothing. A read that changes no state.
    /// </summary>
    [DriverRoute("READ_LEAVE", PipeClass.Window)]
    [Owner("READ_LEAVE", None = OwnerNone.NamesNothing)]
    private object? LeaveRead(ReadLeave press) => new { Left = _reads.Leave(press.Read) };

    /// <summary>Whether <paramref name="tree"/> is one this home opened for a session, which ran in it alone; a person's checkout is not.</summary>
    private static bool OwnTree(SessionTrees trees, string? tree) => SessionReviews.OwnTree(trees, tree);

    /// <summary>
    /// The process a session's own tree runs under, for its page's *Details*: how its work is
    /// committed and how it lands, each with where it was set. Read from the driver file and the tree's place alone: no git, so a
    /// page may ask it for every session it shows.
    /// </summary>
    /// <remarks>
    /// Only a tree this home made is told a process: a session in the person's
    /// checkout, or whose record names no tree here, answers none, as its start told it none. The rules are as resolved now, which a
    /// press of Accept reads; a rule changed since the session's start was not told to it.
    /// </remarks>
    [DriverRoute("SESSION_PROCESS", PipeClass.Open)]
    private async Task<object?> ProcessAsync(SessionProcess press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        return new { Session = id, Process = SessionProcess(DriverConfig.Load(_loop.ConfigPath), new SessionTrees(_loop.Home), tree) };
    }

    /// <summary>
    /// What <c>SESSION_PROCESS</c> says of a tree: its repository's commit rule and landing rule as <c>LINES</c> says each, with where
    /// it was set, the repository and workspace read from the tree's place; null where the tree is not one this home made.
    /// </summary>
    public static object? SessionProcess(DriverConfig config, SessionTrees trees, string? tree)
    {
        if (!OwnTree(trees, tree)) return null;
        var (workspace, repository) = trees.Owner(tree!);
        if (repository.Length == 0) return null;

        var commits = CommitRules.Resolve(config, repository, workspace);
        var landing = LandingRules.Line(config, repository, workspace);
        return new
        {
            Commits = new { Repository = repository, Workspace = workspace, Rule = LinesRoutes.CommitWire(commits.Rule), commits.Source },
            Landing = new
            {
                Repository = repository,
                Workspace = workspace,
                landing.Rule.Form,
                landing.Rule.Pattern,
                landing.Rule.Plugin,
                landing.Rule.AutoAccept,
                landing.Rule.Key,
                landing.Rule.Squash,
                landing.Source,
            },
        };
    }

    // What a person may `@` in a conversation: the files in the tree the record names.
    // Desktop-only for the diff's reason — it is read off a checkout — and read-only.
    /// <summary>
    /// The files in one session's tree, for the composer's `@`.
    /// </summary>
    /// <remarks>
    /// <b>Unlisted is INFORMATION</b>, in the review's class: a record from another machine names no
    /// tree here, and a tree that is gone or not a repository of its own has nothing git will answer
    /// for. One code for both, because the person's next move is the same — type the path, which both
    /// doors expand as typed (`docs/2026-09-25-message-content-evidence.md`).
    /// </remarks>
    [DriverRoute("SESSION_FILES", PipeClass.Open)]
    private async Task<object?> FilesAsync(SessionFiles press, CancellationToken cancellationToken)
    {
        var id = press.Id;

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        var files = string.IsNullOrWhiteSpace(tree)
            ? null
            : await WorkingTree.FilesAsync(tree, cancellationToken);
        if (files is null)
        {
            throw Refusals.Because(
                Refusals.SessionTreeUnlisted,
                "git cannot list this session's tree here — its record names no tree on this machine, or "
                + "the tree is gone or not a repository of its own. A path typed after @ still reaches the agent.",
                ("session", id));
        }

        return new { Session = id, files.Files, files.Unlisted };
    }

    // A file the conversation or the review named, read for the person to look at in the side bar
    //. Desktop-only for the diff's reason, and read-only: there is no editor.
    /// <summary>
    /// One file in a session's tree, for its preview: the tree the record names, which for a
    /// conversation in the repository's checkout is the checkout.
    /// </summary>
    /// <remarks>
    /// <para><b>Only inside that tree</b> (<see cref="FilePreview"/>): a path outside it, a path through a link
    /// that leads out of it, and a path under `.git` are each refused as a code of their own.</para>
    ///
    /// <para><b>The answer names the file as the page asked for it</b>, relative to the tree, and never the
    /// tree's own path, which is machine-local material the page has no use for.</para>
    ///
    /// <para><b>Once the tree is gone, a landed session's file is read from its landed branch</b>:
    /// a finished session is usually a landed one, and a tidy takes its tree, so its preview had nothing to read.
    /// The answer names the branch, so the page says the file is as that branch holds it. The no-tree answer is
    /// left for a session with neither its tree nor the landing's branch here.</para>
    /// </remarks>
    [DriverRoute("SESSION_FILE", PipeClass.Open)]
    private async Task<object?> FileAsync(SessionFile press, CancellationToken cancellationToken)
    {
        var (id, path) = press;

        var service = _loop.Service ?? throw NotReady();

        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (!TreeFolders.TreeGone(tree) || new SessionTrees(_loop.Home).Recorded.Landing(id) is not { } landing)
        {
            return await TreesReviewWires.PreviewAsync(id, tree, path, cancellationToken, _loop.Log);
        }

        var root = await CheckoutOfAsync(service, landing.Repository, cancellationToken);
        return await TreesReviewWires.LandedPreviewAsync(id, root, landing, tree, path, cancellationToken, _loop.Log);
    }

    // Discarding a reviewed session's tree: the PERSON's act, since nothing
    // deletes itself, so its own route rather than anything the diff route could do as a side effect.
    // Its sibling, the merge alone, is retired: the work lands through `LAND_SESSION_TREE`,
    // which merges where the repository's rule says merge.
    /// <summary>
    /// Discard a session's tree.
    /// </summary>
    /// <remarks>
    /// <para><b>A refusal here is an ANSWER, not an error</b> — the tree holds work nobody merged, or
    /// uncommitted work. Each comes back as `{ done: false, message }` with the sentence the tree layer
    /// wrote, exactly as `START_CHAT` does, because the person's next move is different for each and a
    /// code would flatten them into one.</para>
    ///
    /// <para><b>Discard needs `force` said out loud.</b> The unforced call is what produces the
    /// refusal that names what would be lost, so the page asks, shows that sentence, and only then
    /// sends `force`. Destroying work is never a side effect of tidying.</para>
    /// </remarks>
    [DriverRoute("DISCARD_SESSION_TREE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("DISCARD_SESSION_TREE", Session = "id")]
    [GroundSession("DISCARD_SESSION_TREE", "id")]
    [GroundHead("DISCARD_SESSION_TREE", "id")]
    [GroundFiles("DISCARD_SESSION_TREE", "tree", "id", "path", "force")]
    [Says("DISCARD_SESSION_TREE", "id:session")]
    [Publishes("DISCARD_SESSION_TREE", Discards, Session = "id")]
    private async Task<object?> DiscardTreeAsync(DiscardSessionTree press, CancellationToken cancellationToken)
    {
        // `trees remove <path>` names a tree by its path, where the page names its session. A tree by its path is removed as
        // the verb removes it, and its session, where the records name one, is answered; the page's `id` is read as it always was.
        var path = press.Path;
        var id = path is null ? press.Id ?? throw new DriverException("a tree is discarded by its session `id`, or by its `path` — name one.") : null;
        var force = press.Force;

        var service = _loop.Service ?? throw NotReady();

        if (path is not null)
        {
            var owner = SessionBranchDoors.SessionOfTree(await SessionBranchDoors.RecordsOrNoneAsync(service, cancellationToken), path)?.Id;
            var removed = await new SessionTrees(_loop.Home).RemoveAsync(path, force, cancellationToken, service);
            _loop.Nudge();
            return new { Session = owner, Done = removed.Removed, removed.Message, Said = TreesWords.Remove(removed).ToWire() };
        }

        id = id!;
        var (tree, _) = await service.SessionGroundAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(tree))
        {
            throw Refusals.Because(
                Refusals.SessionNotReviewable,
                "this session's record names no working tree on this machine, so there is nothing "
                + "here to discard.",
                ("session", id));
        }

        // The same home the loop derives for the chat runner and the watch: the directory holding
        // `driver.json`. Derived rather than stored twice, so one answer cannot drift from the other.
        var removal = await new SessionTrees(_loop.Home).RemoveAsync(tree, force, cancellationToken, service);
        _loop.Nudge();
        return new { Session = id, Done = removal.Removed, removal.Message, Said = TreesWords.Remove(removal).ToWire() };
    }

    /// <summary>
    /// The branch name the page entered, which the landing takes in place of the rule's; blank or absent is
    /// the rule's, as before. The plan and the press both read it, so what the page shows is what the press does.
    /// </summary>
    public static LandingSubject Entering(LandingSubject subject, string? branch) => subject with { Entered = branch };

    private readonly TreesLandDesk _land = new(loop);
    private readonly TreesHandDesk _hand = new(loop);

    // How a reviewed session's work lands: what a press WOULD do, said before it —
    // merge into the line, or the branch the rule names for this session — and the press, which
    // applies the repository's rule. It is the one door that lands a session's work.
    /// <summary>
    /// A reviewed session's landing: the plan before the press, or the press itself.
    /// </summary>
    /// <remarks>
    /// The pattern is expanded from the session's quest and its title, or for a conversation from the
    /// session and what the person first said — this machine's own record, never the session record's.
    /// A refusal is an answer, as the merge door's are.
    /// </remarks>
    [DriverRoute("LANDING", PipeClass.Open)]
    private Task<object?> LandingAsync(LandingRead read, CancellationToken cancellationToken) =>
        _land.LandAsync(new TreesLandDesk.LandAsk(read.Id, read.Branch, Press: false, read.Said), cancellationToken);

    [DriverRoute("LAND_SESSION_TREE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("LAND_SESSION_TREE", Session = "id")]
    [GroundSession("LAND_SESSION_TREE", "id")]
    [GroundHead("LAND_SESSION_TREE", "id")]
    [GroundRead("LAND_SESSION_TREE", nameof(PipeReads.TreeLanding))]
    [Says("LAND_SESSION_TREE", "id:session")]
    [Publishes("LAND_SESSION_TREE", Lands, Session = "id")]
    private Task<object?> LandSessionTreeAsync(LandSessionTree press, CancellationToken cancellationToken) =>
        _land.LandAsync(new TreesLandDesk.LandAsk(press.Id, press.Branch, Press: true, Answers: press.Answers, Follow: press.Follow), cancellationToken);

    /// <summary>
    /// What a press that reached the landing answers: whether it landed, the driver's sentence, the branch and what its plugin said,
    /// the gate's parts, what it left out or the conflict that refused it, and the verb's words.
    /// </summary>
    /// <remarks>
    /// FABRIC-CLOSE1h9: a push held back lands (<c>done</c>) with no <c>plugin</c>, so
    /// <c>withheld</c> says why it was held, the branch standing and no plugin told, for the page to offer the hand-off. Null wherever
    /// the push went on or was never asked about; a page before it reads the answer as before.
    /// </remarks>
    public static object Pressed(
        string id, TreeLanding landed, LandingGate gate, TreesSaid said, IReadOnlyList<string> followed, string? press = null,
        CarryAnswer? carried = null) => new
    {
        Session = id, Done = landed.Landed, landed.Message, landed.Branch,
        Plugin = landed.Plugin is { } plugin
            ? new { Id = plugin.Plugin, plugin.Pushed, plugin.PullRequest, plugin.Message, plugin.Failed }
            : null,
        landed.Withheld,
        Review = Waits(gate.Review),
        Opinion = Opinion(gate.Opinion),
        Workflow = WorkflowWaits(gate.WorkflowPart()),
        // what it left out, and the conflict that refused it with the words the review offers to say to the session.
        LeftOut = LeftOut(landed.LeftOut),
        Conflict = Conflict(landed.Conflict),
        // what `trees land` prints, the plugin's lines first, and its exit code (§7.3).
        Said = said.ToWire(),
        Followed = followed,
        // what refused a chat run's press, which the page words in the reader's language; null for every other.
        Press = press,
        // LANDBACK1 part 1: the press carried the session on to clear its tree or its conflict (`why`: uncommitted or conflict), and its
        // yes is kept (`told`) until its tree is clean; null for a press that was not carried on. A page before it reads the answer as before.
        Carried = carried is null ? null : new { carried.Why, carried.Told },
    };

    /// <summary>
    /// <c>LANDING</c>'s answer: what a press would do, said before it, beside what holds it and how many paths the tree holds
    /// uncommitted.
    /// </summary>
    /// <param name="leftOutOf">
    /// The branch its line of work landed on where the person left this part out of it, so its review says so and leads with
    /// Discard; null where none did.
    /// </param>
    public static object Planned(
        string session, LandingPlan plan, int uncommitted, LandingGate gate, string? leftOutOf, CarriedAccept? carried = null,
        LandingDelivery? delivery = null) => new
    {
        // the session's newest delivery, where one was written: who authorized it and when, the phase
        // an unresolved one stands at, and when it was delivered; each null where there is none.
        Authorized = delivery is null ? null : new { delivery.Record.Authorization.Authority, delivery.Record.At },
        Running = delivery is { Record.Unresolved: true } ? new { delivery.Record.Intent.Method, delivery.Phase } : null,
        Completed = delivery?.Record is { Settled: DeliverySettlements.Delivered, SettledAt: { } delivered } ? delivered : (DateTimeOffset?)null,
        // Own: the target is the branch the session's agent made, which lands as it stands. Stands: the
        // target already stands, an entry's or a chain's branch, so the press moves it on rather than making it.
        Session = session, plan.Form, plan.Target, plan.Source, plan.Plugin, plan.Problem, plan.Own, plan.Stands, Uncommitted = uncommitted,
        Workflow = WorkflowWaits(gate.WorkflowPart()), Review = Waits(gate.Review), Opinion = Opinion(gate.Opinion),
        // what the press would leave out because the line holds it, what it would land,
        // and a conflict it would be refused for; `leftOut` empty beside `lands` where the line only moved since the work began and the
        // press replays it all; each absent where the work lands as made.
        LeftOut = LeftOut(plan.LeftOut), Lands = plan.Lands?.Select(commit => new { Commit = Short(commit.Sha), commit.Subject }).ToArray(),
        Conflict = Conflict(plan.Conflict),
        LeftOutOf = leftOutOf,
        // LANDBACK1 part 1: the person's Accept is kept on the session, which was carried on to clear its tree and lands when it is clean
        // (`carries` of the Accept's two, `awaiting` while the carry-on's turn runs); null where none is kept. Review shows it as carried on.
        Carried = carried is null ? null : new { carried.Carries, carried.Awaiting },
    };

    /// <summary>The part a session's own Accept is named for: the library's, <see cref="EntryAccepts.EntryLead"/>.</summary>
    public static SessionRecord? EntryLead(IReadOnlyList<SessionRecord> records, string session) => EntryAccepts.EntryLead(records, session);

    /// <summary>A landing's left-out commits on the wire: short id, subject, why, and the line's commit with its patch.</summary>
    public static object[]? LeftOut(IReadOnlyList<LeftOutCommit>? leftOut) => leftOut?
        .Select(commit => new { Commit = Short(commit.Sha), commit.Subject, commit.Why, On = commit.On is { } on ? Short(on) : null })
        .ToArray<object>();

    /// <summary>A replay's conflict on the wire: git's lines, and the rebase words the review's press says to the session.</summary>
    public static object? Conflict(LandingConflict? conflict) => conflict is null ? null : new { conflict.Says, conflict.Words };

    private static string Short(string sha) => TextBounds.ShortCommit(sha, 8);

    /// <summary>The workflow gate's own explanation and terminal doors, shared by the preview and the press.</summary>
    public static object? WorkflowWaits(WorkflowGateState? gate) => gate is null || gate.State == WorkflowGateStates.None
        ? null
        : new { gate.State, Holds = !gate.LetsGo, gate.Says, gate.Outside, gate.Lowered, gate.Otherwise,
            Workflow = gate.Process.Binding?.Workflow, Version = gate.Process.Binding?.Version, gate.Process.Binding?.Kind };

    /// <summary>
    /// What the second opinion's gate says beside a landing's plan or press: its
    /// state, whether it holds, whether the rule requires one, the reviewer and its label, the findings and the disputes counted,
    /// the commits unread, why none could be had, and the sentence; and the token a press sends back to answer what it showed.
    /// Null where the level asks no opinion, so a landing no rule touches answers as it did. The page draws it.
    /// </summary>
    public static object? Opinion(OpinionGateState gate) => gate.State == OpinionGateStates.None
        ? null
        : new
        {
            gate.State,
            Holds = !gate.LetsGo,
            gate.Required,
            Opinion = gate.Opinion?.Id,
            Reviewer = gate.Opinion?.Adapter,
            gate.Opinion?.Product,
            gate.Opinion?.Maker,
            gate.Opinion?.Label,
            Reviewing = gate.Opinion?.Session,
            Findings = gate.Opinion?.Findings?.Count,
            Disputes = gate.Disputes?.Count,
            gate.Since,
            gate.Code,
            Until = Moments.Write(gate.Until),
            gate.Later,
            Person = gate.Person?.Said,
            Answers = gate.PressAnswers ? gate.Token : null,
            Says = gate.Says,
        };

    // The second opinion's presses: the screen's half of `daoris session opinion`,
    // each the driver's own press (OpinionPresses), so both doors say and keep the same. A refusal is an answer, as a landing's is.
    /// <summary>
    /// The second opinion's gate for a session's work, and the person's presses on it: <c>OPINION_GATE</c> reads it;
    /// <c>ASK_OPINION</c> asks a pass the next look starts (<i>Ask now</i>, <i>Try again</i>, <i>Ask again</i>, <i>Ask the same
    /// agent, fresh</i> with <c>sameAgent</c>, or a struck quest's <i>Ask another agent for help</i> with <c>occasion: failure</c>);
    /// <c>OPINION_ANYWAY</c> and <c>OPINION_MYSELF</c> keep the person's answer with their words; <c>STOP_OPINION</c> stops a
    /// reviewer reading.
    /// </summary>
    /// <remarks>
    /// Never Ask Daoris's (§9, D110): asking spends an account at the person's choice, and going on, or reading it themselves, is a
    /// judgement no agent has made. Each answers the gate as it then stands, in <see cref="Opinion"/>'s shape.
    /// </remarks>
    [DriverRoute("OPINION_GATE", PipeClass.Open)]
    private async Task<object?> OpinionGateAsync(OpinionGateRead read, CancellationToken cancellationToken) => read.Said
        ? await OpinionSaidAsync(new("show", read.Id), pressed: false, cancellationToken)
        : OpinionAnswer(read.Id, await Presses().GateAsync(read.Id, cancellationToken), pressed: false);

    [DriverRoute("ASK_OPINION", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("ASK_OPINION", Session = "id")]
    [GroundSession("ASK_OPINION", "id")]
    [GroundHead("ASK_OPINION", "id")]
    [Publishes("ASK_OPINION", Gates, Session = "id")]
    private async Task<object?> AskOpinionAsync(AskOpinion press, CancellationToken cancellationToken)
    {
        var failure = press.Occasion == "failure";
        if (press.Said)
        {
            return await OpinionSaidAsync(
                new("ask", press.Id) { Reviewer = press.Reviewer, SameAgent = press.SameAgent, Failure = failure, Words = press.Words }, pressed: true, cancellationToken);
        }

        var asked = await Presses().AskAsync(press.Id, press.Reviewer, press.SameAgent, press.Words, failure ? "failure" : "asked", cancellationToken);
        return OpinionAnswer(press.Id, asked, pressed: true);
    }

    [DriverRoute("OPINION_ANYWAY", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("OPINION_ANYWAY", Session = "id")]
    [GroundSession("OPINION_ANYWAY", "id")]
    [GroundRead("OPINION_ANYWAY", nameof(PipeReads.OpinionGate))]
    [Publishes("OPINION_ANYWAY", Gates, Session = "id")]
    private async Task<object?> OpinionAnywayAsync(OpinionAnyway press, CancellationToken cancellationToken) => press.Said
        ? await OpinionSaidAsync(new("anyway", press.Id) { Words = press.Words }, pressed: true, cancellationToken)
        : OpinionAnswer(press.Id, await Presses().AnywayAsync(press.Id, press.Words, ReviewDoors.Screen, cancellationToken), pressed: true);

    [DriverRoute("OPINION_MYSELF", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("OPINION_MYSELF", Session = "id")]
    [GroundSession("OPINION_MYSELF", "id")]
    [GroundRead("OPINION_MYSELF", nameof(PipeReads.OpinionGate))]
    [Publishes("OPINION_MYSELF", Gates, Session = "id")]
    private async Task<object?> OpinionMyselfAsync(OpinionMyself press, CancellationToken cancellationToken) => press.Said
        ? await OpinionSaidAsync(new("myself", press.Id) { Words = press.Words }, pressed: true, cancellationToken)
        : OpinionAnswer(press.Id, await Presses().MyselfAsync(press.Id, press.Words, ReviewDoors.Screen, cancellationToken), pressed: true);

    [DriverRoute("STOP_OPINION", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("STOP_OPINION", Session = "id")]
    [GroundWait("STOP_OPINION", "opinion", "opinion")]
    [Says("STOP_OPINION", "id:session", "words:words")]
    [Publishes("STOP_OPINION", Gates)]
    private async Task<object?> StopOpinionAsync(StopOpinion press, CancellationToken cancellationToken)
    {
        if (press.Said) return await OpinionSaidAsync(new("stop", press.Opinion), pressed: true, cancellationToken);
        var stopped = await Presses().StopAsync(press.Opinion, _loop.Processes, cancellationToken);
        _loop.Nudge();
        return new { press.Opinion, Done = stopped.Done, stopped.Message };
    }

    private OpinionPresses Presses() => new(_loop.Home, _loop.Service ?? throw NotReady());

    /// <summary>`daoris session opinion …` sent over the pipe: the verb's own words, a refusal among them as its exit 1.</summary>
    private async Task<object?> OpinionSaidAsync(OpinionCommandAsk asked, bool pressed, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var said = await TerminalSaid.RunAsync(output => OpinionCommand.RunAsync(
            asked, new OpinionPresses(_loop.Home, service), service, _loop.Processes, output, cancellationToken)).ConfigureAwait(false);
        if (pressed) _loop.Nudge();
        return said;
    }

    /// <summary>The gate as it stands after a read or a press, in <see cref="Opinion"/>'s shape; a press first has the loop look.</summary>
    private object OpinionAnswer(string id, OpinionPressed answered, bool pressed)
    {
        if (pressed) _loop.Nudge();
        return new
        {
            Session = id, Done = answered.Done, answered.Message,
            Opinion = answered.Gate is { } gate ? Opinion(gate.Opinion) : null,
            // What the review's *Second opinion* draws: the findings with their answers, beside the state.
            Detail = answered.Gate is { } read ? OpinionDetail(read.Opinion, session => _loop.Usage.Of(session)) : null,
        };
    }

    /// <summary>
    /// The second opinion as the review draws it: the reviewer, the candidate and whether it
    /// covers what would land, each finding with its weight, place, consequence, reproduction, sureness and proposal beside the
    /// working session's answer and how the driver read it, the recheck's word on it, what it read and could not, the tier, and
    /// the person's answer at the gate. Null where the level asks no opinion.
    /// </summary>
    /// <remarks>
    /// Read from the gate the driver judged, never from the host again: the findings are the reviewer's claims (§6.6), and what
    /// counts of an answer (a fix's commit checked, an unanswered finding) is the driver's reading. The tier is <c>person</c> where
    /// the person read it themselves, <c>none</c> where no agent could read it, and <c>agent</c> otherwise (§10).
    /// </remarks>
    public static object? OpinionDetail(OpinionGateState gate, Func<string, UsageEntry?>? usageOf = null)
    {
        // The reviewer's measured use: read by the reviewer's own session; null where none was measured, never 0.
        object? Usage(OpinionView view) => usageOf?.Invoke(view.Session) is { } entry ? new { entry.Used, entry.Size } : null;
        object Pass<T>(OpinionView view, Func<OpinionFindingView, T> beside) => PassOf(view, Usage(view), beside);
        if (gate.State == OpinionGateStates.None) return null;
        var first = gate.Opinion;
        var disputed = gate.Disputes?.First ?? [];
        var raised = gate.Disputes?.Raised ?? [];
        return new
        {
            Tier = gate.State == OpinionGateStates.Myself ? "person" : first is null ? "none" : "agent",
            // Whether what was read covers what would land (§8.2): commits since it was read say it does not.
            Covers = gate.Since is null ? (bool?)null : gate.Since == 0,
            gate.Passes,
            gate.ToPerson,
            gate.Answered,
            Person = gate.Person is { } word
                ? new { word.Said, At = Moments.Write(word.At), word.Words, word.Door }
                : null,
            First = first is null ? null : Pass(first, finding => new
            {
                Answer = first.AnswerTo(finding.Number) is { } answer
                    ? new { answer.Said, answer.Commit, answer.Evidence, answer.Why }
                    : null,
                // How the driver read the answer as the turn ended (§6.4): what counts, and why it does not count as said.
                Counts = gate.Answers?.Findings.FirstOrDefault(row => row.Finding == finding.Number) is { } row
                    ? new { row.Counts, row.Why, row.Fix }
                    : null,
                Rechecked = gate.Recheck?.Rechecked.GetValueOrDefault(finding.Number),
                Disputed = disputed.Contains(finding.Number),
            }),
            Recheck = gate.Recheck is { } recheck ? Pass(recheck, finding => new
            {
                Answer = (object?)null,
                Counts = (object?)null,
                Rechecked = (string?)null,
                Disputed = raised.Contains(finding.Number),
            }) : null,
        };

        static object PassOf<T>(OpinionView view, object? usage, Func<OpinionFindingView, T> beside) => new
        {
            Usage = usage,
            view.Id, view.Occasion, view.Pass, view.State, view.Why, view.Base, view.Tip, Commits = view.Commits.Count, view.Minutes,
            Reviewer = view.Adapter, view.Product, view.Maker, view.Label, Reviewing = view.Session, view.HandedTo, view.Read, view.Limits,
            Findings = view.Findings?.Select(finding => new
            {
                finding.Number, finding.Weight, finding.Where, finding.Claim, finding.Consequence, finding.Reproduce, finding.Sure,
                finding.Proposal, Beside = beside(finding),
            }).ToArray(),
        };
    }

    /// <summary>
    /// What waits on the person at the second opinion's gate on this machine: each work owed
    /// an opinion whose gate holds for the person's answer, oldest first, with the gate as <see cref="Opinion"/> says it. *What needs
    /// you* lists them.
    /// </summary>
    /// <remarks>
    /// <para><b>What waits on the person</b> (<see cref="WaitsOnPerson"/>): a dispute, a required opinion none could be had for, and
    /// commits nobody read where the work lands by itself, since there no press of theirs is coming to answer them.</para>
    ///
    /// <para><b>Read from the look</b>: the gates the driver's last look judged for each opinion owed
    /// (<see cref="DriverLoop.Judged"/>, as every landing door judges them), so a poll runs no git and asks no service. A judgment
    /// is as of that look, at most <see cref="DriverConfig.PollSeconds"/> old; a person's press nudges the loop, so it is not left
    /// waiting a whole poll, and the tick the loop forwards when what waits changes has the page ask again.</para>
    /// </remarks>
    [DriverRoute("OPINION_WAITS", PipeClass.Open)]
    private Task<object?> OpinionWaitsAsync(NoPayload none, CancellationToken cancellationToken)
    {
        _ = _loop.Service ?? throw NotReady();
        return Task.FromResult<object?>(new { Waits = OpinionWaits(_loop.Judged.Latest) });
    }

    /// <summary>
    /// Each judged gate that waits on the person, as <c>OPINION_WAITS</c> answers it and <i>What needs you</i>'s judge reads it:
    /// the work, since when, whether it lands by itself, and the gate as <see cref="Opinion"/> says it, oldest first as the look kept them.
    /// </summary>
    public static IReadOnlyList<object> OpinionWaits(IEnumerable<OpinionJudged>? judged) =>
    [
        .. (judged ?? []).Where(each => WaitsOnPerson(each.Gate, each.Auto)).Select(each => (object)new
        {
            each.Session, each.Quest, each.Repository, each.Workspace,
            Since = Moments.Write(each.Since),
            each.Auto,
            Opinion = Opinion(each.Gate),
        }),
    ];

    /// <summary>
    /// Whether the second opinion's gate waits on the person's answer (the second-agent design §9's *What needs you*): a dispute, a
    /// required opinion none could be had for, or commits nobody read where the work lands by itself (<paramref name="auto"/>).
    /// </summary>
    public static bool WaitsOnPerson(OpinionGateState gate, bool auto) => gate.State switch
    {
        OpinionGateStates.Disputed => true,
        OpinionGateStates.Unavailable => gate.Required,
        OpinionGateStates.CommitsSince => auto,
        _ => false,
    };

    /// <summary>
    /// What the review's gate says beside a landing's plan or press: its state, the environment and the
    /// sentence while it holds the work, so the page can say <i>Waits for your review in <c>environment</c></i> where <i>Accept…</i>
    /// would be; null where nothing waits, so a landing no review touches answers as it did.
    /// </summary>
    public static object? Waits(ReviewGateState review) => review.LetsGo
        ? null
        : new { review.State, review.Decision.Environment, review.Decision.Level, Quest = review.Decision.SetUpStep?.Id, Says = review.Says, review.Decision.ChatRun, review.Decision.LookNode };

    // A branch a landing made, handed to a landing plugin afterwards: what a press would do,
    // said before it, and the press — `daoris tree hand` is the terminal's door.
    /// <summary>
    /// A session's landed branch handed to a landing plugin after its landing: the plan before the
    /// press, or the press. The branch is the one this session's landing made and recorded; a session whose
    /// landing made none answers no branch, which is information, not a failure.
    /// </summary>
    /// <remarks>
    /// A refusal is an answer, as a landing's is. The checkout is the registry's, since the tree may have been
    /// tidied away; a repository with no checkout here is answered in the same sentence form.
    /// </remarks>
    [DriverRoute("HANDOFF_PLAN", PipeClass.Open)]
    private Task<object?> HandOffPlanAsync(HandOffPlan plan, CancellationToken cancellationToken) =>
        _hand.HandOffAsync(new TreesHandDesk.HandAsk(plan.Id, plan.Branch, plan.Repository, plan.Plugin, Press: false, plan.Said), cancellationToken);

    [DriverRoute("HANDOFF", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("HANDOFF", Session = "id")]
    [GroundSession("HANDOFF", "id")]
    [GroundRepository("HANDOFF", "repository")]
    [GroundBranch("HANDOFF", "repository", "branch")]
    [GroundRead("HANDOFF", nameof(PipeReads.HandoffPlan))]
    [Says("HANDOFF", "repository:repository")]
    [Publishes("HANDOFF", StateScopes.HandOff | StateScopes.Sweep, Session = "id")]
    private Task<object?> HandOffPressAsync(HandOff press, CancellationToken cancellationToken) =>
        _hand.HandOffAsync(new TreesHandDesk.HandAsk(press.Id, press.Branch, press.Repository, press.Plugin, Press: true), cancellationToken);

    /// <summary>A repository's checkout here, from the registry — or null where it has none on this machine.</summary>
    internal static async Task<string?> CheckoutOfAsync(ServiceClient service, string repository, CancellationToken cancellationToken)
    {
        var root = (await service.RegistryAsync(cancellationToken))
            .FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Root;
        return string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ? null : root;
    }

    /// <summary>
    /// The hand-off's press, which the review's button and Ask Daoris's card share: the plugin spoken
    /// to, its word kept where the landing's own note is, and the loop told to look again. A static over the loop,
    /// which Help's doors reach by this class's name.
    /// </summary>
    internal static async Task<TreeHand> HandAsync(
        DriverLoop loop, SessionTrees trees, string root, LandedBranch entry, string? plugin, CancellationToken cancellationToken)
    {
        // FABRIC-CLOSE1h8: the result of the session that last moved the branch, read here, judged again just before the
        // plugin is told, so a hand-off that waited behind another push tells no plugin where that result moved meanwhile.
        var pressed = await PressQuests.HandedAsync(entry, loop.Service ?? throw NotReady(), cancellationToken);
        var handed = await trees.HandAsync(root, entry, plugin, cancellationToken, beforePush: pressed.BeforePush);
        if (handed.Plugin is not null) loop.Events.Keep(entry.Session, LandingRules.HandNote(handed), line => loop.Output.Append(entry.Session, line));
        loop.Nudge();
        return handed;
    }
}
