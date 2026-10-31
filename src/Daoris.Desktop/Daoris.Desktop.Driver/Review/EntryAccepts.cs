namespace Daoris.Driver;

/// <summary>
/// What an entry's one Accept and an ask's do around the landing itself (ACCEPTALL1, D167's 2026-10-15 amendment), in the library so
/// the page's <c>LAND_ENTRY</c> and <c>LAND_ASK</c> and the terminal's <c>trees land --entry|--ask</c> are one code (ACCEPTALL1e,
/// D50's twin rule): the entries asked about, the parts of each that have work to land, each part's gate counted as its own press
/// counts it, the landing, then what each landed part's conversation keeps of it and what a held gate writes to the machine log.
/// </summary>
/// <remarks>
/// Nothing of a surface is here: where a plugin's lines and a part's kept words are said, which sessions are in use, and which
/// door a held gate is logged at are each the surface's to hand in.
/// </remarks>
/// <param name="service">The service, for the sessions' records, their quests and the registry.</param>
/// <param name="home">The home the trees, the landings and the opinions are kept under.</param>
/// <param name="trees">The session trees the parts land through, over the surface's landing plugins.</param>
/// <param name="events">The conversations' records, where each landed part's note is kept.</param>
/// <param name="say">Where a kept note's line is said, for a session: the page's conversation, or the terminal's standard error.</param>
/// <param name="inUse">The trees a session still running or waiting holds, which the rule's tidy leaves.</param>
/// <param name="log">The machine log a held gate writes to; none where the surface keeps none.</param>
/// <param name="door">The door a held gate is logged at (<see cref="ReviewDoors"/>).</param>
public sealed class EntryAccepts(
    ServiceClient service, string home, SessionTrees trees, IWorkplaceEventHistory events, Action<string, string> say,
    Func<CancellationToken, Task<IReadOnlySet<string>>> inUse, MachineLog? log, string door)
{
    /// <summary>The parts of <paramref name="entry"/> whose tree offers work to land, in the entry's order: what its Accept lands.</summary>
    public static IReadOnlyList<EntryPart> PartsOf(SessionEntry entry, IReadOnlyList<IWorkplaceSessionRow> rows)
    {
        var offering = rows.Where(row => row.Lands is not null).Select(row => row.Session).ToHashSet(StringComparer.Ordinal);
        return [.. entry.Parts.Where(part => offering.Contains(part.Session))];
    }

    /// <summary>
    /// The part a session's own Accept is named for (ACCEPTALL1b, D167's 2026-10-15 amendment point 2): its entry's first, the records
    /// sharing its key oldest first by <c>created</c> then id, as <see cref="SessionEntries.Read"/> orders an entry's parts. Null where
    /// the session has no record here or is its entry's only part: it lands as before.
    /// </summary>
    public static SessionRecord? EntryLead(IReadOnlyList<SessionRecord> records, string session) => EntryLandings.LeadOf(records, session);

    /// <summary>An ask's own entries (point 4): one per repository, by its name, never its intake.</summary>
    public static IReadOnlyList<SessionEntry> EntriesOf(string ask, IReadOnlyList<SessionEntry> entries) =>
        [.. entries.Where(entry => entry.Origin == $"ask:{ask}" && entry.Repository != "intake")
            .OrderBy(entry => entry.Repository, StringComparer.Ordinal)];

    /// <summary>
    /// Where the entry's parts would land and what holds each: the plan, and each part's gate sentence by session (what holds it,
    /// absent where nothing does). An entry with no part to land has the empty plan.
    /// </summary>
    public async Task<(EntryPlan Plan, IReadOnlyDictionary<string, string> Held)> PlanAsync(
        SessionEntry entry, IReadOnlyList<IWorkplaceSessionRow> rows, CancellationToken cancellationToken)
    {
        var (subject, parts) = await PartsAsync(entry, rows, cancellationToken).ConfigureAwait(false);
        if (parts.Count == 0) return (new EntryPlan(LandingForm.Branch, "", []), new Dictionary<string, string>());
        var plan = await trees.PlanEntryAsync(parts, subject, cancellationToken).ConfigureAwait(false);
        var held = parts.Where(part => part.Gate?.Refusal is not null)
            .ToDictionary(part => part.Session, part => part.Gate!.Refusal!.Message, StringComparer.Ordinal);
        return (plan, held);
    }

    /// <summary>
    /// The press on one entry: each part's gate counted as its own press counts it, the landing, then what each landed part's
    /// conversation keeps of it, and each left-out part's (ACCEPTALL1c), and what a held gate writes to the machine log, as
    /// <c>LAND_SESSION_TREE</c> does for one.
    /// </summary>
    public async Task<EntryLanding> LandAsync(
        SessionEntry entry, IReadOnlyList<IWorkplaceSessionRow> rows, IReadOnlySet<string> leaveOut, CancellationToken cancellationToken)
    {
        var (subject, parts) = await PartsAsync(entry, rows, cancellationToken).ConfigureAwait(false);
        if (parts.Count == 0) return new EntryLanding(false, "Nothing was landed: no part of this entry has work to land on this machine.", null, []);
        var presses = new OpinionPresses(home, service);
        var pressed = new List<EntryLandingPart>(parts.Count);
        foreach (var part in parts)
        {
            var quest = entry.Parts.First(each => each.Session == part.Session).Quest;
            pressed.Add(part with { Gate = await presses.PressedAsync(part.Session, (LandingGate)part.Gate!, null, part.Tree, quest, cancellationToken).ConfigureAwait(false) });
        }

        var together = await TogetherAsync(pressed, presses, cancellationToken).ConfigureAwait(false);
        var landed = await trees.LandEntryAsync(pressed, subject, leaveOut, cancellationToken, inUse, together).ConfigureAwait(false);
        // ACCEPTALL1c (D167's amendment point 3): a part the person left out is recorded with the landing, on its conversation, so its
        // review says so and leads with Discard; only where the entry's work landed somewhere.
        var landedAny = landed.Parts.Any(part => part.Outcome == EntryPartOutcome.Landed);
        foreach (var part in landed.Parts.Where(part => part.Outcome == EntryPartOutcome.LeftOut && landedAny && landed.Branch is not null))
        {
            events.Keep(part.Session, LandingRules.LeftOutNote(landed.Branch!), line => say(part.Session, line));
        }

        foreach (var part in landed.Parts)
        {
            if (part.Landing is not { } its) continue;
            if (its.Landed) events.Keep(part.Session, LandingRules.Note(its), line => say(part.Session, line));
            var gate = pressed.First(each => each.Session == part.Session);
            if (log is not null && gate.Gate!.Held(part.Session, trees.Owner(gate.Tree), its, door) is { } held)
            {
                SessionLog.WriteLanding(log, held);
            }
        }

        return landed;
    }

    /// <summary>
    /// The operation bringing sessions together bound to the entry's head (LINESTOGETHER1d), with each member's gate read as its own
    /// Accept reads it; null where none binds it. A store that does not read binds nothing here: the landing's own guard refuses then.
    /// </summary>
    private async Task<EntryTogether?> TogetherAsync(IReadOnlyList<EntryLandingPart> parts, OpinionPresses presses, CancellationToken ct)
    {
        TogetherOperation? operation;
        try
        {
            operation = new TogetherOperations(home).All().FirstOrDefault(each => each.IsOpen
                && parts.Any(part => TogetherOperation.SameTree(each.Head.Tree, part.Tree)));
        }
        catch (IOException)
        {
            return null;
        }

        if (operation is null) return null;
        var head = parts.First(part => TogetherOperation.SameTree(operation.Head.Tree, part.Tree)).Session;
        var gates = new Dictionary<string, IWorkplaceGate?>(StringComparer.Ordinal);
        foreach (var member in operation.Members)
        {
            var gate = await trees.GateAsync(member.Tree, member.Quest, new ServiceReviewWorld(service), member.Session, ct).ConfigureAwait(false);
            gates[member.Session] = await presses.PressedAsync(member.Session, (LandingGate)gate, null, member.Tree, member.Quest, ct).ConfigureAwait(false);
        }

        return new EntryTogether(operation.Id, head, gates, operation.Members.Select(each => each.Session).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// The entry's parts with work, each with its tree here and its gate, and the subject the entry is named for: its first part's,
    /// the chain's first quest walked up as a chain's is (WSR5), so every part lands on the branch named for it.
    /// </summary>
    private async Task<(LandingSubject Subject, IReadOnlyList<EntryLandingPart> Parts)> PartsAsync(
        SessionEntry entry, IReadOnlyList<IWorkplaceSessionRow> rows, CancellationToken cancellationToken)
    {
        var parts = new List<EntryLandingPart>();
        // FABRIC-CLOSE1h3: the quests the names are read from, each read once; each part's own quest is the result its press is made on.
        var quests = new PressQuests(service.FindQuestAsync);
        foreach (var part in PartsOf(entry, rows))
        {
            var (tree, _) = await service.SessionGroundAsync(part.Session, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tree)) continue;
            var gate = await trees.GateAsync(tree, part.Quest, new ServiceReviewWorld(service), part.Session, cancellationToken).ConfigureAwait(false);
            // TREEFOLLOWNAME1: its own words, which name its landing where its tree is on a branch of its own.
            var own = await LandingRules.SubjectAsync(part.Session, part.Quest, quest => quests.FindAsync(quest, cancellationToken),
                events.Openings([part.Session]).GetValueOrDefault(part.Session)).ConfigureAwait(false);
            // FABRIC-UNITLINK1, FABRIC-CLOSE1h13: the quest the part served and its submission as read here, which its landing keeps.
            if (part.Quest is { } served && await quests.FindAsync(served, cancellationToken).ConfigureAwait(false) is { } read)
            {
                own = own with
                {
                    Served = read.Id,
                    Submission = read.Submission is { } made ? new QuestSubmissionRef(made.Machine, made.Sequence) : null,
                };
            }
            // Judged again under its own tree's hold whether or not that hold waits: the press's fetch and its earlier parts come first.
            parts.Add(new EntryLandingPart(part.Session, tree)
            {
                Gate = gate, Own = own, Pressed = await quests.PressedAsync(part.Session, part.Quest, late: true, cancellationToken).ConfigureAwait(false),
            });
        }

        var lead = entry.Parts.Count > 0 ? entry.Parts[0] : null;
        var subject = lead is null ? new LandingSubject(entry.Key, null, null)
            : await LandingRules.SubjectAsync(lead.Session, lead.Quest, quest => quests.FindAsync(quest, cancellationToken),
                events.Openings([lead.Session]).GetValueOrDefault(lead.Session)).ConfigureAwait(false);
        // ACCEPTFOOT2: a conversation's entry is named for its first part's work, as each part's own Accept names it.
        if (lead is not null && parts.Count > 0)
        {
            var leadTree = parts.FirstOrDefault(part => part.Session == lead.Session)?.Tree;
            if (leadTree is null) (leadTree, _) = await service.SessionGroundAsync(lead.Session, cancellationToken).ConfigureAwait(false);
            subject = await trees.EntryForWorkAsync(subject, leadTree, parts[0].Tree, cancellationToken).ConfigureAwait(false);
        }

        return (subject, parts);
    }
}
