namespace Daoris.Knowledge;

/// <summary>
/// The person's two acts on an ask's delivery plan: an answer to one of its open questions, and a waiver of one release order with a reason. Each
/// is kept on the plan's next revision, written by the person's hand, so the question is settled wherever it is asked and the lead's next
/// revision carries it on. An agent has no door here: <c>team_plan</c> never writes an answer or a waiver.
/// </summary>
/// <param name="store">The plan revisions.</param>
/// <param name="ledger">The decisions taken, where each act is noted.</param>
/// <param name="asks">Where the ask is found.</param>
public sealed class DeliverySettling(TeamStore store, TeamLedger ledger, AskDesk asks)
{
    private static readonly IReadOnlyDictionary<string, string> NothingSeen = new Dictionary<string, string>();

    /// <summary>Records the person's <paramref name="words"/> as the answer to the question under <paramref name="key"/>.</summary>
    public async Task<TeamOutcome> AnswerAsync(string ask, string key, string words, DateTimeOffset now, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(words)) return new(TeamRefusal.Unfit, "An answer is the person's words; nothing was kept.");
        var (plan, refusal) = await PlanAsync(ask, ct).ConfigureAwait(false);
        if (plan is null) return refusal!;

        var answer = key.Trim();
        if (plan.Answers.Any(each => each.Key == answer)) return new(TeamRefusal.Unfit, $"Question `{answer}` is answered already.");
        if (!IsOpen(plan, answer)) return new(TeamRefusal.Unfit, $"No open question `{answer}` on ask `#{plan.Ask}`'s plan.");

        return await KeepAsync(plan with { Answers = [.. plan.Answers, new(answer, words.Trim(), now)] }, $"answered `{answer}`: {words.Trim()}", now, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Waives the release order of <paramref name="deployable"/> after <paramref name="on"/>, with the person's <paramref name="reason"/>.</summary>
    public async Task<TeamOutcome> WaiveAsync(
        string ask, DeliveryRef deployable, DeliveryRef on, string reason, DateTimeOffset now, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return new(TeamRefusal.Unfit, "A waiver needs the person's reason; nothing was waived.");
        var (plan, refusal) = await PlanAsync(ask, ct).ConfigureAwait(false);
        if (plan is null) return refusal!;

        if (!plan.Releases.Any(edge => edge.Deployable == deployable && edge.On == on))
        {
            return new(TeamRefusal.Unfit, $"Ask `#{plan.Ask}`'s plan holds no release order `{deployable}` after `{on}`.");
        }

        var waivers = plan.Waivers.Where(each => each.Deployable != deployable || each.On != on).Append(new DeliveryWaiver(deployable, on, reason.Trim(), now)).ToList();
        var judged = DeliveryGraph.Judge(plan.Deployables, [.. plan.Releases.Where(edge => edge.Source != DeliveryEdge.Declaration)],
            [.. plan.Questions.Where(question => question.Kind != DeliveryQuestion.Cycle)], waivers);
        if (judged.Refusal is { } unordered) return new(TeamRefusal.Unfit, unordered);

        var next = plan with
        {
            Waivers = waivers, Releases = judged.Edges, Questions = judged.Questions,
            Flags = [.. plan.Flags.Where(flag => !DeliveryGraph.Raised(flag)), .. judged.Flags],
        };
        return await KeepAsync(next, $"waived `{deployable}` after `{on}`: {reason.Trim()}", now, ct).ConfigureAwait(false);
    }

    /// <summary>The questions each of <paramref name="asked"/>'s own plan still asks the person, for the asks that have any, by ask.</summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<DeliveryQuestion>>> OpenQuestionsAsync(IEnumerable<string> asked, CancellationToken ct = default)
    {
        var open = new Dictionary<string, IReadOnlyList<DeliveryQuestion>>(StringComparer.Ordinal);
        foreach (var id in asked.Distinct(StringComparer.Ordinal))
        {
            var own = (await store.LatestAsync(id, ct).ConfigureAwait(false)).FirstOrDefault(plan => plan.Scope is null);
            if (own is not null && DeliveryGraph.Open(own, NothingSeen) is { Count: > 0 } questions) open[id] = questions;
        }

        return open;
    }

    // A stale question is computed on a read with the reader's blobs, so it is not on the plan: its key names the order it is about.
    private static bool IsOpen(TeamPlan plan, string key) =>
        DeliveryGraph.Open(plan, NothingSeen).Any(question => question.Key == key)
        || plan.Releases.Any(edge => key.StartsWith($"stale:{edge.Deployable}>{edge.On}:{edge.Source}@", StringComparison.Ordinal));

    private async Task<(TeamPlan? Plan, TeamOutcome? Refusal)> PlanAsync(string ask, CancellationToken ct)
    {
        var id = ask.TrimStart('#');
        var found = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (found is null) return (null, new(TeamRefusal.NotFound, $"No ask `#{id}`."));
        if (found.State == AskState.Closed) return (null, new(TeamRefusal.Closed, $"Ask `#{id}` is closed ({found.Note}): it is settled no more."));

        var own = (await store.LatestAsync(id, ct).ConfigureAwait(false)).FirstOrDefault(plan => plan.Scope is null);
        return own is null ? (null, new(TeamRefusal.NotFound, $"Ask `#{id}` has no plan to settle.")) : (own, null);
    }

    private async Task<TeamOutcome> KeepAsync(TeamPlan changed, string reason, DateTimeOffset now, CancellationToken ct)
    {
        var plan = changed with { Revision = changed.Revision + 1, By = null, Why = $"The person {reason}", At = now };
        if (!await store.AppendAsync(plan, ct).ConfigureAwait(false))
        {
            return new(TeamRefusal.Moved, $"Another write took revision {plan.Revision} first: read the plan again.");
        }

        await ledger.KeepAsync(new TeamDecision(plan.Ask, null, plan.Revision, TeamDecision.Plan, "revised", plan.Why, "person", "settled", now), ct).ConfigureAwait(false);
        return new(TeamRefusal.None, $"Kept on ask `#{plan.Ask}`'s plan, revision {plan.Revision}: {plan.Why}", plan);
    }
}
