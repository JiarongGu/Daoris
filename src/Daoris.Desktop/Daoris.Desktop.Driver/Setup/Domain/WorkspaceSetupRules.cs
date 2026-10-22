namespace Daoris.Driver;

/// <summary>
/// A workspace plan's rules (WSSETUP6, D124 §4.1–§4.4), pure: its defaults, the most set-ups open at once at a cap, where each
/// repository stands from the facts, whether the pilot holds, and the head line in a person's words.
/// <see cref="WorkspaceSetup"/> works a plan against the world; its callers name the rules here.
/// </summary>
public static class WorkspaceSetupRules
{
    public const int DefaultAtOnce = 1;

    public const int DefaultPilot = 2;

    /// <summary>The states in the order the head line says them (§4.4).</summary>
    private static readonly SetupState[] Spoken =
    [
        SetupState.SetUp, SetupState.Waiting, SetupState.Open, SetupState.Parked, SetupState.ToGo, SetupState.Skipped,
        SetupState.Declined, SetupState.Deleted,
    ];

    /// <summary>The most set-ups open at once at a cap: never the last slot, so other work keeps one; one at a cap of one.</summary>
    public static int MostAtOnce(int cap) => Math.Max(1, cap - 1);

    /// <summary>
    /// Where each repository of <paramref name="plan"/> stands, in its order, read from the facts (D124 §4.1): set up from the
    /// registry; open, parked, waiting for review, declined or deleted from its set-up quests and their strikes; skipped from
    /// the plan's own record; else to go. A set-up open or taken, the plan's or the single press's, is read first, so no
    /// repository is ever asked twice. A repository set up stands where its index quest does (ORIENT2c): to go until the
    /// plan has asked it, or the press found it naming an index already.
    /// </summary>
    public static IReadOnlyList<SetupStanding> Stand(
        WorkspaceSetupPlan plan, IReadOnlyList<RegistrationRow> rows, IReadOnlyList<QuestView> quests,
        IReadOnlyDictionary<string, int> strikes, DriverConfig config)
    {
        var standings = new List<SetupStanding>();
        foreach (var repository in plan.Order)
        {
            standings.Add(One(repository));
        }

        return standings;

        SetupStanding One(string repository)
        {
            if (rows.FirstOrDefault(row => Same(row.Repository, repository)) is { Registered: true } row)
            {
                return Indexing(repository, $"set up: {row.Summary?.Trim() ?? "adopted, and declaring what it owns"}");
            }

            var setups = quests.Where(quest => Same(quest.To, repository) && SetupQuests.IsSetup(quest.Title) && !SetupQuests.IsIndex(quest.Title)).ToList();
            if (setups.LastOrDefault(quest => quest.Status is "Open" or "Taken") is { } active)
            {
                return Active(repository, active, "set-up");
            }

            var mine = plan.Published.TryGetValue(repository, out var published)
                ? setups.FirstOrDefault(quest => Same(quest.Id, published))
                : null;
            switch ((mine ?? setups.LastOrDefault())?.Status)
            {
                case "Done":
                    var done = mine ?? setups.Last();
                    return new(repository, SetupState.Waiting, done.Id,
                        $"its set-up #{done.Id} is done and waits for your review; once it is merged, *Bring up to date* registers it.");
                case "Declined":
                    var declined = mine ?? setups.Last();
                    return new(repository, SetupState.Declined, declined.Id,
                        $"its set-up #{declined.Id} was declined{(declined.Note is { Length: > 0 } note ? $": {note.Trim()}" : ".")} "
                        + $"The plan does not ask it again; `daoris-driver setup {repository}` asks it alone.");
            }

            if (published is not null)
            {
                return new(repository, SetupState.Deleted, published,
                    $"its set-up #{published} was deleted. The plan does not ask it again; `daoris-driver setup {repository}` asks it alone.");
            }

            return plan.Skipped.TryGetValue(repository, out var skipped)
                ? new(repository, SetupState.Skipped, null, skipped.Said)
                : new(repository, SetupState.ToGo, null, "to go.");
        }

        // An open or taken quest, parked as the planner parks one (DRV6): its failed sessions, less where the person last
        // restarted it.
        SetupStanding Active(string repository, QuestView active, string what)
        {
            var failed = (strikes.TryGetValue(active.Id, out var count) ? count : 0) - config.ForgivenAt(active.Id);
            return config.Strikes > 0 && failed >= config.Strikes
                ? new(repository, SetupState.Parked, active.Id,
                    $"its {what} #{active.Id} is parked: {failed} session(s) failed on it. `daoris driver retry {active.Id}` "
                    + "starts it again once you know why.")
                : new(repository, SetupState.Open, active.Id, $"its {what} #{active.Id} is {active.Status.ToLowerInvariant()}.");
        }

        // ORIENT2c: a repository set up is asked its index next (D151 point 5), paced with the set-ups. Its standing is its
        // index quest's, read as a set-up's is; one the press found already naming its index, or with one closed done, is
        // set up.
        SetupStanding Indexing(string repository, string setUp)
        {
            var indexes = quests.Where(quest => Same(quest.To, repository) && SetupQuests.IsIndex(quest.Title)).ToList();
            if (indexes.LastOrDefault(quest => quest.Status is "Open" or "Taken") is { } active) return Active(repository, active, "index");

            var mine = plan.Indexed.TryGetValue(repository, out var published)
                ? indexes.FirstOrDefault(quest => Same(quest.Id, published))
                : null;
            var last = mine ?? indexes.LastOrDefault();
            switch (last?.Status)
            {
                case "Done":
                    return new(repository, SetupState.SetUp, last.Id, $"{Ended(setUp)} Its index #{last.Id} is done, for your review.");
                case "Declined":
                    return new(repository, SetupState.Declined, last.Id,
                        $"its index #{last.Id} was declined{(last.Note is { Length: > 0 } note ? $": {note.Trim()}" : ".")} "
                        + $"The plan does not ask it again; `daoris-driver setup {repository}` asks it alone.");
            }

            if (published is not null)
            {
                return new(repository, SetupState.Deleted, published,
                    $"its index #{published} was deleted. The plan does not ask it again; `daoris-driver setup {repository}` asks it alone.");
            }

            if (plan.Skipped.TryGetValue(repository, out var skipped))
            {
                return skipped.Code == SetupRefusals.AlreadySetUp
                    ? new(repository, SetupState.SetUp, null, setUp)
                    : new(repository, SetupState.Skipped, null, skipped.Said);
            }

            return new(repository, SetupState.ToGo, null, $"{Ended(setUp)} Its index is to go.");
        }
    }

    /// <summary>The head line (D124 §4.4): how many stand in each state, in the design's words, then whether it is paused or stopped.</summary>
    public static string Summary(WorkspaceSetupPlan plan, IReadOnlyList<SetupStanding> standings)
    {
        var counted = Spoken
            .Select(state => (State: state, Count: standings.Count(standing => standing.State == state)))
            .Where(each => each.Count > 0)
            .Select(each => $"{each.Count} {Word(each.State)}")
            .ToList();
        var tail = !plan.Live ? " — stopped"
            : plan.Paused?.By switch
            {
                SetupPausedBy.Pilot => " — paused after the pilot",
                SetupPausedBy.Person => " — paused by you",
                SetupPausedBy.Tool => " — paused: the doctrine tool cannot run here",
                null => "",
                _ => " — paused",
            };
        return $"Setting up — {(counted.Count == 0 ? "nothing to set up" : string.Join(" · ", counted))}{tail}";
    }

    /// <summary>A state in the words a person reads (§4.4).</summary>
    public static string Word(SetupState state) => state switch
    {
        SetupState.SetUp => "set up",
        SetupState.Waiting => "waiting for your review",
        SetupState.Open => "setting up",
        SetupState.Parked => "parked",
        SetupState.ToGo => "to go",
        SetupState.Skipped => "skipped",
        SetupState.Declined => "declined",
        _ => "deleted",
    };

    /// <summary>Whether the pilot holds the plan: its first <c>pilot</c> asked, and the person has not resumed it past them.</summary>
    internal static bool PilotHolds(WorkspaceSetupPlan plan, int asked) => plan.Pilot > 0 && !plan.PilotResumed && asked >= plan.Pilot;

    /// <summary>A sentence closed with its stop, so another can follow it: a summary may end with one or not.</summary>
    private static string Ended(string sentence) => sentence.EndsWith('.') ? sentence : sentence + ".";

    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
