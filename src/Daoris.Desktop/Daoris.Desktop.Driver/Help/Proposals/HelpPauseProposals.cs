namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>pause</c> proposal (PAUSE1f-b, D132 §7.4, design §2): an ask's work, or one quest's, paused on this machine
/// or resumed. Its doors are <c>pause</c> and <c>resume</c>, its target the one ask's or quest's id, as the service's box writes
/// it. It is judged by what a pause would do with each piece of the work (<see cref="WorkPausing.Plan"/>) and the pauses
/// <c>driver.json</c> keeps, and applied through the pause and the resume the pages' *Pause…* and *Resume* press.
/// </summary>
/// <remarks>
/// <para><b>The card is the pause's own ask</b> (§2.6): how many running sessions it stops, in the sentence *Pause ask* says,
/// then where they apply that another machine may still take an open quest of a workspace this machine syncs, and that a
/// running intake keeps reading. Then what §2.1 leaves as it is: a session waiting on the person, a teammate's, and a take no
/// session here worked. Nothing is stopped until Apply (D89), and the door judges the work again as it goes.</para>
///
/// <para><b>What changes nothing is said, never refused</b> (D48 §6): a pause of what is paused already with nothing of it
/// running, a pause of work with nothing open or taken here, and a resume of what is not paused. Such a card's Apply calls no
/// door. A pause of what is paused already where something of it runs now stops that, as the terminal's second pause does.
/// A resume names each other pause over a quest of the work, which it does not lift (§2.4).</para>
///
/// <para><b>Abandon is never proposed</b> (§7.4): it declines quests with the person's reason, which is their answer (D37).
/// The box writes the id and, since PAUSE1f-c, whether it is an ask's or a quest's (<c>value</c>); a file without that word
/// is found among this machine's works and pauses, and an id both an ask and a quest have is refused there.</para>
/// </remarks>
internal sealed class HelpPauseProposals : IHelpProposalKind
{
    public string Kind => "pause";

    // PAUSE1f-b: pause and resume; abandon is the person's own (D132 §7.4).
    public string Tool => "pause_propose";

    public IReadOnlyList<string> Doors { get; } = ["pause", "resume"];

    /// <summary>
    /// ASKSCOPE1c2: the ask or the quest it names by id, filed under the workspace its work's record says: the ask as the service
    /// answered it, or the quest named. A pause whose work has gone names none.
    /// </summary>
    public HelpNames Named(HelpProposal proposal, HelpMachineFacts facts)
    {
        var id = proposal.Target?.Trim().TrimStart('#').Trim() ?? "";
        var records = new List<(string Said, string? Workspace)>();
        foreach (var scope in Kinds(proposal))
        {
            var work = WorkOf(facts, scope, id);
            if (scope == WorkScope.Ask && work?.Ask is { } ask) records.Add(HelpNames.Ask(ask.Id, ask.Workspace));
            if (scope == WorkScope.Quest && work?.Pieces.Quests.FirstOrDefault(quest => quest.Joined == WorkJoin.Named) is { } named)
            {
                records.Add(HelpNames.Quest(named.Quest.Id, named.Quest.Workspace));
            }
        }

        return new([proposal.Workspace], [proposal.Repository]) { Records = records };
    }

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        static HelpPlan Refused(string why) => new(why, "", "", null);
        if (proposal.Door is not ("pause" or "resume"))
        {
            return Refused($"`{proposal.Door}` is not a pause's change — `pause` or `resume`; " + (proposal.Door == "abandon"
                ? "abandoning is the person's own, on the ask's or the quest's page, where a go takes them: its reason is their "
                  + "answer, which Ask Daoris never writes."
                : "nothing else is proposed of an ask's or a quest's work."));
        }

        var id = proposal.Target?.Trim().TrimStart('#').Trim() ?? "";
        if (id.Length == 0) return Refused("a pause proposal names one ask or one quest, by its id.");

        var pause = proposal.Door == "pause";
        // A resume also finds a pause whose ask or quest has gone: the pause is this machine's, and only Resume takes it away.
        var kinds = Kinds(proposal);
        var found = kinds
            .Select(scope => (Scope: scope, Work: WorkOf(facts, scope, id)))
            .Where(each => each.Work is not null || (!pause && WorkPausing.Entry(config, each.Scope, id) is not null))
            .ToList();
        if (found.Count == 0)
        {
            return Refused($"there is no {(kinds.Length == 1 ? WorkPausing.Word(kinds[0]) : "ask or quest")} `#{id}` on this machine.");
        }
        if (found.Count > 1)
        {
            return Refused($"`#{id}` names both an ask and a quest on this machine, and a pause proposal does not say which: the "
                + "person pauses it on its page.");
        }

        var (scope, work) = found[0];
        var named = new PausedBy(scope, Spelled(scope, id, work, config));
        return pause ? PausePlan(named, work!, config, facts) : ResumePlan(named, work, config);
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var plan = applying.Plan;
        if (plan.Pause is not { } work)
        {
            return applying.Settled(true, $"Applied: `#{applying.Id}` — nothing changed: {plan.Describe}", null);
        }

        var (ok, message) = applying.Proposal.Door == "pause"
            ? await applying.Doors.PauseWorkAsync(work.Scope, work.Id, ct).ConfigureAwait(false)
            : await applying.Doors.ResumeWorkAsync(work.Scope, work.Id, ct).ConfigureAwait(false);
        return applying.Settled(ok, $"{(ok ? "Applied" : "Not applied")}: `#{applying.Id}` (`{plan.Terminal}`) — {message}", message);
    }

    /// <summary>
    /// §2.6: what the pause stops and starts nothing of, what it adds where it applies, then what §2.1 leaves as it is. What
    /// <see cref="WorkPausing.PauseAsync"/> answers <see cref="PauseVerdict.Nothing"/> for, or finds paused with nothing to
    /// stop, changes nothing.
    /// </summary>
    private static HelpPlan PausePlan(PausedBy work, WorkPlan plan, DriverConfig config, HelpMachineFacts facts)
    {
        var what = What(work);
        var terminal = work.Scope == WorkScope.Ask ? $"daoris-driver ask --pause {work.Id}" : $"daoris-driver quest pause {work.Id}";
        var already = WorkPausing.Entry(config, work.Scope, work.Id) is not null;
        if (!already && !plan.Pausable)
        {
            return new HelpPlan(null, $"Nothing of {what} is open or taken on this machine, so there is nothing to pause: applying "
                + "changes nothing.", terminal, null);
        }

        var stops = plan.Sessions.Count(session => session.Pause == PauseAct.Stopped);
        if (already && stops == 0)
        {
            return new HelpPlan(null, $"{Capital(what)} is paused already, so applying changes nothing: nothing of it starts on this "
                + "machine until you resume it.", terminal, null);
        }

        var kept = stops == 1
            ? "Its tree keeps what it wrote, and a quest it took stays taken here."
            : "Their trees keep what they wrote, and the quests they took stay taken here.";
        var said = new List<string>
        {
            already ? $"{Capital(what)} is paused already, and {Count(stops, "session of it runs", "sessions of it run")} now: applying "
                      + $"stops {(stops == 1 ? "it" : "them")}. {kept}"
            : stops == 0 ? $"Starts nothing of {what} on this machine until you resume it; nothing of it runs now, so nothing is stopped."
            : $"Stops {Count(stops, "running session", "running sessions")} now and starts nothing of {what} on this machine until "
              + $"you resume it. {kept}",
        };

        // §2.6's two sentences, where they apply: an open quest another machine may take (§5.1), and a running intake.
        var open = plan.Quests
            .Where(quest => quest is { Pause: PauseAct.Paused, Quest.Quest.Status: "Open" } && Wired(facts, quest.Quest.Quest.Workspace))
            .Select(quest => quest.Quest.Quest.Id)
            .ToList();
        if (open.Count > 0) said.Add($"Another machine may still take {Quests(open)}: a pause holds this machine only.");
        if (plan.Sessions.Any(session => session.Pause == PauseAct.Intake)) said.Add("Its intake keeps reading; what it publishes waits too.");

        // §2.1: what the pause leaves as it is, each with why.
        var parked = Sessions(plan, PauseAct.Parked);
        if (parked.Count > 0)
        {
            said.Add(parked.Count == 1
                ? $"{Listed(parked)} is left waiting on you: your answer is kept, and what it starts waits for Resume."
                : $"{Listed(parked)} are left waiting on you: your answers are kept, and what they start waits for Resume.");
        }

        var teammates = Sessions(plan, PauseAct.Teammate);
        if (teammates.Count > 0)
        {
            said.Add(teammates.Count == 1
                ? $"{Listed(teammates)} runs on `{teammates[0][..teammates[0].IndexOf('/')]}`, a teammate's machine, and nothing this "
                  + "machine sends reaches it."
                : $"{Listed(teammates)} run on teammates' machines, and nothing this machine sends reaches them.");
        }

        var elsewhere = plan.Quests.Where(quest => quest.Pause == PauseAct.Elsewhere).Select(quest => quest.Quest.Quest.Id).ToList();
        if (elsewhere.Count > 0)
        {
            said.Add(elsewhere.Count == 1
                ? $"{Quests(elsewhere)} is taken with no session of this machine's on it, so a pause here does not reach it."
                : $"{Quests(elsewhere)} are taken with no session of this machine's on them, so a pause here does not reach them.");
        }

        return new HelpPlan(null, string.Join(' ', said), terminal, null) { Pause = work };
    }

    /// <summary>
    /// §2.4: the pause lifted and each stop it made released; it starts nothing itself. Each other pause over a quest of the work
    /// stays, named by what holds it, as the resume's answer names it after; a resume of what is not paused changes nothing.
    /// </summary>
    private static HelpPlan ResumePlan(PausedBy work, WorkPlan? plan, DriverConfig config)
    {
        var what = What(work);
        if (WorkPausing.Entry(config, work.Scope, work.Id) is not { } entry)
        {
            return new HelpPlan(null, $"{Capital(what)} is not paused on this machine, so applying changes nothing.", work.Door, null);
        }

        var said = new List<string>
        {
            $"Resume {what} on this machine: its pause is lifted, and the driver's next look plans its work as it would have, a "
            + "taken quest carried on in its tree.",
        };
        var released = entry.Stopped.OrderBy(stop => stop.Key, StringComparer.Ordinal).Select(stop => $"`{stop.Value}` on `#{stop.Key}`").ToList();
        if (released.Count == 1) said.Add($"It releases the pause's stop of {released[0]}.");
        else if (released.Count > 1) said.Add($"It releases the pause's {released.Count} stops: {string.Join(", ", released)}.");

        var stays = new List<(string Quest, PausedBy By)>();
        foreach (var quest in plan?.Quests ?? [])
        {
            if (quest.PausedBy is { } by && !Same(by, work)) stays.Add((quest.Quest.Quest.Id, by));
        }

        // A quest's own pause is said before its ask's (PausedWork), so the ask that asked it, paused too, is read from the file.
        var asked = plan?.Pieces.Quests.FirstOrDefault(quest => quest.Joined == WorkJoin.Named)?.Quest;
        if (work.Scope == WorkScope.Quest && asked is not null && !stays.Any(stay => stay.Quest == asked.Id))
        {
            var asker = config.PausedAsks.Keys.FirstOrDefault(ask => string.Equals(asked.From, AskWork.SenderOf(ask), StringComparison.OrdinalIgnoreCase));
            if (asker is not null) stays.Add((asked.Id, new PausedBy(WorkScope.Ask, asker)));
        }

        said.AddRange(stays.Select(stay => stay.By.Scope == WorkScope.Quest && string.Equals(stay.By.Id, stay.Quest, StringComparison.OrdinalIgnoreCase)
            ? $"`#{stay.Quest}` stays paused on its own: `{stay.By.Door}` resumes it."
            : $"`#{stay.Quest}` stays paused with {What(stay.By)}: `{stay.By.Door}` resumes it."));
        return new HelpPlan(null, string.Join(' ', said), work.Door, null) { Pause = work };
    }

    /// <summary>
    /// Which it names: the box says (PAUSE1f-c); a file from before it, or a word that is neither, is left to the lookup, as an
    /// ask's or a quest's.
    /// </summary>
    private static WorkScope[] Kinds(HelpProposal proposal) => proposal.Value?.Trim().ToLowerInvariant() switch
    {
        "ask" => [WorkScope.Ask],
        "quest" => [WorkScope.Quest],
        _ => [WorkScope.Ask, WorkScope.Quest],
    };

    /// <summary>The work of this scope and id among the facts, matched in any case, or null where this machine has none.</summary>
    private static WorkPlan? WorkOf(HelpMachineFacts facts, WorkScope scope, string id) =>
        facts.Work.FirstOrDefault(plan => plan.Pieces.Scope == scope && string.Equals(plan.Pieces.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The id as this machine's records spell it: the ask's or the named quest's, else the pause's in <c>driver.json</c>.</summary>
    private static string Spelled(WorkScope scope, string id, WorkPlan? work, DriverConfig config) => work switch
    {
        null => (scope == WorkScope.Ask ? config.PausedAsks : config.PausedQuests).Keys
            .FirstOrDefault(key => string.Equals(key, id, StringComparison.OrdinalIgnoreCase)) ?? id,
        _ when scope == WorkScope.Ask => work.Ask?.Id ?? work.Pieces.Id,
        _ => work.Pieces.Quests.FirstOrDefault(quest => quest.Joined == WorkJoin.Named)?.Quest.Id ?? work.Pieces.Id,
    };

    /// <summary>Whether this machine syncs the quest's workspace with a remote, an unstated one being the default (§5.1).</summary>
    private static bool Wired(HelpMachineFacts facts, string? workspace) =>
        facts.Wired.Contains(string.IsNullOrWhiteSpace(workspace) ? RemoteTarget.DefaultWorkspace : workspace.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool Same(PausedBy one, PausedBy other) =>
        one.Scope == other.Scope && string.Equals(one.Id, other.Id, StringComparison.OrdinalIgnoreCase);

    private static List<string> Sessions(WorkPlan plan, string act) =>
        [.. plan.Sessions.Where(session => session.Pause == act).Select(session => session.Session.Record.Id)];

    private static string What(PausedBy work) => $"{work.Word} `#{work.Id}`";

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    private static string Listed(IEnumerable<string> sessions) => string.Join(", ", sessions.Select(session => $"`{session}`"));

    private static string Quests(IEnumerable<string> quests) => string.Join(", ", quests.Select(quest => $"`#{quest}`"));
}

public interface IHelpPauseDoors
{
    /// <summary>
    /// The pause the pages' *Pause…* and a session's *Pause ask…* and *Pause quest…* press (<c>WORK_PAUSE</c>, PAUSE1b, D132
    /// §2.1): <see cref="WorkPausing.PauseAsync"/>, which reads the work again as it goes. Its answer comes back as its sentence,
    /// a refusal included.
    /// </summary>
    Task<(bool Ok, string Message)> PauseWorkAsync(WorkScope scope, string id, CancellationToken ct);

    /// <summary>
    /// The resume the pages' *Resume* and a session's *Resume ask* and *Resume quest* press (<c>WORK_RESUME</c>, PAUSE1b, D132
    /// §2.4): <see cref="WorkPausing.ResumeAsync"/>, its answer naming what still holds each quest, as its sentence.
    /// </summary>
    Task<(bool Ok, string Message)> ResumeWorkAsync(WorkScope scope, string id, CancellationToken ct);
}
