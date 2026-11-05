namespace Daoris.Driver;

/// <summary>
/// Abandon an ask's work, or one quest's, on this machine (PAUSE1d, D132 points 6–10, design §3): listed first, then done on
/// a second press that sends exactly what the list held, with the person's reason. One implementation the screen's
/// <c>WORK_PLAN</c> and <c>WORK_ABANDON</c> and the terminal's <c>ask --abandon</c> and <c>quest abandon</c> call (D50).
/// </summary>
public static class WorkAbandoning
{
    /// <summary>The sentence both doors say when the second press carries no reason (<c>WORK_REASON</c>).</summary>
    public const string NeedsReason = "abandoning needs your reason: each declined quest keeps it.";

    /// <summary>What a record this abandon stopped says (design §4.1): a person reads it, and nothing decides from it (D104).</summary>
    public static string Note(WorkScope scope, string id) => NoteOf(scope, id).Note;

    /// <summary><see cref="Note"/> with its code and the work's id (LANG1a, the language design §4 rows 54–55).</summary>
    public static Noted NoteOf(WorkScope scope, string id) => scope == WorkScope.Ask
        ? Noted.Of(EndNoteCodes.StoppedAbandonedAsk, $"ask `#{id}` abandoned.", ("ask", id))
        : Noted.Of(EndNoteCodes.StoppedAbandonedQuest, $"quest `#{id}` abandoned.", ("quest", id));

    /// <summary>The terminal's door to the first press, and with <c>--reason</c> and <c>--yes</c> the second (design §7.2).</summary>
    public static string Door(WorkScope scope, string id) =>
        scope == WorkScope.Ask ? $"daoris ask abandon {id}" : $"daoris quest abandon {id}";

    /// <summary>
    /// What an abandon would do with each piece of a work (design §3.2). Pure: the pause's plan, this machine's claim on each
    /// taken quest, the proof of each tree, what each kept tree holds for its review, and the archive marks.
    /// </summary>
    /// <param name="claims">This machine's claim on each taken quest (D68 §4): <c>held</c>, <c>unconfirmed</c>, <c>lost</c> or <c>none</c>.</param>
    /// <param name="judged">Each tree's proof, by <see cref="AbandonKeys.TreeKey"/>.</param>
    /// <param name="held">What each kept tree holds for its review (D88's proof, the reader's), by <see cref="AbandonKeys.TreeKey"/>; null is no tree to read.</param>
    /// <param name="grounds">Each tree's base and registered checkout, by <see cref="AbandonKeys.TreeKey"/>, which the second press judges it again from.</param>
    public static AbandonPlan Plan(
        WorkPlan work, IReadOnlyDictionary<string, string> claims, IReadOnlyDictionary<string, TreeOnlyHere> judged,
        IReadOnlyDictionary<string, TreeWork?> held, IReadOnlySet<string> archived,
        IReadOnlyDictionary<string, (string? Base, string? Root)>? grounds = null, IReadOnlySet<string>? reopened = null)
    {
        var sessions = work.Pieces.Sessions;
        var quests = work.Quests.Select(each => QuestAct(each.Quest, sessions, claims, reopened)).ToList();
        var landed = work.Pieces.Landings.Select(landing => landing.Session).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trees = work.Pieces.Trees.Select(tree => TreeAct(tree, judged, landed, grounds)).ToList();
        var byKey = trees.GroupBy(tree => tree.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return new AbandonPlan(work, quests, [.. work.Sessions.Select(each => SessionAct(each.Session, byKey, held, archived))], trees)
        {
            // An ask already closed has nothing to close; a quest's work closes no ask.
            Closes = work.Ask is { State: not "Closed" } ask ? ask.Id : null,
        };
    }

    /// <summary>The first press: the work and what an abandon would do with each piece; null where no such ask or quest is here.</summary>
    public static async Task<AbandonPlan?> PlanAsync(WorkWorld world, WorkScope scope, string id, CancellationToken ct = default)
    {
        var named = WorkPausing.Named(id);
        if (await WorkPausing.PlanAsync(world, scope, named, ct).ConfigureAwait(false) is not { } work) return null;

        // Whose take each taken quest is (D68 §4): this machine's, or another's.
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var reopened = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var quest in work.Quests.Select(each => each.Quest.Quest).Where(quest => quest.Status == "Taken"))
        {
            var (claim, why) = await world.Service.ClaimStandingAsync(quest.Id, ct).ConfigureAwait(false);
            claims[quest.Id] = claim;
            if (claim == "lost" && why == "source-reopened") reopened.Add(quest.Id);
        }

        IReadOnlyList<RepoView> registry = work.Pieces.Trees.Count == 0 ? [] : await world.Service.RegistryAsync(ct).ConfigureAwait(false);
        var landed = work.Pieces.Landings.Select(landing => landing.Session).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var judged = new Dictionary<string, TreeOnlyHere>(StringComparer.OrdinalIgnoreCase);
        var held = new Dictionary<string, TreeWork?>(StringComparer.OrdinalIgnoreCase);
        var grounds = new Dictionary<string, (string? Base, string? Root)>(StringComparer.OrdinalIgnoreCase);
        var trees = world.TreesHere;
        foreach (var tree in work.Pieces.Trees)
        {
            var key = AbandonKeys.TreeKey(tree.Repository, tree.Branch);
            var ground = (Base: FirstBase(tree, work.Pieces.Sessions), Root: RootOf(registry, tree, world.Home));
            grounds[key] = ground;
            var proof = await trees.OnlyHereAsync(tree.Path, tree.Branch, ground.Base, ground.Root, ct).ConfigureAwait(false);
            judged[key] = proof;
            // A tree that stays is read for its review, as Sessions' reader reads it: its sessions are archived only where it holds nothing.
            if (proof.Kind != OnlyHereKind.Gone && (!proof.Discards || tree.Sessions.Any(landed.Contains)))
            {
                held[key] = await trees.WorkAsync(tree.Path, ct).ConfigureAwait(false);
            }
        }

        var archived = new SessionArchive(world.Home).Marks().Keys.ToHashSet(StringComparer.Ordinal);
        return Plan(work, claims, judged, held, archived, grounds, reopened) with
        {
            Abandoned = new AbandonRecord(world.Home).Last(scope, named),
        };
    }

    /// <summary>
    /// The second press (design §3.4): the pieces the first press listed, each judged again, taken in order: the scope paused,
    /// the sessions stopped and ended, the quests declined with the reason and the ask closed with it, one pass per wired
    /// workspace, the trees discarded behind the proof, the sessions archived, <c>driver.json</c> tidied, and the record.
    /// </summary>
    /// <param name="pieces">The keys the first press listed (<see cref="AbandonPlan.Pieces"/>); null is what the list holds now, a terminal's <c>--yes</c>.</param>
    /// <param name="door">The machine log's <c>door</c>: <c>screen</c> or <c>terminal</c>.</param>
    public static async Task<AbandonOutcome> AbandonAsync(
        WorkWorld world, WorkScope scope, string id, string? reason, IReadOnlyCollection<string>? pieces, string door,
        CancellationToken ct = default)
    {
        var named = WorkPausing.Named(id);
        // The reason first: each declined quest keeps it, so nothing is asked or written without one.
        if (string.IsNullOrWhiteSpace(reason)) return new AbandonOutcome(scope, named, AbandonVerdict.Reason);
        var said = reason.Trim();
        if (await PlanAsync(world, scope, named, ct).ConfigureAwait(false) is not { } plan)
        {
            return new AbandonOutcome(scope, named, AbandonVerdict.Unknown);
        }

        var listed = (pieces ?? plan.Pieces).Where(piece => !string.IsNullOrWhiteSpace(piece)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (listed.Count == 0) return new AbandonOutcome(scope, named, AbandonVerdict.Nothing);

        // Each piece judged again (design §3.1): one listed that the reader no longer takes changed since the list; one the reader
        // takes now that the list did not hold joined since, and is not taken, since the person did not see it.
        var now = plan.Pieces.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var goes = listed.Where(now.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = listed.Where(piece => !now.Contains(piece)).Select(piece => KeptNow(plan, piece) with { Changed = true }).ToList();
        var joined = plan.Pieces.Where(piece => !listed.Contains(piece, StringComparer.OrdinalIgnoreCase)).ToList();
        var failed = new List<AbandonKeep>();
        var stayed = new List<AbandonKeep>();

        // 1. The scope paused first, so nothing of the work starts between the steps that follow.
        var config = DriverConfig.Load(world.ConfigPath);
        var earlier = WorkPausing.Entry(config, scope, named);
        var at = earlier?.At ?? WorkPausing.ToSecond(world.Clock());
        WorkPausing.WithPause(config, scope, named, new WorkPause(at, earlier?.Stopped ?? new Dictionary<string, string>())).Save(world.ConfigPath);

        // 2. Every live session listed stopped as the person's, every parked one ended unanswered.
        var note = NoteOf(scope, named);
        var stopped = new List<WorkStop>();
        var unreached = new HashSet<string>(StringComparer.Ordinal);
        foreach (var session in plan.Sessions.Where(session => session.Act is AbandonAct.Stop or AbandonAct.End && goes.Contains(session.Key)))
        {
            var record = session.Session.Record;
            string? why;
            try
            {
                why = await WorkPausing.StopAsync(world, record, note, RequestDoor.Abandon, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException)
            {
                why = PauseKept.Unanswered;
            }

            if (why is null)
            {
                stopped.Add(new WorkStop(record.Id, record.Quest));
            }
            else
            {
                unreached.Add(record.Id);
                failed.Add(new AbandonKeep(session.Key, AbandonWhy.Unreached) { Detail = why, Changed = false });
            }
        }

        // Each stop recorded on the pause, so a Resume after a step that failed releases it (design §2.4).
        if (stopped.Any(stop => stop.Quest is not null))
        {
            var paused = DriverConfig.Load(world.ConfigPath);
            var entry = WorkPausing.Entry(paused, scope, named) ?? new WorkPause(at, new Dictionary<string, string>());
            var stops = new Dictionary<string, string>(entry.Stopped, StringComparer.OrdinalIgnoreCase);
            foreach (var stop in stopped.Where(stop => stop.Quest is not null)) stops[stop.Quest!] = stop.Session;
            WorkPausing.WithPause(paused, scope, named, entry with { Stopped = stops }).Save(world.ConfigPath);
        }

        // 3. Each listed quest declined with the reason, verbatim: an open one only while it is open (PAUSE1c). A quest whose
        // session could not be stopped is kept, since declining it would leave its session working on a declined quest.
        var declined = new List<string>();
        foreach (var quest in plan.Quests.Where(quest => quest.Act == AbandonAct.Decline && goes.Contains(quest.Key)))
        {
            var questId = quest.Quest.Quest.Id;
            var running = plan.Sessions.Select(session => session.Session.Record)
                .FirstOrDefault(record => Same(record.Quest, questId) && unreached.Contains(record.Id));
            if (running is not null)
            {
                failed.Add(new AbandonKeep(quest.Key, AbandonWhy.Unreached) { Detail = running.Id, Changed = false });
                continue;
            }

            try
            {
                var (ok, message) = await world.Service.DeclineQuestAsync(questId, said, quest.WhileOpen, ct).ConfigureAwait(false);
                if (ok) declined.Add(questId);
                else failed.Add(new AbandonKeep(quest.Key, AbandonWhy.Refused) { Detail = message, Changed = false });
            }
            catch (Exception error) when (error is DriverException or HttpRequestException)
            {
                failed.Add(new AbandonKeep(quest.Key, AbandonWhy.Refused) { Detail = error.Message, Changed = false });
            }
        }

        var closed = false;
        if (plan.Closes is { } ask && goes.Contains(AbandonKeys.AskKey(ask)))
        {
            try
            {
                var answer = await world.Service.CloseAskAsync(ask, said, ct).ConfigureAwait(false);
                if (answer.Ok) closed = true;
                else failed.Add(new AbandonKeep(AbandonKeys.AskKey(ask), AbandonWhy.Refused) { Detail = answer.Message, Changed = false });
            }
            catch (Exception error) when (error is DriverException or HttpRequestException)
            {
                failed.Add(new AbandonKeep(AbandonKeys.AskKey(ask), AbandonWhy.Refused) { Detail = error.Message, Changed = false });
            }
        }

        // 4. One pass per wired workspace a declined quest is held in, before the answer, and each shared decline's answer.
        var declines = await PassAsync(world, plan, declined, ct).ConfigureAwait(false);

        // 5. Each listed tree and branch discarded behind the proof, judged again now.
        var discarded = new List<AbandonedTree>();
        var discarding = plan.Trees.Where(tree => tree.Act is AbandonAct.Discard or AbandonAct.Delete && goes.Contains(tree.Key)).ToList();
        if (discarding.Count > 0)
        {
            IReadOnlyList<SessionRecord>? records = null;
            try
            {
                records = await world.Service.SessionRecordsAsync(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                // Whether a session still runs in a tree cannot be read, so no tree goes.
            }

            var landings = new LandedBranches(world.Home).Entries().Select(landing => landing.Session).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var tree in discarding)
            {
                if (records is null
                    || tree.Tree.Sessions.Any(session => records.Any(record => record.Live && string.Equals(record.Id, session, StringComparison.Ordinal))))
                {
                    failed.Add(new AbandonKeep(tree.Key, AbandonWhy.InUse) { Changed = false });
                    continue;
                }

                if (tree.Tree.Sessions.Any(landings.Contains))
                {
                    changed.Add(new AbandonKeep(tree.Key, AbandonWhy.Landed) { Changed = true });
                    continue;
                }

                var proof = await world.TreesHere.OnlyHereAsync(tree.Tree.Path, tree.Tree.Branch, tree.BaseCommit, tree.Root, ct).ConfigureAwait(false);
                if (!proof.Discards)
                {
                    changed.Add(new AbandonKeep(tree.Key, TreeWhy(proof)) { Changed = true, Where = proof.Where });
                    continue;
                }

                var removed = await world.TreesHere.DiscardAsync(tree.Tree.Path, tree.Tree.Branch, proof, ct).ConfigureAwait(false);
                if (removed.Removed)
                {
                    discarded.Add(new AbandonedTree(
                        tree.Tree.Repository, tree.Tree.Branch, proof.Tip, proof.Commits, proof.Uncommitted, tree.Tree.Sessions, Alone: !proof.TreeHere));
                }
                else
                {
                    failed.Add(new AbandonKeep(tree.Key, AbandonWhy.Refused) { Detail = removed.Message, Changed = false });
                }
            }
        }

        // 6. Each listed session the reader now places in Ended archived; a session stopped whose tree stays with work is left to review.
        var archived = await ArchiveAsync(world, plan, goes, unreached, changed, stayed, failed, ct).ConfigureAwait(false);

        // 7. driver.json tidied: a closed quest is never planned again (D46 §3), so its release and mark go; then the pause,
        // unless a step failed or something joined, since a half-finished abandon must never leave its work starting again.
        var closedNow = new HashSet<string>(declined, StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var quest in await world.Service.EveryQuestAsync(ct).ConfigureAwait(false))
            {
                if (quest.Status is "Done" or "Declined" && plan.Work.Pieces.Has(quest.Id)) closedNow.Add(quest.Id);
            }
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            // What this abandon declined is closed whatever the read says.
        }

        var stillPaused = failed.Count > 0 || joined.Count > 0;
        var tidy = DriverConfig.Load(world.ConfigPath);
        var handed = new HandedQuests(world.Home);
        foreach (var quest in closedNow)
        {
            tidy = tidy.WithoutQuest(quest);
            // A quest held on another agent (MIXAGENT1a-ii) goes back to the machine's when it closes.
            handed.Clear(quest);
        }

        if (!stillPaused) tidy = WorkPausing.WithPause(tidy, scope, named, null);
        tidy.Save(world.ConfigPath);

        // 8. The record (design §4.2) and the log line (§4.3).
        stayed.InsertRange(0, plan.Kept.Where(keep => !changed.Any(each => string.Equals(each.Piece, keep.Piece, StringComparison.OrdinalIgnoreCase))));
        var outcome = new AbandonOutcome(scope, named, AbandonVerdict.Abandoned)
        {
            Listed = listed.Count,
            Went = listed.Count - changed.Count - failed.Count,
            Changed = changed,
            Failed = failed,
            Joined = joined,
            Declined = declined,
            Closed = closed,
            Stopped = stopped,
            Discarded = discarded,
            Archived = archived,
            Stayed = stayed,
            Declines = declines,
            StillPaused = stillPaused,
        };
        await RecordAsync(world, outcome, door, said, ct).ConfigureAwait(false);
        return outcome;
    }

    /// <summary>
    /// Step 4 (design §3.4, §5.2): one pass for each wired workspace a declined quest is held in, as D95's delete runs one, then
    /// each decline read back. Lost where another machine's take stood, unconfirmed where the pass did not reach the remote or
    /// left the quest behind, confirmed otherwise. A workspace with no remote answers none: its declines travel nowhere.
    /// </summary>
    private static async Task<IReadOnlyList<AbandonDecline>> PassAsync(WorkWorld world, AbandonPlan plan, IReadOnlyList<string> declined, CancellationToken ct)
    {
        var answers = new List<AbandonDecline>();
        if (world.Sync is not { } sync || declined.Count == 0) return answers;

        var workspaceOf = plan.Quests.ToDictionary(quest => quest.Quest.Quest.Id, quest => quest.Quest.Quest.Workspace, StringComparer.OrdinalIgnoreCase);
        foreach (var circle in declined.GroupBy(quest => workspaceOf.GetValueOrDefault(quest) ?? RemoteTarget.DefaultWorkspace, StringComparer.OrdinalIgnoreCase))
        {
            SyncReport? report;
            try
            {
                report = await sync(circle.Key, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or TaskCanceledException)
            {
                report = new SyncReport(error.Message);
            }

            if (report is null) continue;

            IReadOnlyList<string>? behind = null;
            IReadOnlyList<QuestView>? after = null;
            try
            {
                behind = (await world.Service.SyncStandingAsync(circle.Key, ct).ConfigureAwait(false)).Behind;
                after = await world.Service.EveryQuestAsync(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                // Read back nothing, so nothing is confirmed.
            }

            foreach (var quest in circle)
            {
                var status = after?.FirstOrDefault(each => string.Equals(each.Id, quest, StringComparison.OrdinalIgnoreCase))?.Status;
                answers.Add(new AbandonDecline(quest, status is not null && status != "Declined"
                    ? DeclineAnswer.Lost
                    : report.Problem is not null || status is null || behind is null || behind.Contains(quest, StringComparer.OrdinalIgnoreCase)
                        ? DeclineAnswer.Unconfirmed
                        : DeclineAnswer.Confirmed));
            }
        }

        return answers;
    }

    /// <summary>
    /// Step 6 (design §3.4): each listed session archived through <see cref="SessionArchive"/>, judged by Sessions' own reader as
    /// it now places it, so the abandon never hides what needs the person (D126 §5.2).
    /// </summary>
    private static async Task<IReadOnlyList<string>> ArchiveAsync(
        WorkWorld world, AbandonPlan plan, IReadOnlySet<string> goes, IReadOnlySet<string> unreached,
        List<AbandonKeep> changed, List<AbandonKeep> stayed, List<AbandonKeep> failed, CancellationToken ct)
    {
        var listed = plan.Sessions.Where(session => goes.Contains(session.Key) && !unreached.Contains(session.Session.Record.Id)).ToList();
        foreach (var session in listed.Where(session => session.Act is AbandonAct.Stop or AbandonAct.End && !session.Archive && session.Why is not null))
        {
            stayed.Add(new AbandonKeep(session.Key, session.Why!));
        }

        var asked = listed.Where(session => session.Act == AbandonAct.Archive || (session.Act is AbandonAct.Stop or AbandonAct.End && session.Archive)).ToList();
        if (asked.Count == 0) return [];

        var ids = asked.Select(session => session.Session.Record.Id).ToList();
        try
        {
            var look = await SessionGroups.LookAsync(
                world.Service, DriverConfig.Load(world.ConfigPath), world.Unadopted, lastLook: null, world.Home, ids, ct).ConfigureAwait(false);
            var answer = new SessionArchive(world.Home).Archive(ids, SessionGroups.Read(look, ids), [.. look.Records.Select(record => record.Id)], world.Clock());
            var archived = new List<string>();
            foreach (var outcome in answer.Outcomes)
            {
                if (outcome.Verdict == ArchiveVerdict.Archived)
                {
                    archived.Add(outcome.Session);
                    continue;
                }

                var session = asked.First(each => string.Equals(each.Session.Record.Id, outcome.Session, StringComparison.Ordinal));
                var why = outcome.Verdict switch
                {
                    ArchiveVerdict.Unknown => AbandonWhy.Gone,
                    ArchiveVerdict.NeedsYou when outcome.Group == SessionGroup.Review => AbandonWhy.Review,
                    _ => AbandonWhy.Unreached,
                };
                // An archive the list held alone changed since; one after a stop is the stop's, and the session stays named.
                if (session.Act == AbandonAct.Archive) changed.Add(new AbandonKeep(session.Key, why) { Changed = true });
                else stayed.Add(new AbandonKeep(session.Key, why));
            }

            return archived;
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            foreach (var session in asked.Where(session => session.Act == AbandonAct.Archive))
            {
                failed.Add(new AbandonKeep(session.Key, AbandonWhy.Refused) { Detail = error.Message, Changed = false });
            }

            return [];
        }
    }

    /// <summary>Step 8: <c>abandoned.json</c>, dropping entries whose ask or quest has no record here any more, and the log line.</summary>
    private static async Task RecordAsync(WorkWorld world, AbandonOutcome outcome, string door, string reason, CancellationToken ct)
    {
        var entry = new AbandonEntry(WorkPausing.Word(outcome.Scope), outcome.Id, world.Clock(), door, reason)
        {
            Declined = outcome.Declined,
            Closed = outcome.Closed,
            Trees = outcome.Discarded,
            Stopped = [.. outcome.Stopped.Select(stop => stop.Session)],
            Archived = outcome.Archived,
            // What git or the service said stays with the terminal's answer: the record holds words from fixed lists, never a path.
            Stayed = [.. outcome.Stayed, .. outcome.Changed, .. outcome.Failed.Select(keep => keep with { Detail = null })],
            Declines = outcome.Declines,
        };

        Func<AbandonEntry, bool>? known = null;
        try
        {
            var asks = (await world.Service.EveryAskAsync(ct).ConfigureAwait(false)).Select(ask => ask.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var quests = (await world.Service.EveryQuestAsync(ct).ConfigureAwait(false)).Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            known = each => each.Scope == "ask" ? asks.Contains(each.Id) : quests.Contains(each.Id);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            // Which records still stand cannot be read, so every entry stays.
        }

        try
        {
            new AbandonRecord(world.Home).Write(entry, known);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A record not written is no record, and the abandon still abandoned (design §4.2).
        }

        world.Log?.Info("work.abandoned",
            ("scope", entry.Scope),
            ("declined", outcome.Declined.Count),
            ("discarded", outcome.Discarded.Count(tree => !tree.Alone)),
            ("branches", outcome.Discarded.Count(tree => tree.Alone)),
            ("archived", outcome.Archived.Count),
            ("kept", entry.Stayed.Count),
            ("lost", outcome.Declines.Count(decline => decline.Answer == DeclineAnswer.Lost)),
            ("door", door));
    }

    /// <summary>A quest's act (design §3.2): what Daoris made for this work on this machine goes, and what is somewhere else stays.</summary>
    private static AbandonQuest QuestAct(
        WorkQuest quest, IReadOnlyList<WorkSession> sessions, IReadOnlyDictionary<string, string> claims, IReadOnlySet<string>? reopened)
    {
        var id = quest.Quest.Id;
        switch (quest.Quest.Status)
        {
            case "Open":
                return new AbandonQuest(quest, AbandonAct.Decline) { WhileOpen = true };
            case "Done":
                return new AbandonQuest(quest, AbandonAct.Keep) { Why = AbandonWhy.Done };
            case "Taken" when claims.GetValueOrDefault(id) is "held" or "unconfirmed":
                // This machine's take: Daoris's where a session record of this machine's took it (STANDDOWN2), else outside Daoris.
                return sessions.Any(session => !session.Teammate && session.Record.Took && Same(session.Record.Quest, id))
                    ? new AbandonQuest(quest, AbandonAct.Decline)
                    : new AbandonQuest(quest, AbandonAct.Keep) { Why = AbandonWhy.TakenOutside };
            case "Taken" when reopened?.Contains(id) == true && claims.GetValueOrDefault(id) == "lost":
                // FABRIC-CLOSE1c20: lost to the person's own reopen, said as that, never as another machine's take.
                return new AbandonQuest(quest, AbandonAct.Keep) { Why = AbandonWhy.SourceReopened };
            case "Taken":
                return new AbandonQuest(quest, AbandonAct.Keep)
                {
                    Why = AbandonWhy.TakenElsewhere,
                    Machine = sessions.Where(session => session.Teammate && Same(session.Record.Quest, id))
                        .Select(session => session.Record.Id[..session.Record.Id.IndexOf('/')]).LastOrDefault(),
                };
            default:
                // Declined already, or a state this build does not know: nothing to take.
                return new AbandonQuest(quest, AbandonAct.None);
        }
    }

    /// <summary>A tree's act (design §3.3): discarded behind the proof; a session a landing names keeps it whatever git says.</summary>
    private static AbandonTree TreeAct(
        WorkTree tree, IReadOnlyDictionary<string, TreeOnlyHere> judged, IReadOnlySet<string> landed,
        IReadOnlyDictionary<string, (string? Base, string? Root)>? grounds)
    {
        var key = AbandonKeys.TreeKey(tree.Repository, tree.Branch);
        var proof = judged.GetValueOrDefault(key) ?? new TreeOnlyHere(OnlyHereKind.Unknown);
        var ground = grounds?.GetValueOrDefault(key) ?? default;
        var named = new AbandonTree(tree, AbandonAct.Keep, proof) { BaseCommit = ground.Base, Root = ground.Root };
        if (proof.Kind == OnlyHereKind.Gone) return named with { Act = AbandonAct.None };
        if (tree.Sessions.Any(landed.Contains)) return named with { Why = AbandonWhy.Landed };
        return proof.Discards
            ? named with { Act = proof.TreeHere ? AbandonAct.Discard : AbandonAct.Delete }
            : named with { Why = TreeWhy(proof) };
    }

    /// <summary>
    /// A session's act (design §3.2): stopped or ended, then archived once nothing keeps it needing the person. A teammate's
    /// live session is named and never reached (D47 §4); a teammate's ended one may be archived here, a mark of this machine's.
    /// </summary>
    private static AbandonSession SessionAct(
        WorkSession session, IReadOnlyDictionary<string, AbandonTree> trees, IReadOnlyDictionary<string, TreeWork?> held, IReadOnlySet<string> archived)
    {
        var record = session.Record;
        if (record.Teammate && record.Live)
        {
            return new AbandonSession(session, AbandonAct.Keep) { Why = AbandonWhy.Teammate, Machine = record.Id[..record.Id.IndexOf('/')] };
        }

        // To review where its tree stays and holds work nobody accepted, as Sessions' reader reads it (D126 §5.2).
        var review = session.Branch is { } branch
            && trees.TryGetValue(AbandonKeys.TreeKey(record.Repository, branch), out var tree)
            && tree.Act == AbandonAct.Keep
            && held.GetValueOrDefault(tree.Key) is { Holds: true };
        if (record.Live)
        {
            var act = record.State == "awaiting-person" ? AbandonAct.End : AbandonAct.Stop;
            return review ? new AbandonSession(session, act) { Why = AbandonWhy.Review } : new AbandonSession(session, act) { Archive = true };
        }

        if (archived.Contains(record.Id)) return new AbandonSession(session, AbandonAct.None);
        return review
            ? new AbandonSession(session, AbandonAct.Keep) { Why = AbandonWhy.Review }
            : new AbandonSession(session, AbandonAct.Archive) { Archive = true };
    }

    /// <summary>Why a tree the proof did not clear stays.</summary>
    private static string TreeWhy(TreeOnlyHere proof) => proof.Kind switch
    {
        OnlyHereKind.Elsewhere => AbandonWhy.Elsewhere,
        OnlyHereKind.CheckedOut => AbandonWhy.CheckedOut,
        OnlyHereKind.Gone => AbandonWhy.Gone,
        _ => AbandonWhy.Unknown,
    };

    /// <summary>What the reader now says of a piece the list held to go and no longer takes: kept with why, or gone.</summary>
    private static AbandonKeep KeptNow(AbandonPlan plan, string piece)
    {
        bool Is(string key) => string.Equals(key, piece, StringComparison.OrdinalIgnoreCase);
        if (plan.Quests.FirstOrDefault(quest => Is(quest.Key)) is { Why: { } questWhy } quest) return new AbandonKeep(piece, questWhy) { Machine = quest.Machine };
        if (plan.Sessions.FirstOrDefault(session => Is(session.Key)) is { Why: { } sessionWhy } session) return new AbandonKeep(piece, sessionWhy) { Machine = session.Machine };
        if (plan.Trees.FirstOrDefault(tree => Is(tree.Key)) is { Why: { } treeWhy } tree) return new AbandonKeep(piece, treeWhy) { Where = tree.Judged.Where };
        return new AbandonKeep(piece, AbandonWhy.Gone);
    }

    /// <summary>The base the tree's commits are judged from: its first session's that has one (D80: a carry-on goes back into its tree).</summary>
    private static string? FirstBase(WorkTree tree, IReadOnlyList<WorkSession> sessions) =>
        tree.Sessions
            .Select(id => sessions.FirstOrDefault(session => string.Equals(session.Record.Id, id, StringComparison.Ordinal))?.Record.BaseCommit)
            .FirstOrDefault(baseCommit => baseCommit is { Length: > 0 });

    /// <summary>The repository's registered checkout in the tree's workspace, read from the layout Daoris chose (<c>trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>).</summary>
    private static string? RootOf(IReadOnlyList<RepoView> registry, WorkTree tree, string home)
    {
        string? workspace = null;
        try
        {
            var parts = Path.GetRelativePath(DaorisHome.Entry(home, HomeEntry.Trees), tree.Path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length >= 3) workspace = RemoteTarget.Workspace(parts[0]);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path no folder could have names no workspace.
        }

        var rows = registry.Where(row => string.Equals(row.Repository, tree.Repository, StringComparison.OrdinalIgnoreCase)).ToList();
        return (rows.FirstOrDefault(row => workspace is not null && string.Equals(row.Workspace, workspace, StringComparison.OrdinalIgnoreCase)) ?? rows.FirstOrDefault())?.Root;
    }

    private static bool Same(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>What a terminal prints of the first press.</summary>
    public static IReadOnlyList<string> Lines(AbandonPlan plan) => AbandonLines.Of(plan);

    /// <summary>What a terminal prints of the second press.</summary>
    public static IReadOnlyList<string> Lines(AbandonOutcome outcome) => AbandonLines.Of(outcome);
}
