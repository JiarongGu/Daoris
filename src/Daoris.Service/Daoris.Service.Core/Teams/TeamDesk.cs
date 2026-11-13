namespace Daoris.Knowledge;

/// <summary>
/// The one door that writes an ask's plan: the lead, Ask Daoris, a person by hand and a worker splitting its own part all come through it. It
/// judges the plan (<see cref="TeamPlanJudge"/>), appends the revision, and publishes each part as a quest under <c>(ask, part)</c>, so a retried
/// write returns the quests it made and two parts to one repository and lane are two quests.
/// </summary>
/// <param name="store">The revisions and the part-to-quest memory.</param>
/// <param name="ledger">Who is on the team and the choices taken.</param>
/// <param name="asks">What publishes a part as the ask's quest.</param>
/// <param name="policy">The paths no worker changes, by repository.</param>
public sealed class TeamDesk(TeamStore store, TeamLedger ledger, AskDesk asks, Func<string, CancellationToken, Task<TeamPathPolicy>> policy)
{
    /// <summary>Judge, keep and publish a plan revision; see the class.</summary>
    public async Task<TeamOutcome> SaveAsync(TeamSave save, DateTimeOffset now, CancellationToken ct = default)
    {
        var ask = await asks.FindAsync(save.Ask.TrimStart('#'), ct).ConfigureAwait(false);
        if (ask is null) return new(TeamRefusal.NotFound, $"No ask `#{save.Ask.TrimStart('#')}`.");
        if (ask.State == AskState.Closed) return new(TeamRefusal.Closed, $"Ask `#{ask.Id}` is closed ({ask.Note}): it is planned no more.");

        var latest = await store.LatestAsync(ask.Id, ct).ConfigureAwait(false);
        var current = latest.FirstOrDefault(plan => plan.Scope == save.Scope);
        if (save.Expect != (current?.Revision ?? 0))
        {
            return new(TeamRefusal.Moved, $"The plan stands at revision {current?.Revision ?? 0}, and you read {save.Expect}: read it again and write the next from what it says now.");
        }

        var depth = 1;
        if (save.Scope is { } scope)
        {
            var holder = latest.FirstOrDefault(plan => plan.Parts.Any(part => part.Id == scope));
            if (holder is null) return new(TeamRefusal.Unfit, $"Part `{scope}` is in no plan of ask `#{ask.Id}`, so it has none to split.");
            depth = holder.Depth + 1;
        }

        var others = latest.Where(plan => plan.Scope != save.Scope).ToList();
        var policies = new Dictionary<string, TeamPathPolicy>(StringComparer.OrdinalIgnoreCase);
        foreach (var repository in save.Parts.Select(part => part.Repository).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            policies[repository] = await policy(repository, ct).ConfigureAwait(false);
        }

        var parts = save.Parts.Select(part => part with { Parent = save.Scope }).ToList();
        var judged = TeamPlanJudge.Judge(
            parts, save.Edges, [.. others.SelectMany(plan => plan.Parts)], [.. others.SelectMany(plan => plan.Edges)],
            repository => policies.GetValueOrDefault(repository, TeamPathPolicy.None));
        if (judged.Refusal is { } unfit) return new(TeamRefusal.Unfit, unfit);

        var plan = new TeamPlan(ask.Id, save.Scope, (current?.Revision ?? 0) + 1, save.By, save.Why, now)
        {
            Base = save.Base, Parts = judged.Parts, Edges = judged.Edges, Flags = judged.Flags, Depth = depth,
        };
        if (!await store.AppendAsync(plan, ct).ConfigureAwait(false))
        {
            return new(TeamRefusal.Moved, $"Another write took revision {plan.Revision} first: read the plan again.");
        }

        var published = new List<TeamPublished>();
        foreach (var part in plan.Parts) published.Add(await PublishAsync(ask, part, save.By, now, ct).ConfigureAwait(false));
        await KeepTeamAsync(plan, published, ct).ConfigureAwait(false);

        return new(TeamRefusal.None, Said(plan, published), plan) { Published = published };
    }

    /// <summary>The newest revision of each plan the ask holds, with each part's quest.</summary>
    public async Task<(IReadOnlyList<TeamPlan> Plans, IReadOnlyList<TeamPartRow> Parts)> ReadAsync(string ask, CancellationToken ct = default) =>
        (await store.LatestAsync(ask.TrimStart('#'), ct).ConfigureAwait(false), await store.PartsAsync(ask.TrimStart('#'), ct).ConfigureAwait(false));

    /// <summary>Who is on the ask's team and the choices taken about it, each oldest first.</summary>
    public async Task<(IReadOnlyList<TeamMember> Members, IReadOnlyList<TeamDecision> Decisions)> TeamAsync(string ask, CancellationToken ct = default) =>
        (await ledger.MembersAsync(ask.TrimStart('#'), ct).ConfigureAwait(false), await ledger.DecisionsAsync(ask.TrimStart('#'), ct).ConfigureAwait(false));

    /// <summary>The session that wrote a revision is the lead of what it planned, and the revision is its decision, with what it published.</summary>
    private async Task KeepTeamAsync(TeamPlan plan, IReadOnlyList<TeamPublished> published, CancellationToken ct)
    {
        if (plan.By is { } session) await ledger.KeepAsync(new TeamMember(plan.Ask, plan.Scope, TeamMember.Lead, session, plan.Revision, plan.At), ct).ConfigureAwait(false);
        await ledger.KeepAsync(
            new TeamDecision(
                plan.Ask, plan.Scope, plan.Revision, TeamDecision.Plan, plan.Revision == 1 ? "planned" : "revised", plan.Why, plan.By ?? "person",
                string.Join(", ", published.Select(each => each.Quest is { } quest ? $"{each.Part} #{quest}" : $"{each.Part} unpublished")), plan.At),
            ct).ConfigureAwait(false);
    }

    /// <summary>The ask a session plans for and the part it splits: a worker serving a part splits that part, and a session answering an ask plans the ask.</summary>
    public async Task<(string Ask, string? Scope)?> ScopeOfAsync(Session? caller, CancellationToken ct = default)
    {
        if (caller?.Quest is { } quest && await store.OfQuestAsync(quest, ct).ConfigureAwait(false) is { } part) return (part.Ask, part.Part);
        return caller?.Ask is { } ask ? (ask, null) : null;
    }

    // The writing session publishes, so the ask's own intake, which is the lead, moves the ask's tier as its own quest_publish does.
    private async Task<TeamPublished> PublishAsync(Ask ask, TeamPart part, string? session, DateTimeOffset now, CancellationToken ct)
    {
        // The title is kept with the part before its quest is made, so a retry after a crash, or a revision that rewords the part, asks the same.
        var row = await store.ReserveAsync(ask.Id, part.Id, part.Title, ct).ConfigureAwait(false);
        if (row.Quest is { } made) return new(part.Id, made, null);

        var draft = new AskDraft(row.Title, part.Body)
        {
            Part = $"{ask.Id}/{part.Id}/1",
            Requirements = part.Requirements,
            Weight = part.Weight,
            Paths = part.Claims,
        };
        var outcome = await asks.PublishAsync(ask.Id, QuestAddress.Spell(part.Repository, part.Lanes), now, ct, draft, session, byAgent: true)
            .ConfigureAwait(false);
        if (outcome.Quest is not { } quest) return new(part.Id, null, outcome.Message);

        await store.RecordQuestAsync(ask.Id, part.Id, quest.Id, ct).ConfigureAwait(false);
        return new(part.Id, quest.Id, null);
    }

    private static string Said(TeamPlan plan, IReadOnlyList<TeamPublished> published)
    {
        var lines = new List<string>
        {
            plan.Scope is null
                ? $"Plan for ask `#{plan.Ask}`, revision {plan.Revision}: {plan.Parts.Count} part(s), {plan.Edges.Count} edge(s)."
                : $"Split of part `{plan.Scope}` of ask `#{plan.Ask}`, revision {plan.Revision} (depth {plan.Depth}): {plan.Parts.Count} part(s).",
        };
        lines.AddRange(published.Select(each => each.Quest is { } quest
            ? $"- `{each.Part}` is quest `#{quest}`."
            : $"- `{each.Part}` has no quest yet: {each.Note}"));
        lines.AddRange(plan.Flags.Select(flag => $"- Flag `{flag.Kind}`: {flag.Text}"));
        return string.Join('\n', lines);
    }
}
