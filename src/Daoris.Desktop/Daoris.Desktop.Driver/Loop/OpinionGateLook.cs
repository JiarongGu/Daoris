using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The second opinions owed on this machine, looked at beside the look (XAGENT1f, PART10m, D166): what <see cref="OpinionLook"/>
/// made owed is closed, asked, delivered and sat here, over the loop's context. The gate delivers, so it holds the delivery, and
/// the delivery passes.
/// </summary>
internal sealed class OpinionGateLook(LoopContext context, DriverOpinions opinions, OpinionDeliverer deliverer)
{
    private readonly LoopContext _context = context;

    private readonly DriverOpinions _opinions = opinions;

    private readonly OpinionDeliverer _deliverer = deliverer;

    private ServiceClient Service => _context.Service;

    private string Home => _context.Home;

    private DriverConfig Config => _context.Config;

    /// <summary>
    /// The second opinions owed on this machine, at a look (XAGENT1f; the second-agent design §2.1, §7, §8.1): each closed once
    /// past, each asked where nothing was yet and the chain's last step here is done and not held, asked again once a cool-off
    /// resets, the person's asks started, and each delivered (XAGENT1e) until answered; each pass and delivery beside the look, one
    /// per chain's work at a time. Returns the quests the opinions sit, and how many passes this look starts, which take slots
    /// before any quest's first start (D130 point 10). A service that does not answer is said, and everything is looked at again
    /// at the next look.
    /// </summary>
    /// <param name="judged">
    /// Where each owed landing opinion's gate is kept, as the attention band's route reads it (XAGENT1g3): oldest first, one per
    /// session, the review's own answer read with it as every landing door reads it. A look that could not read leaves none.
    /// </param>
    internal async Task<(IReadOnlyDictionary<string, string> Sits, int Passes)> OpinionsAsync(
        Snapshot snapshot, List<string> events, CancellationToken ct, List<OpinionJudged>? judged = null)
    {
        var sits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dues = new OpinionDues(Home);
        var open = dues.Open();
        if (open.Count == 0) return (sits, 0);

        var passes = 0;
        try
        {
            var world = new ServiceReviewWorld(Service);
            var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
            var gates = new OpinionGates(Home);
            // FABRIC-CLOSE1h15: a branch made whose push is held back has not landed, so the opinion holding it stays owed and is asked.
            var pushHeld = OpinionLook.PushHeld(Home);
            foreach (var due in open)
            {
                var now = DateTimeOffset.UtcNow;
                var chain = ReviewGate.ChainOf(quests, due.Quest);
                var quest = chain.FirstOrDefault(each => Same(each.Id, due.Quest));
                // WORKFLOW1f: the run's process, read once for this due and handed to each part below, so a named workflow's opinion
                // step decides whether it is owed, who reads it and what holds.
                var process = WorkflowProcesses.Read(Home, Config, due.Repository, due.Workspace,
                    WorkflowRunBindings.RunOf(chain, due.Repository) ?? due.Quest);
                if (await OpinionLook.ClosesAsync(due, quest, chain, Config, _context.Trees.Recorded, ct, process, pushHeld).ConfigureAwait(false) is { } closed)
                {
                    dues.Close(due.Session, due.Occasion, closed, now);
                    continue;
                }

                var key = TreeGates.OpinionChain(chain, due.Quest);
                var claim = $"{key}/{due.Repository}/{due.Occasion}";
                // The landing's gate reads D154's review beside it, as every landing door does (XAGENT1g3): a review that answered what
                // the opinion showed settles it. It moves no ask below, which read states the review never changes.
                var review = judged is not null && due.Occasion == OpinionRules.Landing
                    ? await ReviewGate.ReadAsync(world, Config, Path.GetFullPath(due.Tree), due.Repository, due.Workspace, due.Quest, ct, process)
                        .ConfigureAwait(false)
                    : null;
                var gate = await _context.Trees.OpinionAsync(due.Tree, due.Repository, due.Workspace, due.Quest, due.Session, world, review, due.Occasion, ct, process)
                    .ConfigureAwait(false);
                if (review is not null)
                {
                    judged!.Add(new OpinionJudged(due.Session, due.Quest, due.Repository, due.Workspace, due.At,
                        (process.Landing ?? LandingRules.Choose(Config, due.Repository, due.Workspace)).Rule.AutoAccept, gate));
                }

                // The person's asks first (§8.5): Ask now, Try again, Ask again, Ask the same agent, fresh. Not capped (§8.3).
                var asked = gates.Read(key, due.Repository).Requests.Where(request => Same(request.Session, due.Session)).ToList();
                if (asked.Count > 0)
                {
                    if (_context.Runs.TryOpinion(claim))
                    {
                        var request = gates.Take(key, due.Repository, due.Session)[^1];
                        passes++;
                        OpinionBeside(claim, token => AskPassAsync(due, key, chain, request.Occasion, request, snapshot, process, token), ct);
                    }
                }
                else if (((gate.State == OpinionGateStates.NotAsked && !gate.Held)
                          || (gate is { State: OpinionGateStates.Unavailable, Code: ReviewerUnavailable.Cooling, Until: { } reset } && reset <= now))
                         // One opinion reads a chain's whole work here (§8.1): an earlier step's is its last step's to ask.
                         && (due.Occasion == OpinionRules.Steps || !OpinionGate.HasLaterWork(chain, due.Quest, due.Repository)))
                {
                    if (_context.Runs.TryOpinion(claim))
                    {
                        passes++;
                        OpinionBeside(claim, token => AskPassAsync(due, key, chain, due.Occasion, null, snapshot, process, token), ct);
                    }
                }
                else if (gate is { State: OpinionGateStates.WithSession or OpinionGateStates.ReadAgain, Opinion: { } first } && _context.Runs.TryOpinion(claim))
                {
                    OpinionBeside(claim, token => DeliverBesideAsync(due, chain, first, process, token), ct);
                }

                Sit(sits, due, chain, gate);
            }

            if (judged is not null)
            {
                var ordered = judged.OrderBy(each => each.Since).DistinctBy(each => each.Session, StringComparer.OrdinalIgnoreCase).ToList();
                judged.Clear();
                judged.AddRange(ordered);
            }
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                          or UnauthorizedAccessException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            events.Add($"opinion  the second opinions owed could not be looked at this look, and are looked at again at the next: {error.Message}");
            judged?.Clear();
        }

        return (sits, passes);
    }

    /// <summary>
    /// The quests an opinion owed sits (§7, §8.1): under <c>landing</c>, the chain's set-up step here while an agent is still at work
    /// on the opinion of the work it shows (a dispute does not sit it: the person is about to look); under <c>steps</c>, the chain's
    /// next step while the opinion on the step before is unsettled.
    /// </summary>
    private static void Sit(Dictionary<string, string> sits, OpinionDue due, IReadOnlyList<QuestView> chain, OpinionGateState gate)
    {
        if (due.Occasion == OpinionRules.Landing)
        {
            if (!OpinionGateStates.AtWork(gate.State)) return;
            foreach (var step in chain.Where(each => each.SetUpIn is not null && each.Status == "Open" && Same(each.To, due.Repository)))
            {
                sits[step.Id] = $"set-up step `#{step.Id}` waits for a second opinion on the work it shows, so you look once at work that "
                    + $"already answered another agent: {gate.Says}";
            }

            return;
        }

        if (gate.LetsGo) return;
        foreach (var next in chain.Where(each => Same(each.Parent, due.Quest) && each.Status == "Open"))
        {
            sits[next.Id] = $"waits for a second opinion on the step before it, `#{due.Quest}`, before it starts: {gate.Says}";
        }
    }

    /// <summary>
    /// A pass or a delivery beside the look (XAGENT1f), as a start runs: told opened at once, so the look never waits on it, its
    /// line joining a later look's report, and the chain's claim let go however it ended.
    /// </summary>
    private void OpinionBeside(string claim, Func<CancellationToken, Task<string>> work, CancellationToken ct) =>
        _ = _context.Runs.StartAsync(async opened =>
        {
            opened();
            try
            {
                return new StartRun(await work(ct).ConfigureAwait(false), Opened: true);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                              or UnauthorizedAccessException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return new StartRun($"opinion  the second opinion on {claim} stopped, and is looked at again at the next look: {error.Message}", Opened: true);
            }
            finally
            {
                _context.Runs.OpinionDone(claim);
            }
        });

    /// <summary>
    /// Ask one first pass on an owed opinion's work (§2.1, §3, §4): the reviewer chosen from the rule's, or the person's named one,
    /// or the working agent fresh at their press; the candidate from where the work leaves the line (or, for a step, where its
    /// session began) to its tree's tip; the chain's quests here and the ask's words. Kept at the gate the moment the host opens it,
    /// or kept as unavailable with its code; logged either way.
    /// </summary>
    /// <param name="process">The run's process (WORKFLOW1f): a named workflow's opinion step names which declared reviewers read it.</param>
    private async Task<string> AskPassAsync(
        OpinionDue due, string key, IReadOnlyList<QuestView> chain, string occasion, OpinionRequest? request, Snapshot snapshot,
        WorkflowProcess process, CancellationToken ct)
    {
        var gates = new OpinionGates(Home);
        var named = $"`{due.Repository}` for session {due.Session}";
        var at = DateTimeOffset.UtcNow;
        var by = request is null ? OpinionAskers.Look : OpinionAskers.Person;
        // A step that cannot start is never asked: its gate says why, and the person's own answers are the floor (WORKFLOW1f).
        if ((process.OpinionCannot ?? process.Problem ?? process.Unread) is { } cannot)
        {
            return $"opinion  no second opinion on {named}: its workflow cannot start one: {cannot}";
        }

        if (process.Opinion is not { Rule: { IsNone: false } rule })
        {
            return $"opinion  no second opinion on {named}: no rule here names a reviewer for `{due.Repository}`.";
        }

        var tip = await ReviewGit.HeadAsync(due.Tree, ct).ConfigureAwait(false);
        var root = await TreeGates.CheckoutOfTreeAsync(due.Tree, ct).ConfigureAwait(false);
        if (tip is null || root is null) return $"held  second opinion on {named}: git could not read its tree, and the next look tries again.";

        var start = occasion == OpinionRules.Steps
            ? (await Service.SessionGroundAsync(due.Session, ct).ConfigureAwait(false)).BaseCommit
            : await _context.Trees.ForkPointAsync(due.Tree, ct).ConfigureAwait(false);
        var record = await Service.RecordAsync(due.Session, ct).ConfigureAwait(false);
        var part = chain.Where(quest => Same(quest.To, due.Repository)).ToList();

        // The families that wrote the work (§3.1): the agents of this machine's last run on each of the chain's quests here.
        IReadOnlyCollection<string> wrote = [.. part.Select(quest => snapshot.LastRun.GetValueOrDefault(quest.Id)?.Adapter)
            .Append(record?.Adapter).OfType<string>().Where(adapter => adapter.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
        IReadOnlyList<string> reviewers = request switch
        {
            { SameAgent: true } => [record?.Adapter ?? Config.Adapter],
            { Reviewer: { } one } => [one],
            _ => rule.Reviewers,
        };
        var choice = await ReviewerChoice.ChooseAsync(_context.Harnesses, rule with { Reviewers = reviewers }, wrote, Config, due.Workspace, ct)
            .ConfigureAwait(false);

        var watch = Stopwatch.StartNew();
        var ask = new OpinionPassAsk(occasion, due.Session, due.Repository, root, start ?? tip, tip, rule)
        {
            Workspace = due.Workspace,
            Quests = part,
            Words = await AskWords.ReadAsync(Service, chain.FirstOrDefault()?.From, ct).ConfigureAwait(false),
            Opened = (opinion, _) =>
            {
                gates.Asked(key, due.Repository, new OpinionAskKept(at, occasion, due.Session, tip)
                {
                    Opinion = opinion, By = by, Reviewer = choice.Reviewer, Label = choice.Label,
                });
                Service.LandingSaid(OpinionLines.Asked(opinion, due.Session, occasion, choice.Label, choice.Reviewer, OpinionViews.FirstPass));
            },
        };
        var run = await (_context.Passes ?? _opinions.PassAsync)(ask, choice, ct).ConfigureAwait(false);

        if (run.Opinion is null)
        {
            // No reviewer could read it, or its pass did not open (§3.3, §8.4): said, never downgraded silently.
            var code = run.Code ?? choice.Code ?? ReviewerUnavailable.Refused;
            gates.Asked(key, due.Repository, new OpinionAskKept(at, occasion, due.Session, tip) { Code = code, Until = choice.Cooling?.Until, By = by });
            Service.LandingSaid(OpinionLines.Unavailable(due.Session, occasion, code));
            return $"opinion  {run.Line}";
        }

        var view = await Service.Opinions.ReadAsync(run.Opinion, ct).ConfigureAwait(false);
        if (view is { Given: true }) Service.LandingSaid(OpinionLines.Given(view, (long)watch.Elapsed.TotalSeconds));
        else if (view is { State: OpinionViews.Failed }) Service.LandingSaid(OpinionLines.Unavailable(due.Session, occasion, view.Why ?? "ended"));
        return $"opinion  {run.Line}";
    }

    /// <summary>
    /// Deliver an owed opinion as far as the facts let it go (XAGENT1e's <see cref="OpinionDeliverer.DeliverAsync"/>): its findings handed to the
    /// working session at its turn's end, its answers read, its one recheck asked; asked with what the first pass read.
    /// </summary>
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
            Words = await AskWords.ReadAsync(Service, chain.FirstOrDefault()?.From, ct).ConfigureAwait(false),
        };
        var delivered = await _deliverer.DeliverAsync(first.Id, ask, ct).ConfigureAwait(false);
        if (delivered.Recheck is { Opinion: { } recheck })
        {
            Service.LandingSaid(OpinionLines.Asked(recheck, first.Working, first.Occasion, first.Label, first.Adapter, OpinionViews.RecheckPass));
            if (await Service.Opinions.ReadAsync(recheck, ct).ConfigureAwait(false) is { Given: true } given) Service.LandingSaid(OpinionLines.Given(given, null));
        }

        return $"opinion  {delivered.Line}";
    }

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
