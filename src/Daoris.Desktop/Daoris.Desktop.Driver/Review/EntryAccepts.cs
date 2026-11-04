namespace Daoris.Driver;

/// <summary>
/// What an entry's one Accept and an ask's do (ACCEPTALL1, D167's 2026-10-15 amendment), in the library so the page's
/// <c>LAND_ENTRY</c> and <c>LAND_ASK</c> and the terminal's <c>trees land --entry|--ask</c> are one code (ACCEPTALL1e, D50's twin
/// rule): the entries asked about, the parts of each that have work to land, each part's gate counted as its own press counts it,
/// and the word each part's review keeps. A part lands through its run's <c>land</c> node at the runtime's pass; a part no
/// <c>land</c> node performs is held.
/// </summary>
/// <param name="service">The service, for the sessions' records, their quests and the registry.</param>
/// <param name="home">The home the trees, the landings and the opinions are kept under.</param>
/// <param name="trees">The session trees the parts are read through, over the surface's landing plugins.</param>
/// <param name="events">The conversations' records, which name a part's work.</param>
public sealed class EntryAccepts(ServiceClient service, string home, SessionTrees trees, IWorkplaceEventHistory events)
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
    /// The press on one entry: each part's gate counted as its own press counts it, then the review's word kept for each part whose
    /// run's <c>land</c> the runtime performs. A part no <c>land</c> node performs is held, said.
    /// </summary>
    public async Task<EntryLanding> LandAsync(
        SessionEntry entry, IReadOnlyList<IWorkplaceSessionRow> rows, IReadOnlySet<string> leaveOut, CancellationToken cancellationToken)
    {
        var (_, parts) = await PartsAsync(entry, rows, cancellationToken).ConfigureAwait(false);
        if (parts.Count == 0) return new EntryLanding(false, "Nothing was landed: no part of this entry has work to land on this machine.", null, []);
        var presses = new OpinionPresses(home, service);
        var pressed = new List<EntryLandingPart>(parts.Count);
        foreach (var part in parts)
        {
            var quest = entry.Parts.First(each => each.Session == part.Session).Quest;
            pressed.Add(part with { Gate = await presses.PressedAsync(part.Session, (LandingGate)part.Gate!, null, part.Tree, quest, cancellationToken).ConfigureAwait(false) });
        }

        var steps = EntryLandings.Sequence(pressed, leaveOut);
        var performing = pressed.Where((each, at) => steps[at].Lands && each.Gate?.Process is { LandsByRuntime: true }).ToList();
        var accepted = await AcceptPerformedAsync(performing, pressed.Except(performing).ToList(), cancellationToken).ConfigureAwait(false);
        var outcomes = new List<EntryPartLanding>(pressed.Count);
        for (var at = 0; at < pressed.Count; at++)
        {
            var part = pressed[at];
            if (performing.Contains(part)) outcomes.Add(accepted.First(each => each.Session == part.Session));
            else if (steps[at].LeftOut) outcomes.Add(new EntryPartLanding(part.Session, EntryPartOutcome.LeftOut));
            else if (steps[at].SameTreeAs is not null) outcomes.Add(new EntryPartLanding(part.Session, EntryPartOutcome.SameTree));
            else
            {
                var why = part.Gate?.Refusal?.Message ?? "no saved workflow's `land` node performs its landing, so it is held.";
                outcomes.Add(new EntryPartLanding(part.Session, EntryPartOutcome.Refused, new TreeLanding(false, $"Nothing was landed: {why}")
                {
                    Refusal = AutoLandingCode.Refused,
                }));
            }
        }

        return new EntryLanding(
            accepted.Count > 0 && outcomes.All(each => each.Outcome is not EntryPartOutcome.Refused)
                && accepted.All(part => part.Outcome is EntryPartOutcome.Landed or EntryPartOutcome.BroughtInto),
            accepted.Any(part => part.Outcome == EntryPartOutcome.Accepted) ? "Accepted; Daoris lands each part at its next pass."
                : outcomes.Any(each => each.Outcome == EntryPartOutcome.Refused) ? "Nothing more was landed: see each part."
                : "Landed.", null, outcomes);
    }

    /// <summary>
    /// Waits for what the performer's landing of a part came to: asks the loop for a whole pass, then reads the outcome within the bound
    /// (<see cref="LandOutcomes"/>). Null where the surface has no loop (the terminal alone), or the landing has not ended by the bound.
    /// </summary>
    public Func<string, string, string, CancellationToken, Task<TreeLanding?>>? Performs { get; init; }

    /// <summary>
    /// The parts whose run's <c>land</c> the runtime performs, accepted (WORKFLOWCLEAN1 1c; the cleanup design §2.2, §13 point 3): each head's
    /// review word carries the operation bringing sessions together it delivers, the performer delivers the head's unit, and a member brought into
    /// it takes no word of its own. A part the landing came to is said as the landing says it; else it is accepted, to land at the pass.
    /// </summary>
    private async Task<List<EntryPartLanding>> AcceptPerformedAsync(
        IReadOnlyList<EntryLandingPart> performing, IReadOnlyList<EntryLandingPart> others, CancellationToken ct)
    {
        var accepted = new List<EntryPartLanding>();
        if (performing.Count == 0) return accepted;
        TogetherOperation? operation = null;
        try
        {
            operation = new TogetherOperations(home).All().FirstOrDefault(each => each.IsOpen
                && performing.Concat(others).Any(part => TogetherOperation.SameTree(each.Head.Tree, part.Tree)));
        }
        catch (IOException)
        {
            // A store that does not read binds nothing here: the landing's own guard refuses then.
        }

        foreach (var part in performing)
        {
            if (operation is not null && operation.Members.Any(member => TogetherOperation.SameTree(member.Tree, part.Tree)))
            {
                accepted.Add(new EntryPartLanding(part.Session, EntryPartOutcome.BroughtInto,
                    new TreeLanding(false, $"Brought into operation {operation.Id}: its head's landing carries it.")));
                continue;
            }

            var heads = operation is not null && TogetherOperation.SameTree(operation.Head.Tree, part.Tree);
            var word = WorkplaceWorkflows.Current.KeepReviewWord(
                home, null, part.Gate!.Process!, DateTimeOffset.UtcNow,
                heads ? $"brought together with {string.Join(", ", operation!.Members.Select(each => each.Session))}, operation {operation.Id}" : null);
            var tip = ((LandingGate)part.Gate).Tip ?? ((LandingGate)part.Gate).GraphPart()?.Commit;
            var outcome = word.Kept && word.Unit is { } unit && tip is not null && Performs is { } waits
                ? await waits(part.Session, unit, tip, ct).ConfigureAwait(false) : null;
            accepted.Add(outcome is null
                ? new EntryPartLanding(part.Session, EntryPartOutcome.Accepted,
                    new TreeLanding(false, word.Said ?? "Accepted; Daoris lands it at its next pass."))
                : new EntryPartLanding(part.Session, outcome.Landed ? EntryPartOutcome.Landed : EntryPartOutcome.Refused, outcome));
        }

        return accepted;
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
            subject = await trees.EntryForWorkAsync(subject, leadTree, parts[0].Tree, cancellationToken, parts[0].Gate?.Process).ConfigureAwait(false);
        }

        return (subject, parts);
    }
}
