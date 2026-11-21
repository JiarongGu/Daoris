using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The opinion's gate, its rule over one owed opinion at a time: what <see cref="OpinionLook"/> made owed is closed, asked, delivered and
/// sat here, over the loop's context, as the bus's consumer hands it each fact (<see cref="OpinionGateConsumer"/>). The gate delivers, so
/// it holds the delivery, and the delivery passes. What each judgment found is kept in the facts (<see cref="LookFacts"/>), the gate's
/// view, which the look's plan reads (<see cref="Sits"/>, <see cref="Judged"/>) and never judges.
/// </summary>
internal sealed class OpinionGateJudge(LoopContext context, DriverOpinions opinions, OpinionDeliverer deliverer)
{
    private readonly LoopContext _context = context;

    private readonly DriverOpinions _opinions = opinions;

    private readonly OpinionDeliverer _deliverer = deliverer;

    private ServiceClient Service => _context.Service;

    private string Home => _context.Home;

    private DriverConfig Config => _context.Config;

    /// <summary>Told when a pass or a delivery begun beside ends, its owed opinion's subject named: the consumer judges it again.</summary>
    internal Action<string>? BesideEnded { get; set; }

    /// <summary>
    /// One owed opinion judged now, by the gate's rule: closed once past; asked where nothing is yet and the chain's last step here is
    /// done and not held, asked again once a cool-off resets, the person's asks started; delivered until answered; passed by the policy;
    /// or waiting, named. A pass or a delivery it finds owed is begun beside under the chain's claim, so a second judgment meanwhile owes
    /// no second; a draining update holds a pass, said once. What it found is kept in the gate's view, the look's plan woken where it moved
    /// what the plan reads. Null where it closed.
    /// </summary>
    /// <exception cref="HttpRequestException">The service did not answer: the consumer's entry is tried again.</exception>
    internal async Task<OpinionFact?> JudgeDueAsync(OpinionDue due, CancellationToken ct, List<string>? events = null)
    {
        var facts = _context.Runs.Facts;
        var dues = new OpinionDues(Home);
        var world = new ServiceReviewWorld(Service);
        var subject = Subject(due);
        var fact = await JudgeAsync(due, dues, world, await world.QuestsAsync(ct).ConfigureAwait(false), keep: true, beside: true, ct).ConfigureAwait(false);
        var begun = fact?.Act is { } act && !HeldByDrain(act, events) && Begin(act, ct);
        facts.KeepOpinionsOf(dues.Open().Select(Subject));
        if (fact is not null && facts.KeepOpinion(subject, ((IOpinionFact)fact).WithoutAct())) _context.Runs.FactsMoved();
        return fact is null ? null : fact with { Begun = begun };
    }

    /// <summary>
    /// The quests the owed opinions sit, as the gate's view holds them: each owed opinion's last judgment's, and where none has answered
    /// yet, what it could sit (<see cref="NotJudged"/>), never a start before the opinion. A read of the view: nothing here judges.
    /// </summary>
    internal IReadOnlyDictionary<string, OpinionSit> Sits(Snapshot snapshot)
    {
        var sits = new Dictionary<string, OpinionSit>(StringComparer.OrdinalIgnoreCase);
        foreach (var due in new OpinionDues(Home).Open())
        {
            var found = _context.Runs.Facts.Opinion(Subject(due)) is OpinionFact fact ? fact.Sits.Select(pair => (pair.Key, pair.Value)) : NotJudged(due, snapshot);
            foreach (var (quest, why) in found) sits[quest] = why;
        }

        return sits;
    }

    /// <summary>Each owed landing opinion's gate as its last judgment kept it, oldest first and one per session, as the attention band reads it.</summary>
    internal IReadOnlyList<OpinionJudged> Judged()
    {
        var judged = new List<OpinionJudged>();
        foreach (var due in new OpinionDues(Home).Open())
        {
            if (_context.Runs.Facts.Opinion(Subject(due)) is OpinionFact { Judged: { } kept }) judged.Add(kept);
        }

        Order(judged);
        return judged;
    }

    /// <summary>
    /// Whether an update's drain holds the pass <paramref name="act"/> found owed, said once to <paramref name="events"/> where
    /// the caller has them: a reviewer's session in a copy is work the drain waits on. The opinion stays owed and its quests stay sat.
    /// </summary>
    private bool HeldByDrain(OpinionAct act, List<string>? events)
    {
        if (!Config.Draining) return false;
        if (events is not null)
        {
            _context.DrainHolds(events, "opinion", $"{act.Claim}", $"the second opinion owed on session `{act.Due.Session}`'s work");
        }

        return true;
    }

    /// <summary>An owed opinion's subject: its session's at its occasion, one owed each (<see cref="OpinionDues.Close"/>).</summary>
    internal static string Subject(OpinionDue due) => OpinionBusFacts.Owed(due.Session, due.Occasion);

    /// <summary>
    /// What an owed opinion no judgment has answered for yet holds, read from the look's open quests as the planner holds a
    /// start still opening: under <c>steps</c>, the chain's next steps; under <c>landing</c>, every open set-up step of its repository,
    /// since which chain each is in is what the judgment reads. A wait of one judgment, never a start before the opinion.
    /// </summary>
    internal static IEnumerable<(string Quest, OpinionSit Why)> NotJudged(OpinionDue due, Snapshot snapshot) =>
        // A chat run's opinion holds no quest; its gate is the person's press alone.
        due.OnRun ? []
        : due.Occasion == OpinionRules.Landing
            ? snapshot.Quests.Where(each => each.SetUpIn is not null && each.Status == "Open" && Same(each.To, due.Repository))
                .Select(step => (step.Id, new OpinionSit(
                    $"set-up step `#{step.Id}` waits for a second opinion on the work it shows, which is not judged yet.",
                    new SittingFact(SittingFact.Landing))))
            : snapshot.Quests.Where(each => Same(each.Parent, due.Quest) && each.Status == "Open")
                .Select(next => (next.Id, new OpinionSit(
                    $"waits for a second opinion on the step before it, `#{due.Quest}`, before it starts: it is not judged yet.",
                    new SittingFact(SittingFact.Steps) { Quest = due.Quest })));

    /// <summary>
    /// One owed opinion judged as it stands now: null where it is closed, and closed on file; otherwise the quests
    /// it sits, its landing gate where <paramref name="keep"/> asks for it, and the pass it finds owed, which the caller begins. A delivery
    /// it finds owed takes no slot, so it is begun here, beside.
    /// </summary>
    /// <param name="beside">
    /// Judged beside the look: a pass in flight on the chain's claim owes no other, since the gate it read may predate that
    /// pass's opening; the next judgment reads where it came to.
    /// </param>
    private async Task<OpinionFact?> JudgeAsync(
        OpinionDue due, OpinionDues dues, IOpinionWorld world, IReadOnlyList<QuestView> quests, bool keep,
        bool beside, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        // A due naming a chat run reads the run's process and gate, keyed by the run, and no chain.
        var run = due.OnRun ? due.Run : null;
        var chain = run is null ? ReviewGate.ChainOf(quests, due.Quest) : [];
        var quest = chain.FirstOrDefault(each => Same(each.Id, due.Quest));
        // The run's process, read once for this due and handed to each part below, so a named workflow's opinion
        // step decides whether it is owed, who reads it and what holds.
        var process = WorkflowProcesses.Read(Home, Config, due.Repository, due.Workspace,
            run ?? WorkflowRunBindings.RunOf(chain, due.Repository) ?? due.Quest);
        var closes = run is null
            ? await OpinionLook.ClosesAsync(due, quest, chain, Config, _context.Trees.Recorded, ct, _context.Trees, process).ConfigureAwait(false)
            : await OpinionLook.RunClosesAsync(due, _context.Trees.Recorded, process, ct, _context.Trees).ConfigureAwait(false);
        if (closes is { } closed)
        {
            dues.Close(due.Session, due.Occasion, closed, now);
            return null;
        }

        var key = run ?? TreeGates.OpinionChain(chain, due.Quest);
        var claim = $"{key}/{due.Repository}/{due.Occasion}";
        var inFlight = beside && _context.Runs.OpinionHeld(claim);
        // The landing's gate reads the review beside it, as every landing door does: a review that answered what
        // the opinion showed settles it. It moves no ask below, which read states the review never changes.
        var review = keep && due.Occasion == OpinionRules.Landing
            ? await ReviewGate.ReadAsync(world, Config, Path.GetFullPath(due.Tree), due.Repository, due.Workspace, run is null ? due.Quest : null, ct,
                    process, run: run, home: Home, session: due.Session)
                .ConfigureAwait(false)
            : null;
        var gate = await _context.Trees.OpinionAsync(due.Tree, due.Repository, due.Workspace, run is null ? due.Quest : null, due.Session, world, review,
                due.Occasion, ct, process, run: run)
            .ConfigureAwait(false);
        if (gate.Recheck is { Given: true } rechecked) new OpinionDeliveries(Home).RecheckConsulted(rechecked);
        // A chat's work never lands by itself: its kept gate says so to the attention band.
        var kept = review is null
            ? null
            : new OpinionJudged(due.Session, due.Quest, due.Repository, due.Workspace, due.At,
                run is null && process.Landing?.AutoAccept == true, gate);

        // The person's asks first: Ask now, Try again, Ask again, Ask the same agent, fresh. Not capped.
        OpinionAct? act = null;
        var asked = new OpinionGates(Home).Read(key, due.Repository).Requests.Where(request => Same(request.Session, due.Session)).ToList();
        var recheckCapped = gate is { State: OpinionGateStates.Unavailable, Recheck: { State: OpinionViews.Failed, Why: OpinionViews.Limited } };
        if (asked.Count > 0)
        {
            act = new OpinionAct(due, key, claim, chain, due.Occasion, Person: true, process);
        }
        else if (((gate.State == OpinionGateStates.NotAsked && !gate.Held)
                  || (gate is { State: OpinionGateStates.Unavailable, Code: ReviewerUnavailable.Cooling, Until: not null } && !recheckCapped))
                 // One opinion reads a chain's whole work here: an earlier step's is its last step's to ask.
                 && (due.Occasion == OpinionRules.Steps || !OpinionGate.HasLaterWork(chain, due.Quest, due.Repository))
                 // A run bound to a graph version is asked its opinion only once its record has begun
                 // the node, so what its graph places before the opinion comes first. The person's own ask above is theirs, and goes.
                 && GraphGate.Begun(Home, process, NodeKinds.Opinion))
        {
            act = new OpinionAct(due, key, claim, chain, due.Occasion, Person: false, process)
            {
                Early = gate is { State: OpinionGateStates.Unavailable, Code: ReviewerUnavailable.Cooling, Until: { } before } && before > now,
                Ended = gate.Opinion?.Id,
            };
        }
        // A delivery goes a session on in a turn, which the drain does not count; it waits for a look that does not drain.
        else if ((gate.State is OpinionGateStates.WithSession or OpinionGateStates.ReadAgain || recheckCapped) && gate.Opinion is { } first && !Config.Draining
                 && _context.Runs.TryOpinion(claim))
        {
            OpinionBeside(Subject(due), claim, token => DeliverBesideAsync(due, chain, first, process, token), ct);
        }

        var sits = new Dictionary<string, OpinionSit>(StringComparer.OrdinalIgnoreCase);
        Sit(sits, due, chain, gate);
        return new OpinionFact(sits, kept, inFlight ? null : act)
        {
            Turns = Turns(gate),
            Unread = gate.State == OpinionGateStates.Unread || review?.State == ReviewStates.Unread,
        };
    }

    /// <summary>When what a judgment read of <paramref name="gate"/> turns over though no record moves: its reset, and a reading pass's
    /// end of minutes (<see cref="LookFacts.Stamped"/> keeps the earliest still ahead).</summary>
    internal static IReadOnlyList<DateTimeOffset> Turns(OpinionGateState gate) =>
        [.. ((DateTimeOffset?[])[gate.Until, Reading(gate.Opinion), Reading(gate.Recheck)]).OfType<DateTimeOffset>()];

    private static DateTimeOffset? Reading(OpinionView? pass) => pass is { State: OpinionViews.Reading } ? pass.Due : null;

    /// <summary>
    /// Begin the pass <paramref name="act"/> found owed, beside the look, under the chain's claim: false where a pass or a delivery of this
    /// loop's holds it. The person's ask is taken from the gate as it is begun, and one taken meanwhile leaves nothing to ask.
    /// </summary>
    private bool Begin(OpinionAct act, CancellationToken ct)
    {
        if (!_context.Runs.TryOpinion(act.Claim)) return false;
        OpinionRequest? request = null;
        if (act.Person)
        {
            var taken = new OpinionGates(Home).Take(act.Key, act.Due.Repository, act.Due.Session);
            if (taken.Count == 0)
            {
                _context.Runs.OpinionDone(act.Claim);
                return false;
            }

            request = taken[^1];
        }

        var occasion = request?.Occasion ?? act.Occasion;
        OpinionBeside(Subject(act.Due), act.Claim, token => AskPassAsync(act.Due, act.Key, act.Chain, occasion, request, act.Process, token, act.Early ? act.Ended ?? "" : null), ct, SettledNews(act.Due));
        return true;
    }

    /// <summary>
    /// A chat run's pass, once it comes to an end, is news: the person is asked to press Accept
    /// again once it settles, so a notice naming the conversation says it did. One held to try again is no settling; a quest's pass is none.
    /// </summary>
    private static Func<string, LookClassed?>? SettledNews(OpinionDue due) => due.OnRun
        ? line => line.StartsWith("held ", StringComparison.Ordinal)
            ? null
            : LookClassed.Noticed("opinion", new LookNews(LookSaid.OpinionSettled, $"{due.Run}-{due.At.ToUnixTimeMilliseconds()}",
                Session: due.Session, Workspace: due.Workspace))
        : null;

    /// <summary>The kept gates oldest first, one per session, as the attention band's route reads them.</summary>
    private static void Order(List<OpinionJudged> judged)
    {
        var ordered = judged.OrderBy(each => each.Since).DistinctBy(each => each.Session, StringComparer.OrdinalIgnoreCase).ToList();
        judged.Clear();
        judged.AddRange(ordered);
    }

    /// <summary>
    /// The quests an opinion owed sits: under <c>landing</c>, the chain's set-up step here while an agent is still at work
    /// on the opinion of the work it shows (a dispute does not sit it: the person is about to look); under <c>steps</c>, the chain's
    /// next step while the opinion on the step before is unsettled.
    /// </summary>
    private static void Sit(Dictionary<string, OpinionSit> sits, OpinionDue due, IReadOnlyList<QuestView> chain, OpinionGateState gate)
    {
        if (due.Occasion == OpinionRules.Landing)
        {
            if (!OpinionGateStates.AtWork(gate.State)) return;
            foreach (var step in chain.Where(each => each.SetUpIn is not null && each.Status == "Open" && Same(each.To, due.Repository)))
            {
                sits[step.Id] = new(
                    $"set-up step `#{step.Id}` waits for a second opinion on the work it shows, so you look once at work that "
                    + $"already answered another agent: {gate.Says}",
                    new SittingFact(SittingFact.Landing) { State = gate.State });
            }

            return;
        }

        if (gate.LetsGo) return;
        foreach (var next in chain.Where(each => Same(each.Parent, due.Quest) && each.Status == "Open"))
        {
            sits[next.Id] = new(
                $"waits for a second opinion on the step before it, `#{due.Quest}`, before it starts: {gate.Says}",
                new SittingFact(SittingFact.Steps) { Quest = due.Quest, State = gate.State });
        }
    }

    /// <summary>
    /// A pass or a delivery beside the look, as a start runs: told opened at once, so the look never waits on it, its
    /// line joining a later look's report, and the chain's claim and <paramref name="subject"/>'s stamp let go however it ended.
    /// </summary>
    private void OpinionBeside(
        string subject, string claim, Func<CancellationToken, Task<string>> work, CancellationToken ct, Func<string, LookClassed?>? classed = null) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                var line = await work(ct).ConfigureAwait(false);
                return new StartRun(line, Opened: true) { Look = classed?.Invoke(line) };
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return new StartRun($"opinion  the second opinion on {claim} stopped, and its gate judges it again: {error.Message}", Opened: true);
            }
            finally
            {
                // The claim is let go first, so the judgment it wakes reads no pass in flight and owes the next.
                _context.Runs.OpinionDone(claim);
                BesideEnded?.Invoke(subject);
            }
        });

    /// <summary>
    /// Who a first pass's walk may ask: the person's named reviewer, the rule's, or at the person's same-agent press the agent the work
    /// ran on. A record that names no agent it ran on goes on as a continuation does: the agent its choice asked for, else its role's first entry, never the machine's <c>adapter</c> key.
    /// </summary>
    /// <exception cref="DriverException">The record's role's stored entry does not read, or the role is off.</exception>
    internal static IReadOnlyList<string> Reviewers(OpinionRequest? request, PriorSession? record, OpinionRule rule, DriverConfig config) => request switch
    {
        { SameAgent: true } => [record?.Adapter ?? StartPlan.ContinuedOn(record, config)],
        { Reviewer: { } one } => [one],
        _ => rule.Reviewers,
    };

    /// <summary>
    /// Ask one first pass on an owed opinion's work: the reviewer chosen from the rule's, or the person's named one,
    /// or the working agent fresh at their press; the candidate from where the work leaves the line (or, for a step, where its
    /// session began) to its tree's tip; the chain's quests here and the ask's words. Kept at the gate the moment the host opens it,
    /// or kept as unavailable with its code; logged either way.
    /// </summary>
    /// <param name="process">The run's process: a named workflow's opinion step names which declared reviewers read it.</param>
    private async Task<string> AskPassAsync(
        OpinionDue due, string key, IReadOnlyList<QuestView> chain, string occasion, OpinionRequest? request,
        WorkflowProcess process, CancellationToken ct, string? early = null)
    {
        var gates = new OpinionGates(Home);
        var named = $"`{due.Repository}` for session {due.Session}";
        var at = DateTimeOffset.UtcNow;
        var by = request is null ? OpinionAskers.Look : OpinionAskers.Person;
        // A step that cannot start is never asked: its gate says why, and the person's own answers are the floor.
        if ((process.OpinionCannot ?? process.Problem ?? process.Unread) is { } cannot)
        {
            return $"opinion  no second opinion on {named}: its workflow cannot start one: {cannot}";
        }

        if (process.Opinion is not { Rule: { IsNone: false } rule })
        {
            return $"opinion  no second opinion on {named}: no rule here names a reviewer for `{due.Repository}`.";
        }

        // A chat run's pass reads the tip the person's press captured, never the tree's HEAD at the look,
        // which a turn finished meanwhile may have moved.
        var tip = due is { OnRun: true, Tip: { } pressed } ? pressed : await ReviewGit.HeadAsync(due.Tree, ct).ConfigureAwait(false);
        var root = await TreeGates.CheckoutOfTreeAsync(due.Tree, ct).ConfigureAwait(false);
        if (tip is null || root is null) return $"held  second opinion on {named}: git could not read its tree, and the next look tries again.";

        var start = occasion == OpinionRules.Steps
            ? (await Service.SessionGroundAsync(due.Session, ct).ConfigureAwait(false)).BaseCommit
            : await _context.Trees.ForkPointAsync(due.Tree, ct).ConfigureAwait(false);
        var records = OpinionWriters.ById(await Service.RecordsAsync(ct).ConfigureAwait(false));
        var record = records.GetValueOrDefault(due.Session);
        var part = chain.Where(quest => Same(quest.To, due.Repository)).ToList();

        // The families that wrote the work: every agent the line ran on, each part of each of the chain's quests here, so a
        // line carried on across makers at a limit counts both.
        var wrote = OpinionWriters.Agents(records, due.Session, part.Select(quest => quest.Id));
        var declared = Reviewers(request, record, rule, Config);
        // A line level's act on a vacancy (a re-cast, the difference relaxed, the prepaid spent) shapes the look's walk; a person's ask is theirs.
        var casts = new OpinionCasts(Home);
        var acted = casts.Read(key, due.Repository, due.Session);
        var reviewers = request is null ? OpinionCasting.Reviewers(declared, OpinionCasting.Newest(acted), [.. wrote]) : declared;
        // The rule decides who, the opinion actor each agent's model; a refusal at the open is asked again here.
        // The role the run's opinion step names, kept at its bind, gives the models in place of the opinion actor's.
        var stepRole = process.Binding?.RoleOf(NodeKinds.Opinion)?.Role;
        Task<ReviewerChoice> Walk(IReadOnlyList<ReviewerPassed>? passed) =>
            ReviewerChoice.ChooseAsync(_context.Harnesses, rule with { Reviewers = reviewers }, wrote, Config, due.Workspace, ct, passed: passed,
                catalogue: () => RoleCatalog.ReadHome(_context.Harnesses.Home), role: stepRole, repository: due.Repository);
        var choice = await Walk(null).ConfigureAwait(false);
        if (early is not null && !choice.Chosen) return $"held  second opinion on {named}: every holder is cooling, and the reset is asked again.";
        if (early is not null) Service.LandingSaid(OpinionLines.HandedOn(due.Session, occasion, early, ""));

        var reading = choice;

        var watch = Stopwatch.StartNew();
        var ask = new OpinionPassAsk(occasion, due.Session, due.Repository, root, start ?? tip, tip, rule)
        {
            Workspace = due.Workspace,
            Quests = part,
            Words = due.OnRun ? null : await AskWords.ReadOfSessionAsync(Service, due.Session, ct).ConfigureAwait(false),
            Opening = due.OnRun ? ChatRuns.OpeningOf(_context.Events, due.Run!) : null,
            Reask = Walk,
            Replaced = next => reading = next,
            Opened = (opinion, _) =>
            {
                gates.Asked(key, due.Repository, new OpinionAskKept(at, occasion, due.Session, tip)
                {
                    Opinion = opinion, By = by, Reviewer = reading.Reviewer, Label = reading.Label,
                });
                Service.LandingSaid(OpinionLines.Asked(opinion, due.Session, occasion, reading.Label, reading.Reviewer, OpinionViews.FirstPass));
            },
        };
        var run = await (_context.Passes ?? _opinions.PassAsync)(ask, choice, ct).ConfigureAwait(false);

        if (run.Opinion is null)
        {
            // No reviewer could read it, or its pass did not open: said, never downgraded silently.
            var code = run.Code ?? reading.Code ?? ReviewerUnavailable.Refused;
            gates.Asked(key, due.Repository, new OpinionAskKept(at, occasion, due.Session, tip) { Code = code, Until = reading.Cooling?.Until, By = by });
            // Nobody free to read it: a vacancy on the reporting line, journalled once until a level acts on it.
            if (OpinionVacancy.Of(reading) is { } vacancy && acted.LastOrDefault()?.Act != OpinionCasting.Vacant)
            {
                casts.Keep(key, due.Repository, new OpinionCast(at, due.Session, OpinionCasting.Step, reading.Role ?? OpinionCasting.Step, OpinionCasting.Vacant, vacancy.Back, vacancy.Code)
                {
                    Offered = OpinionCasting.Offered(declared, reading),
                });
            }

            Service.LandingSaid(OpinionLines.Unavailable(due.Session, occasion, code));
            return $"opinion  {run.Line}";
        }

        // A reviewer holds the step: a vacancy that stood is filled, and the act that answered it shapes no later walk.
        if (OpinionCasting.Current(acted).Count > 0)
        {
            casts.Keep(key, due.Repository, new OpinionCast(
                at, due.Session, OpinionCasting.Step, reading.Role ?? OpinionCasting.Step, OpinionCasting.Filled, reading.Reviewer, run.Opinion));
        }

        var view = await Service.Opinions.ReadAsync(run.Opinion, ct).ConfigureAwait(false);
        if (view is { Given: true }) Service.LandingSaid(OpinionLines.Given(view, (long)watch.Elapsed.TotalSeconds));
        else if (view is { State: OpinionViews.Failed }) Service.LandingSaid(OpinionLines.Unavailable(due.Session, occasion, view.Why ?? "ended"));
        return $"opinion  {run.Line}";
    }

    /// <summary>Deliver an owed opinion as far as the facts let it go (<see cref="OpinionDeliverer.DeliverAsync"/>), asked with what the
    /// first pass read; a recheck it asks says its own lines, and its consultation is kept, also here for one an older build left without.</summary>
    private async Task<string> DeliverBesideAsync(
        OpinionDue due, IReadOnlyList<QuestView> chain, OpinionView first, WorkflowProcess process, CancellationToken ct)
    {
        if (process.Opinion is not { Rule: { IsNone: false } rule })
        {
            return $"opinion  second opinion {first.Id} is not delivered: no rule here names a reviewer for `{due.Repository}` any more.";
        }

        var root = await TreeGates.CheckoutOfTreeAsync(due.Tree, ct).ConfigureAwait(false) ?? "";
        var ask = new OpinionPassAsk(first.Occasion, first.Working, due.Repository, root, first.Base, first.Tip, rule)
        {
            Workspace = due.Workspace,
            Quests = [.. chain.Where(quest => Same(quest.To, due.Repository))],
            Words = due.OnRun ? null : await AskWords.ReadOfSessionAsync(Service, due.Session, ct).ConfigureAwait(false),
            // A chat run's findings go back into its conversation, compared with the press's turn count.
            Opening = due.OnRun ? ChatRuns.OpeningOf(_context.Events, due.Run!) : null,
            Run = due.OnRun ? due.Run : null,
            Turns = due.OnRun ? due.Turns : null,
        };
        var delivered = await _deliverer.DeliverAsync(first.Id, ask, ct).ConfigureAwait(false);
        return $"opinion  {delivered.Line}";
    }

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
