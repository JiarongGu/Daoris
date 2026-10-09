using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The second opinions owed by themselves (XAGENT1f, D155 point 2; the second-agent design §2.1): what a driven record's conclusion
/// makes owed, and why the look closes one.
/// </summary>
public static class OpinionLook
{
    /// <summary>
    /// A driven record that just concluded, read against the opinion rule standing for its repository (§2.1): a done in a tree of
    /// its own, under a rule whose occasions read it, is owed one at each occasion the rule names. Never throws: the record has
    /// concluded, and an opinion not owed leaves the person's <i>Ask now</i>.
    /// </summary>
    /// <param name="process">
    /// Its run's process (WORKFLOW1f): under a named workflow, its version's opinion step says the occasions, one that cannot start
    /// among them, so it sits where it is owed. Null reads the rule live, as before.
    /// </param>
    /// <returns>The occasions it is owed at; empty where none.</returns>
    public static IReadOnlyList<string> Concluded(
        string home, DriverConfig config, string session, QuestView quest, string? status, string state, string tree, DateTimeOffset? at = null,
        WorkflowProcess? process = null)
    {
        try
        {
            if (status != "Done" || SessionStates.IsParked(state)) return [];
            var trees = new SessionTrees(home);
            if (!trees.Holds(tree)) return [];
            var (workspace, repository) = trees.Owner(tree);
            var resolved = process is null ? OpinionRules.Resolve(config, repository, workspace) : process.Opinion;
            if (resolved is null || (resolved.Rule.IsNone && process?.OpinionCannot is null)) return [];
            var rule = resolved.Rule;

            var dues = new OpinionDues(home);
            foreach (var occasion in rule.On)
            {
                dues.Due(new OpinionDue(session, quest.Id, repository, workspace, tree, occasion, at ?? DateTimeOffset.UtcNow));
            }

            return rule.On;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>
    /// Why an opinion owed is closed now, or null where it is still owed: its quest no longer done, its rule no longer reading at
    /// its occasion, its tree gone, its work landed (a landing recorded for it, or nothing in its tree that no branch of the
    /// person's holds), or, under <c>steps</c>, no next step of its chain left to hold.
    /// </summary>
    /// <param name="process">
    /// Its run's process (WORKFLOW1f): under a named workflow, whether its version still reads at the occasion. One that cannot be
    /// read here closes nothing, since it may read once it can. Null reads the rule live, as before.
    /// </param>
    public static async Task<string?> ClosesAsync(
        OpinionDue due, QuestView? quest, IReadOnlyList<QuestView> chain, DriverConfig config, LandedBranches landed, CancellationToken ct,
        WorkflowProcess? process = null)
    {
        if (quest is not { Status: "Done" }) return OpinionDueClosed.Undone;
        var reads = process is null
            ? OpinionRules.Resolve(config, due.Repository, due.Workspace) is { Rule: { IsNone: false } rule } && rule.On.Contains(due.Occasion)
            : process.Problem is not null || process.Unread is not null || OpinionGate.Reads(process.Opinion, process.OpinionCannot, due.Occasion);
        if (!reads) return OpinionDueClosed.Off;

        if (SessionTrees.TreeGone(due.Tree)) return OpinionDueClosed.Gone;
        if (landed.Landing(due.Session) is { } landing && landing.Names(due.Session)) return OpinionDueClosed.Landed;
        if (due.Occasion == OpinionRules.Steps
            && !chain.Any(step => string.Equals(step.Parent, due.Quest, StringComparison.OrdinalIgnoreCase) && step.Status == "Open"))
        {
            return OpinionDueClosed.Past;
        }

        return await SessionTrees.UnlandedAsync(due.Tree, "HEAD", ct).ConfigureAwait(false) == 0 ? OpinionDueClosed.Landed : null;
    }
}

public sealed partial class Driver
{
    /// <summary>
    /// The second opinions owed on this machine, at a look (XAGENT1f; the second-agent design §2.1, §7, §8.1): each closed once
    /// past, each asked where nothing was yet and the chain's last step here is done and not held, asked again once a cool-off
    /// resets, the person's asks started, and each delivered (XAGENT1e) until answered; each pass and delivery beside the look, one
    /// per chain's work at a time. Returns the quests the opinions sit, and how many passes this look starts, which take slots
    /// before any quest's first start (D130 point 10). A service that does not answer is said, and everything is looked at again
    /// at the next look.
    /// </summary>
    internal async Task<(IReadOnlyDictionary<string, string> Sits, int Passes)> OpinionsAsync(
        Snapshot snapshot, List<string> events, CancellationToken ct)
    {
        var sits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dues = new OpinionDues(home);
        var open = dues.Open();
        if (open.Count == 0) return (sits, 0);

        var passes = 0;
        try
        {
            var world = new ServiceReviewWorld(service);
            var quests = await world.QuestsAsync(ct).ConfigureAwait(false);
            var gates = new OpinionGates(home);
            foreach (var due in open)
            {
                var now = DateTimeOffset.UtcNow;
                var chain = ReviewGate.ChainOf(quests, due.Quest);
                var quest = chain.FirstOrDefault(each => Same(each.Id, due.Quest));
                // WORKFLOW1f: the run's process, read once for this due and handed to each part below, so a named workflow's opinion
                // step decides whether it is owed, who reads it and what holds.
                var process = WorkflowProcesses.Read(home, config, due.Repository, due.Workspace,
                    WorkflowRunBindings.RunOf(chain, due.Repository) ?? due.Quest);
                if (await OpinionLook.ClosesAsync(due, quest, chain, config, _trees.Recorded, ct, process).ConfigureAwait(false) is { } closed)
                {
                    dues.Close(due.Session, due.Occasion, closed, now);
                    continue;
                }

                var key = SessionTrees.OpinionChain(chain, due.Quest);
                var claim = $"{key}/{due.Repository}/{due.Occasion}";
                var gate = await _trees.OpinionAsync(due.Tree, due.Repository, due.Workspace, due.Quest, due.Session, world, null, due.Occasion, ct, process)
                    .ConfigureAwait(false);

                // The person's asks first (§8.5): Ask now, Try again, Ask again, Ask the same agent, fresh. Not capped (§8.3).
                var asked = gates.Read(key, due.Repository).Requests.Where(request => Same(request.Session, due.Session)).ToList();
                if (asked.Count > 0)
                {
                    if (_runs.TryOpinion(claim))
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
                    if (_runs.TryOpinion(claim))
                    {
                        passes++;
                        OpinionBeside(claim, token => AskPassAsync(due, key, chain, due.Occasion, null, snapshot, process, token), ct);
                    }
                }
                else if (gate is { State: OpinionGateStates.WithSession or OpinionGateStates.ReadAgain, Opinion: { } first } && _runs.TryOpinion(claim))
                {
                    OpinionBeside(claim, token => DeliverBesideAsync(due, chain, first, process, token), ct);
                }

                Sit(sits, due, chain, gate);
            }
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                          or UnauthorizedAccessException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            events.Add($"opinion  the second opinions owed could not be looked at this look, and are looked at again at the next: {error.Message}");
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
        _ = _runs.StartAsync(async opened =>
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
                _runs.OpinionDone(claim);
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
        var gates = new OpinionGates(home);
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

        var tip = await ReviewGate.HeadAsync(due.Tree, ct).ConfigureAwait(false);
        var root = await SessionTrees.CheckoutOfTreeAsync(due.Tree, ct).ConfigureAwait(false);
        if (tip is null || root is null) return $"held  second opinion on {named}: git could not read its tree, and the next look tries again.";

        var start = occasion == OpinionRules.Steps
            ? (await service.SessionGroundAsync(due.Session, ct).ConfigureAwait(false)).BaseCommit
            : await _trees.ForkPointAsync(due.Tree, ct).ConfigureAwait(false);
        var record = await service.RecordAsync(due.Session, ct).ConfigureAwait(false);
        var part = chain.Where(quest => Same(quest.To, due.Repository)).ToList();

        // The families that wrote the work (§3.1): the agents of this machine's last run on each of the chain's quests here.
        IReadOnlyCollection<string> wrote = [.. part.Select(quest => snapshot.LastRun.GetValueOrDefault(quest.Id)?.Adapter)
            .Append(record?.Adapter).OfType<string>().Where(adapter => adapter.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
        IReadOnlyList<string> reviewers = request switch
        {
            { SameAgent: true } => [record?.Adapter ?? config.Adapter],
            { Reviewer: { } one } => [one],
            _ => rule.Reviewers,
        };
        var choice = await ReviewerChoice.ChooseAsync(_harnesses, rule with { Reviewers = reviewers }, wrote, config, due.Workspace, ct)
            .ConfigureAwait(false);

        var watch = Stopwatch.StartNew();
        var ask = new OpinionPassAsk(occasion, due.Session, due.Repository, root, start ?? tip, tip, rule)
        {
            Workspace = due.Workspace,
            Quests = part,
            Words = await AskWords.ReadAsync(service, chain.FirstOrDefault()?.From, ct).ConfigureAwait(false),
            Opened = (opinion, _) =>
            {
                gates.Asked(key, due.Repository, new OpinionAskKept(at, occasion, due.Session, tip)
                {
                    Opinion = opinion, By = by, Reviewer = choice.Reviewer, Label = choice.Label,
                });
                service.LandingSaid(OpinionLines.Asked(opinion, due.Session, occasion, choice.Label, choice.Reviewer, OpinionViews.FirstPass));
            },
        };
        var run = await (Passes ?? PassAsync)(ask, choice, ct).ConfigureAwait(false);

        if (run.Opinion is null)
        {
            // No reviewer could read it, or its pass did not open (§3.3, §8.4): said, never downgraded silently.
            var code = run.Code ?? choice.Code ?? ReviewerUnavailable.Refused;
            gates.Asked(key, due.Repository, new OpinionAskKept(at, occasion, due.Session, tip) { Code = code, Until = choice.Cooling?.Until, By = by });
            service.LandingSaid(OpinionLines.Unavailable(due.Session, occasion, code));
            return $"opinion  {run.Line}";
        }

        var view = await service.ReadOpinionAsync(run.Opinion, ct).ConfigureAwait(false);
        if (view is { Given: true }) service.LandingSaid(OpinionLines.Given(view, (long)watch.Elapsed.TotalSeconds));
        else if (view is { State: OpinionViews.Failed }) service.LandingSaid(OpinionLines.Unavailable(due.Session, occasion, view.Why ?? "ended"));
        return $"opinion  {run.Line}";
    }

    /// <summary>
    /// Deliver an owed opinion as far as the facts let it go (XAGENT1e's <see cref="DeliverAsync"/>): its findings handed to the
    /// working session at its turn's end, its answers read, its one recheck asked; asked with what the first pass read.
    /// </summary>
    private async Task<string> DeliverBesideAsync(
        OpinionDue due, IReadOnlyList<QuestView> chain, OpinionView first, WorkflowProcess process, CancellationToken ct)
    {
        if (process.Opinion is not { Rule: { IsNone: false } rule })
        {
            return $"opinion  second opinion {first.Id} is not delivered: no rule here names a reviewer for `{due.Repository}` any more.";
        }

        var root = await SessionTrees.CheckoutOfTreeAsync(due.Tree, ct).ConfigureAwait(false) ?? "";
        var ask = new OpinionPassAsk(first.Occasion, first.Working, due.Repository, root, first.Base, first.Tip, rule)
        {
            Workspace = due.Workspace,
            Quests = [.. chain.Where(quest => Same(quest.To, due.Repository))],
            Words = await AskWords.ReadAsync(service, chain.FirstOrDefault()?.From, ct).ConfigureAwait(false),
        };
        var delivered = await DeliverAsync(first.Id, ask, ct).ConfigureAwait(false);
        if (delivered.Recheck is { Opinion: { } recheck })
        {
            service.LandingSaid(OpinionLines.Asked(recheck, first.Working, first.Occasion, first.Label, first.Adapter, OpinionViews.RecheckPass));
            if (await service.ReadOpinionAsync(recheck, ct).ConfigureAwait(false) is { Given: true } given) service.LandingSaid(OpinionLines.Given(given, null));
        }

        return $"opinion  {delivered.Line}";
    }

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
