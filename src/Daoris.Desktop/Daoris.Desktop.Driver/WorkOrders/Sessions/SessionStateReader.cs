namespace Daoris.Driver;

/// <summary>
/// The one place a session's state is read: needs you, failed, working, pending or done, from its record, the planner's
/// verdict on its quest and its run's waits, with the finer reason inside; gathered once per look and read by
/// <see cref="SessionGroups.Read"/>, so the list, the head, Notifications and the terminal say one state.
/// </summary>
/// <remarks>
/// <para><b>The first state that holds</b>: a live record by its own state; then what holds the person's words; a parked quest;
/// a helper's ask; its run's waits; an awaiting reply; then its ending, read against the verdict where the planner still considers
/// its quest. A tree holding commits no run waits on is said in its session, never as a state.</para>
///
/// <para><b>A teammate's record is read by its state only.</b> Its park waits on them, and the planner's verdicts are this
/// machine's.</para>
///
/// <para><b>Archive never hides what needs the person</b>: the mark moves a done or failed session to the Archived fold, and leaves
/// every other where its state puts it, its mark said.</para>
/// </remarks>
internal sealed class SessionStateReader
{
    private readonly SessionLook _look;
    private readonly Dictionary<string, Consideration> _verdicts;
    private readonly Dictionary<string, QuestView> _quests;
    private readonly HashSet<string> _liveTrees;
    private readonly Dictionary<string, string> _newestOnTree;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PersonWait>> _runWaits;

    public SessionStateReader(SessionLook look)
    {
        _look = look;
        _runWaits = RunWaitSessions.Of(look);
        _verdicts = look.Considered
            .GroupBy(consideration => consideration.Quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _quests = look.Quests
            .GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var ours = look.Records.Where(record => !record.Teammate && record.Tree is not null).ToList();
        // An open conversation between turns writes nothing into its tree until the person says more.
        _liveTrees = new HashSet<string>(
            ours.Where(record => record.Live && !look.Between.Contains(record.Id)).Select(record => SessionGroups.Normal(record.Tree!)),
            StringComparer.OrdinalIgnoreCase);
        _newestOnTree = ours
            .GroupBy(record => SessionGroups.Normal(record.Tree!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(record => record.Created).ThenByDescending(record => record.Id, StringComparer.Ordinal).First().Id,
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A session's state and where it is listed, and what its own tree offers to land in whichever state it rests, bar one that
    /// runs or goes on with the person's words: either is about to write into that tree again. Where the line holds its commits by
    /// content, its discard instead. An open conversation between turns offers its landing too, by the same judgement, and never a
    /// discard while it is open.
    /// </summary>
    public SessionGrouping Place(SessionRecord record)
    {
        var row = StateOf(record);
        var between = _look.Between.Contains(record.Id);
        if (row.Shown == ShownState.GoingOn
            || LandableTree(record) is not { } tree
            || !_look.Trees.TryGetValue(SessionGroups.Normal(tree), out var work))
        {
            return row;
        }

        return between
            ? row with { Lands = LandOffer.Of(tree, work) }
            : row with { Lands = LandOffer.Of(tree, work), Discards = DiscardOffer.Of(tree, work) };
    }

    private SessionGrouping StateOf(SessionRecord record)
    {
        // A failure an account's limit made is said limited, in whichever state it rests.
        var row = new SessionGrouping(record.Id, SessionGroup.Done, ShownState.Of(record))
        {
            Archived = _look.Archived.ContainsKey(ConversationArchive.Of(record)),
            Teammate = record.Teammate,
            // The ledger's half, as the service answered it, and this machine's.
            Deletable = record.Deletable && !record.Live && !record.Teammate && !_look.Kept.Contains(record.Id),
        };

        if (record.Live)
        {
            // A live session of a quest whose earlier sessions here failed is another try,
            // and its head says how many failed before it, counted as the planner counts them, from the last *Try again*.
            if (!record.Teammate && record.Quest is { } quest && _look.Strikes.TryGetValue(quest, out var failed) && failed > 0)
            {
                row = row with { Strikes = failed };
            }

            // The call its agent is inside, said only while it works; a park's turn ended, and a teammate's calls
            // run on their machine, which this one heard none of.
            if (!record.Teammate && record.State == "working" && _look.Calls.TryGetValue(record.Id, out var call))
            {
                row = row with { Calling = call };
            }

            // An answered park goes on at the driver's next look; a teammate's park waits on them, since nothing this window
            // sends reaches its process.
            if (record.Answered) return In(row with { Shown = ShownState.Answered }, SessionGroup.Working);
            if (SessionGroups.WaitsOnYou(record.Id, record.State, record.Answered)) return In(row, SessionGroup.NeedsYou, SessionReason.Question);
            return In(row, record.State == "queued" ? SessionGroup.Pending : SessionGroup.Working);
        }

        if (record.Teammate) return Ended(row, record, verdict: null);

        // A chat the person's words wait on goes on with them, since its runner takes them up at once,
        // unless the runner says what holds them.
        if (_look.ChatsWaiting.Contains(record.Id))
        {
            return _look.Held.TryGetValue(record.Id, out var chat)
                ? Held(row.HeldBy(chat), chat)
                : In(row with { Shown = ShownState.GoingOn }, SessionGroup.Working);
        }

        var verdict = VerdictOnLast(record);
        // Its line says the stop holds its quest, in whichever state it rests; and that a pause does, since a pause is the
        // reason before a stop. So does its end past its transient bound, which *Try again* releases for that end alone.
        row = row with { HoldsQuest = verdict?.Verdict is StartVerdict.Stopped or StartVerdict.TransientHeld or StartVerdict.PolicyHeld, PausedBy = verdict?.PausedBy };

        // The person's words wait on it, so it goes on with them where the planner starts it; else what holds them is its state.
        // Before parked: words written to it are the person's Try again, and its tree is what it goes on in. The session a reopen
        // names, though a later one is its quest's last here, reads that quest's verdict for its words.
        if (WordsWait(record) && VerdictOn(record) is { } words)
        {
            if (Standing(words, held: true) is { } standing) return In(row, standing.State, standing.Reason);
            // The planner's own hold first; where it starts them, the cool-off a fresh plan cannot see, read from
            // the home for the account the words resume on.
            if ((WordsHold.Of(words, WaitOn(words.Quest.Id)) ?? _look.Held.GetValueOrDefault(record.Id)) is not { } holds)
            {
                return In(row with { Shown = ShownState.GoingOn }, SessionGroup.Working);
            }

            return Held(row.HeldBy(holds) with { PausedBy = words.PausedBy }, holds);
        }

        if (verdict?.Verdict == StartVerdict.Exhausted)
        {
            var strikes = _look.Strikes.TryGetValue(record.Quest!, out var counted) ? counted : (int?)null;
            return In(row with { Shown = ShownState.Parked, Strikes = strikes }, SessionGroup.Failed);
        }

        // It ended its turn on its own helper's ask and goes on with the answer in its own tree: the planner considers
        // nothing of its taken quest meanwhile, so no verdict says so.
        if (record.AwaitsHelper) return In(row with { Shown = ShownState.AwaitingHelper }, SessionGroup.Working);

        if (_runWaits.TryGetValue(record.Id, out var waits))
        {
            var held = ReviewableTree(record) is { } tree && _look.Trees.TryGetValue(SessionGroups.Normal(tree), out var work) && work.Holds ? work : null;
            var waited = row.WaitedOn(waits, held);
            return In(waited, SessionGroup.NeedsYou, waited.Reason);
        }

        if (verdict is { Verdict: StartVerdict.Waiting, Quest.Awaits: { Length: > 0 } awaits })
        {
            return In(row with
            {
                Shown = ShownState.AwaitingReply,
                Awaits = awaits,
                AwaitsOf = _quests.TryGetValue(awaits, out var question) ? question.To : null,
            }, SessionGroup.Working);
        }

        return Ended(row, record, verdict);
    }

    /// <summary>
    /// An ended record's state: what the planner's verdict on its quest says where it still considers it (a hold the person
    /// answers, a failure it stops at, a carry-on, a next start held), else its own ending.
    /// </summary>
    private SessionGrouping Ended(SessionGrouping row, SessionRecord record, Consideration? verdict)
    {
        if (verdict is not null)
        {
            if (Standing(verdict, held: false) is { } standing) return In(row, standing.State, standing.Reason);
            // A verdict on words' go-on, or on a closed quest, says nothing of this session once no words wait on it.
            if (!verdict.GoesOn && verdict.Quest.Status is "Open" or "Taken")
            {
                return verdict.Verdict == StartVerdict.Start
                    ? In(row, SessionGroup.Working, SessionReason.CarriedOn)
                    : In(row, SessionGroup.Pending, (WordsHold.Of(verdict, WaitOn(verdict.Quest.Id)) ?? new(WordsHold.Waits, verdict.Reason)).Why);
            }
        }

        // A done with a departure on it is never plain done: it needs the person until they accept the departure, which is their word on it.
        if (record.State == "completed" && record.Quest is { } closed && _quests.TryGetValue(closed, out var done) && QuestLeft.Has(done)
            && _look.LastRun.TryGetValue(closed, out var newest) && string.Equals(newest.Session, record.Id, StringComparison.Ordinal))
        {
            return QuestLeft.Waits(done) ? In(row, SessionGroup.NeedsYou, SessionReason.Left) : In(row, SessionGroup.Done, SessionReason.PartLeft);
        }

        return In(row, record.State is "failed" or "not-started" ? SessionGroup.Failed : SessionGroup.Done);
    }

    /// <summary>
    /// The state a verdict puts a session in whatever its ending, or null where it is a plain hold or a start. The person's own stop
    /// is done; where their words wait on it, it is the hold of those words.
    /// </summary>
    private (string State, string Reason)? Standing(Consideration verdict, bool held) => verdict.Verdict switch
    {
        StartVerdict.PolicyHeld => (SessionGroup.NeedsYou, SessionReason.PolicyHeld),
        StartVerdict.Unclosed => (SessionGroup.NeedsYou, SessionReason.Unclosed),
        StartVerdict.Stopped when verdict.Said?.Code == SittingFact.Steward => (SessionGroup.NeedsYou, SessionReason.StewardStop),
        StartVerdict.Blocked when WaitOn(verdict.Quest.Id) is { Until: null } => (SessionGroup.NeedsYou, SessionReason.SignIn),
        StartVerdict.Exhausted when !held => (SessionGroup.Failed, ShownState.Parked),
        StartVerdict.TransientHeld => (SessionGroup.Failed, SessionReason.Transient),
        StartVerdict.TakenElsewhere => (SessionGroup.Working, SessionReason.Elsewhere),
        StartVerdict.WaitsForOpinion => (SessionGroup.Working, SessionReason.Opinion),
        StartVerdict.Stopped when !held => (SessionGroup.Done, "stopped"),
        _ => null,
    };

    /// <summary>A row its words' hold keeps pending, its reason the hold's code.</summary>
    private static SessionGrouping Held(SessionGrouping row, WordsHold holds) => In(row, SessionGroup.Pending, holds.Why);

    /// <summary>
    /// The row in <paramref name="state"/> for <paramref name="reason"/>, its shown word where none is named; listed in the Archived
    /// fold where its mark stands and it is done or failed.
    /// </summary>
    private static SessionGrouping In(SessionGrouping row, string state, string? reason = null) => row with
    {
        State = state,
        Reason = reason ?? row.Shown,
        Group = row.Archived && (state is SessionGroup.Done or SessionGroup.Failed || reason == SessionReason.Left) ? SessionGroup.Archived : state,
    };

    /// <summary>
    /// The tree an ended session of this machine's would offer to land, were there commits in it: its own, the newest
    /// session on it, held by nothing live. Whatever its ending, and whether or not its quest goes back into it: the person
    /// may land what it committed before a carry-on writes more. An open conversation between turns too, as the page's
    /// foot offers it. Null for any other.
    /// </summary>
    public string? LandableTree(SessionRecord record)
    {
        // A partner's record never lands; its piece reaches the line through its lead alone.
        if ((record.Live && !_look.Between.Contains(record.Id)) || record.Teammate || record.Partner || record.Tree is not { } tree) return null;
        var key = SessionGroups.Normal(tree);
        return _newestOnTree.TryGetValue(key, out var newest) && newest == record.Id && !_liveTrees.Contains(key) ? tree : null;
    }

    /// <summary>
    /// The tree whose work a run waiting on the person shows: its landable tree, its quest not parked and not going back into
    /// it. Null for any other.
    /// </summary>
    private string? ReviewableTree(SessionRecord record)
    {
        if (LandableTree(record) is not { } tree) return null;

        // A quest the planner still considers while it is taken goes back into this session's tree: its carry-on or its resume,
        // whatever holds that start for now. Bar the person's stop, a pause or a finished session's take, until they release it.
        return VerdictOnLast(record) is { } verdict
               && verdict.Verdict is not (StartVerdict.Stopped or StartVerdict.Paused or StartVerdict.Unclosed)
               && (verdict.Verdict == StartVerdict.Exhausted || verdict.Quest.Status == "Taken" || verdict.CarriesWork is > 0)
            ? null
            : tree;
    }

    /// <summary>
    /// Whether the person's words wait on this record for it to go on with: the record its quest's words
    /// go on in (<see cref="WordsOn"/>).
    /// </summary>
    private bool WordsWait(SessionRecord record) =>
        record.Quest is { } quest && string.Equals(WordsOn(quest)?.Session, record.Id, StringComparison.Ordinal);

    /// <summary>
    /// The record the person's words wait on for this quest, the planner's <c>WordsOn</c> read again: with
    /// words kept on it, not every one a word it is already found unable to go on with (its marks). The session the quest's
    /// reopen names first, though a later one is its last here, else its last session here; where the planner
    /// started the words' go-on, the record it names. The person's stop of the last session holds the named record's words,
    /// so they are listed on it, held by that verdict.
    /// </summary>
    private PriorSession? WordsOn(string quest)
    {
        if (_verdicts.TryGetValue(quest, out var verdict) && verdict is { GoesOn: true, Resumes: { } resumes }) return resumes;
        if (_look.NamedByReopen.TryGetValue(quest, out var named) && Waits(named)) return named;
        return _look.LastRun.TryGetValue(quest, out var last) && Waits(last) ? last : null;

        // The planner's hold, in this copy's own right.
        bool Waits(PriorSession record) =>
            record.WordsWaiting && !GoOnMarks.Judged(record, _look.Unable.GetValueOrDefault(record.Session)) && !_look.SupervisorHolds(record);
    }

    /// <summary>The planner's verdict on this session's quest, where its words go on in this session (<see cref="WordsOn"/>) or it is that quest's last here.</summary>
    private Consideration? VerdictOn(SessionRecord record) =>
        VerdictOnLast(record)
        ?? (WordsWait(record) && _verdicts.TryGetValue(record.Quest!, out var verdict) ? verdict : null);

    /// <summary>The look's wait holding this quest's start, on a cool-off or on signed-out accounts, or null.</summary>
    private AccountWait? WaitOn(string quest) =>
        _look.Waits.FirstOrDefault(wait => wait.Quests.Contains(quest, StringComparer.OrdinalIgnoreCase));

    /// <summary>The planner's verdict on this session's quest, where this session is that quest's last here; null otherwise.</summary>
    private Consideration? VerdictOnLast(SessionRecord record) =>
        record.Quest is { } quest
        && _look.LastRun.TryGetValue(quest, out var last)
        && string.Equals(last.Session, record.Id, StringComparison.Ordinal)
        && _verdicts.TryGetValue(quest, out var verdict)
            ? verdict
            : null;
}
