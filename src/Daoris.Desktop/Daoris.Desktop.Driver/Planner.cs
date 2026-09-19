namespace Daoris.Driver;

/// <summary>A quest as the service answered it — enough to decide on, and enough to compose a target from.</summary>
public sealed record QuestView(string Id, string From, string To, string Title, string Body, string Status);

/// <summary>A registration as the service answered it. The root is present only from a local service.</summary>
public sealed record RepoView(string Repository, bool Adopted, string? Root);

/// <summary>An ACTIVE session as the service answered it — closed ones never reach the planner.</summary>
public sealed record SessionView(string Id, string Repository);

/// <summary>Everything a tick's decisions are made from, fetched once so the plan is coherent.</summary>
public sealed record Snapshot(
    IReadOnlyList<QuestView> Quests,
    IReadOnlyList<RepoView> Repositories,
    IReadOnlyList<SessionView> Active);

public enum StartVerdict
{
    /// <summary>Start a session for this quest, now.</summary>
    Start,

    /// <summary>The receiver has not opted into driving on this machine — the person's choice (D46 §2).</summary>
    NotDrivable,

    /// <summary>The person paused this repository.</summary>
    Held,

    /// <summary>The receiver has not adopted, so there is no agent to be.</summary>
    NotAdopted,

    /// <summary>No filesystem root is known — nowhere to spawn. `connect` from the repository fixes it.</summary>
    NoRoot,

    /// <summary>An active session (or an older quest this tick) holds the repository.</summary>
    RepositoryBusy,

    /// <summary>The concurrency cap is spent.</summary>
    AtCapacity,
}

/// <param name="Quest">The quest considered.</param>
/// <param name="Verdict"><see cref="StartVerdict.Start"/>, or why not.</param>
/// <param name="Reason">The sentence a person reads. "Sitting" must always say why (D46 §3).</param>
/// <param name="Root">Where a start would spawn — carried so the executor never re-derives it.</param>
public sealed record Consideration(QuestView Quest, StartVerdict Verdict, string Reason, string? Root = null);

/// <summary>
/// The decision half of a tick: which open quests start, and why every other one is sitting.
/// </summary>
/// <remarks>
/// <para><b>Pure, deliberately.</b> Plan and apply are separate functions — the CLI's own convention,
/// held here for the same reason: a decision that can be asserted without spawning anything stays
/// tested, and a printed plan is a driver that can explain itself.</para>
///
/// <para><b>The plan never writes quest state and never outranks the ledger.</b> The service re-judges
/// every open through <c>SessionLedger</c>; this planner exists to avoid asking for work the ledger
/// would refuse, and to give every refusal a sentence before it happens.</para>
/// </remarks>
public static class Planner
{
    public static IReadOnlyList<Consideration> Plan(Snapshot snapshot, DriverConfig config)
    {
        var considerations = new List<Consideration>();
        var busy = new HashSet<string>(snapshot.Active.Select(s => s.Repository), StringComparer.OrdinalIgnoreCase);
        var blockedBy = snapshot.Active.ToDictionary(s => s.Repository, s => s.Id, StringComparer.OrdinalIgnoreCase);
        var startedThisTick = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var slots = config.Cap - snapshot.Active.Count;

        // The service already orders open-oldest-first; keeping its order is what makes "oldest starts
        // first" one implementation rather than two that drift.
        foreach (var quest in snapshot.Quests.Where(q => q.Status == "Open"))
        {
            considerations.Add(Consider(quest));
        }

        return considerations;

        Consideration Consider(QuestView quest)
        {
            var repo = snapshot.Repositories.FirstOrDefault(r =>
                string.Equals(r.Repository, quest.To, StringComparison.OrdinalIgnoreCase));

            if (repo is null || !repo.Adopted)
            {
                return new(quest, StartVerdict.NotAdopted,
                    $"`{quest.To}` has not adopted, so there is no agent to be.");
            }

            if (!config.Drivable.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
            {
                return new(quest, StartVerdict.NotDrivable,
                    $"`{quest.To}` has not been opted into driving on this machine.");
            }

            if (config.Holds.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
            {
                return new(quest, StartVerdict.Held, $"`{quest.To}` is held by the person.");
            }

            if (repo.Root is null)
            {
                return new(quest, StartVerdict.NoRoot,
                    $"no root is known for `{quest.To}` — run `daoris connect` from that repository.");
            }

            if (blockedBy.TryGetValue(quest.To, out var session))
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{session}` is active in `{quest.To}` — one session per repository.");
            }

            if (startedThisTick.TryGetValue(quest.To, out var ahead))
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"queued behind quest `#{ahead}` in `{quest.To}` — oldest first, one at a time.");
            }

            if (slots <= 0)
            {
                return new(quest, StartVerdict.AtCapacity,
                    $"the concurrency cap ({config.Cap}) is spent — it frees as sessions finish.");
            }

            slots--;
            startedThisTick[quest.To] = quest.Id;
            return new(quest, StartVerdict.Start, $"starting in `{quest.To}`.", repo.Root);
        }
    }
}
