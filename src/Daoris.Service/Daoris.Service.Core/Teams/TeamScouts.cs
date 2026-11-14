namespace Daoris.Knowledge;

/// <summary>
/// The scout's half in the service: the lead's ask for a part to be oriented is judged against the plan (the part must be in a plan of the lead's
/// ask), and the pack a scout answers is kept with the plan and its session recorded as a team member, so the lead reads it through the plan door.
/// </summary>
/// <param name="store">The plan revisions, which say what parts exist and where.</param>
/// <param name="ledger">Where the scout's pack and its membership are kept.</param>
public sealed class TeamScouts(TeamStore store, TeamLedger ledger)
{
    /// <summary>
    /// The part <paramref name="part"/> of the ask the <paramref name="asker"/> leads, or a sentence saying why it is none: only a lead asks a scout, the
    /// intake of an ask or a worker for the part it splits, and the part must be one a plan of that ask wrote.
    /// </summary>
    public async Task<(HelperScout? Scout, string? Refusal)> JudgeAsync(Session? asker, string? part, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(part)) return (null, "Name the part to orient (`part`), as the plan writes its id.");
        var ask = asker?.Ask;
        if (ask is null && asker?.Quest is { } quest && await store.OfQuestAsync(quest, ct).ConfigureAwait(false) is { } serving) ask = serving.Ask;
        if (ask is null) return (null, "Only a lead asks a scout: the session that answers an ask, or a worker splitting its own part. This session is neither.");

        var wanted = part.Trim();
        var plans = await store.LatestAsync(ask, ct).ConfigureAwait(false);
        var holder = plans.FirstOrDefault(plan => plan.Parts.Any(each => each.Id == wanted));
        return holder is null
            ? (null, $"Ask `#{ask}` has no part `{wanted}` in its plan: write the plan with `team_plan` first, then ask for the part to be oriented.")
            : (new HelperScout(ask, wanted, holder.Parts.First(each => each.Id == wanted).Repository), null);
    }

    /// <summary>Keeps the pack the scout answered for its part, and records the scout as a member of the team at the revision that holds the part.</summary>
    public async Task KeepAsync(HelperScout scout, string session, string pack, DateTimeOffset now, CancellationToken ct = default)
    {
        var plans = await store.LatestAsync(scout.Ask, ct).ConfigureAwait(false);
        var revision = plans.FirstOrDefault(plan => plan.Parts.Any(each => each.Id == scout.Part))?.Revision ?? 1;
        await ledger.KeepAsync(new TeamScouted(scout.Ask, scout.Part, session, pack, now), ct).ConfigureAwait(false);
        await ledger.KeepAsync(new TeamMember(scout.Ask, scout.Part, TeamMember.Scout, session, revision, now), ct).ConfigureAwait(false);
    }

    /// <summary>What the ask's scouts answered, oldest first.</summary>
    public Task<IReadOnlyList<TeamScouted>> ScoutedAsync(string ask, CancellationToken ct = default) => ledger.ScoutedAsync(ask.TrimStart('#'), ct);
}
