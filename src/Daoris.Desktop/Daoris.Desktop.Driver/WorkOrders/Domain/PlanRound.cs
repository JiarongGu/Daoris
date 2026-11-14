namespace Daoris.Driver;

/// <summary>
/// One tick's plan: the state the quests are planned against, and the walk that gives each quest its consideration in the order the
/// reasons are weighed.
/// </summary>
internal sealed class PlanRound
{
    private readonly RoundReading reading;
    private readonly RoundSaying saying;

    internal PlanRound(
        Snapshot snapshot, DriverConfig config, bool unadopted, bool window, IRoleStarts? roles, Action<LineCourse>? decided)
    {
        Snapshot = snapshot;
        Config = config;
        Unadopted = unadopted;
        Window = window;

        // A start of this machine's still opening takes its place as a live session does, counted once where its record has opened
        // since and an active session already stands for its quest.
        Opening = Planner.Opening(snapshot);
        // The cap counts what the driver started, never a conversation the person holds; the active list keeps them for the busy read.
        Slots = config.Cap - snapshot.SlotsHeld - Opening.Count;

        reading = new RoundReading(snapshot, config, roles, decided);
        saying = new RoundSaying(this, reading);
    }

    internal Snapshot Snapshot { get; }

    internal DriverConfig Config { get; }

    /// <summary>Which door this machine's starts ride: it decides whether a repository that registered without adopting can be driven.</summary>
    internal bool Unadopted { get; }

    /// <summary>Whether this loop carries Daoris's browser, the window a local set-up step shows its chain's work in.</summary>
    internal bool Window { get; }

    internal IReadOnlyList<StartReservation> Opening { get; }

    internal Dictionary<string, string> StartedThisTick { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The trees a start planned at this look goes into, each with its quest: a tree takes one session, and a further part of a
    /// session may now go into a tree a resume or a carry-on goes back into, so each holds it for the other.
    /// </summary>
    internal List<(string Tree, string Quest)> TreesThisTick { get; } = [];

    /// <summary>The quests started at this look, each an earlier unit to a later start whose declared paths meet its own.</summary>
    internal List<QuestView> StartsThisLook { get; } = [];

    /// <summary>The places left under the cap; every start planned at this look spends one.</summary>
    internal int Slots { get; set; }

    internal IReadOnlyList<Consideration> Run()
    {
        var considerations = new List<Consideration>();

        // The service already orders open-oldest-first; keeping its order is what makes "oldest starts first" one implementation
        // rather than two that drift. But a record the person wrote to goes first: a reopen goes before every start the driver
        // planned itself, so the quests whose last session here has words waiting are planned ahead of the rest, the open list's in
        // its order and then the closed ones no list holds.
        var writtenTo = Snapshot.Quests.Concat(Snapshot.Closed)
            .Where(reading.WrittenTo)
            .DistinctBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var quest in writtenTo.Concat(Snapshot.Quests.Where(quest => !reading.WrittenTo(quest)))
                     .OrderBy(quest => Snapshot.HeldStarts.Contains(quest.Id)))
        {
            // A step whose source the person reopened: withdrawn while open and held while taken, until they go on with it on the
            // new work or abandon it. Before every other reason, a pause and the person's stop included, since neither Resume nor
            // Try again moves it: only their choice does, at the service. It spends no slot and no turn.
            if (quest.HeldByReopen && reading.Plans(quest))
            {
                considerations.Add(HoldVerdicts.Reopened(quest));
                continue;
            }

            // A pause holds every quest of its work this planner would plan, before every other reason, the person's stop
            // included: Resume is the one press that moves it, so a released stop starts nothing meanwhile. First, too, so a paused
            // quest spends no slot and no repository's turn, and the next starts where it would have. Words written to its session
            // wait for Resume too.
            if (Snapshot.Paused.TryGetValue(quest.Id, out var pause) && reading.Plans(quest))
            {
                considerations.Add(HoldVerdicts.Paused(quest, pause));
                continue;
            }

            // The person wrote to its last session here, parked or ended: that record goes on with their words. Before the
            // person's stop, which the words release as Try again does: the same session goes on with them. Or the session its
            // reopen names, told it: see WordsOn.
            if (reading.WordsOn(quest) is { } written)
            {
                considerations.Add(saying.GoOn(quest, written));
                continue;
            }

            // A person's stop holds its quest, open or taken, until they release that stop. Before everything else, since it is the
            // one reason the quest sits that the person must act on.
            if (reading.StopHolding(quest) is { } last)
            {
                considerations.Add(HoldVerdicts.Stopped(quest, last));
                continue;
            }

            // A quest, Open or Taken, whose last session here ended past its transient bound is held for the person, read by its
            // note's code and never a mark, before every other path: no carry-on, no fresh start and no resume until their word or a
            // hand-over from that record.
            if (reading.TransientHolding(quest) is { } exhausted)
            {
                considerations.Add(Snapshot.PolicyHolds.TryGetValue(quest.Id, out var policyHold)
                    ? HoldVerdicts.PolicyHeld(quest, exhausted, policyHold)
                    : HoldVerdicts.TransientHeld(quest, exhausted));
                continue;
            }

            // A line an account's limit cut off whose role's policy asks, or whose bound of moves is reached, waits for the person's yes.
            if (Snapshot.PolicyHolds.TryGetValue(quest.Id, out var limitHold) && quest.Status is "Open" or "Taken"
                && Snapshot.LastRun.TryGetValue(quest.Id, out var limitedHeld)
                && (limitedHeld.Limit || StaffSnapshotCourse.EndedQuiet(limitedHeld) || StaffSnapshotCourse.FailedStrike(limitedHeld)))
            {
                considerations.Add(HoldVerdicts.PolicyHeld(quest, limitedHeld, limitHold));
                continue;
            }

            // The hand-over made from that held record: an Open quest is carried on from it in its tree, whatever its tree holds,
            // never started afresh with no brief.
            if (quest.Status == "Open" && reading.TransientReleasedFrom(quest) is { } released)
            {
                considerations.Add(saying.CarryOn(quest, released));
                continue;
            }

            // A session that committed work and failed before it took its quest (its connector could not start) leaves the quest
            // open, to be started again in a new tree that would build the same work from nothing. Where its tree holds commits
            // beyond its start, the quest is carried on there, as a cut-off of a take is; the strikes still count it, so a quest
            // that fails so again parks for the person with its work in the tree. Whatever holds the carry-on, its verdict names the
            // work, so the sessions' groups read that tree as one the quest goes back into.
            if (quest.Status == "Open" && reading.RetriedFrom(quest) is { } failed)
            {
                considerations.Add(saying.CarryOn(quest, failed) with { CarriesWork = Snapshot.Committed[quest.Id] });
            }
            // An open quest whose last session here an account's limit ended is the line's resume, carried on from that record,
            // never started afresh in a new tree: its first part held a conversation too.
            else if (quest.Status == "Open" && reading.LimitedFrom(quest) is { } limited)
            {
                considerations.Add(saying.CarryOn(quest, limited));
            }
            else if (quest.Status == "Open")
            {
                var considered = saying.Considering.Consider(quest);
                // A chain's next step in the repository its parent ran in builds on that run; any other first start may be a further
                // part of a session, which goes into its tree or runs alongside.
                considerations.Add(considered.Verdict != StartVerdict.Start ? considered
                    : quest.Parent is { } parent
                      && Snapshot.LastRun.TryGetValue(parent, out var before)
                      && string.Equals(before.Repository, quest.To, StringComparison.OrdinalIgnoreCase)
                        ? considered with { BuildsOn = before }
                        : saying.Joining(considered));
            }
            else if (quest is { Status: "Taken", Awaits: { Length: > 0 } awaits }
                     && !reading.ResumedOnAnswer(quest)
                     && Snapshot.LastRun.TryGetValue(quest.Id, out var prior))
            {
                considerations.Add(saying.Resume(quest, awaits, prior));
            }
            else if (quest is { Status: "Taken", Awaits: null or "" } && reading.CarriedOnFrom(quest) is { } cutOff)
            {
                // Whose the take is, before anything a carry-on would sit for: the ledger refuses one over a take that is not this
                // machine's, whatever is opted in or held here.
                considerations.Add(saying.TakenElsewhere(quest, cutOff) ?? saying.CarryOn(quest, cutOff));
            }
            else if (UnclosedQuests.LeftBy(quest, Snapshot) is { } left)
            {
                considerations.Add(UnclosedQuests.Held(quest, left));
            }
        }

        return considerations;
    }
}
