namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>session</c> proposal (SESSUX1h2b, D126 §5.2, §5.3, §7.3): sessions archived on this machine or unarchived,
/// by id, or every session that ended archived. Its doors are <c>archive</c> and <c>unarchive</c>, its target the ids
/// comma-joined, or none and the value <c>ended</c> (with <c>archive</c> only), as the service's box writes it. It is judged by
/// where Sessions' list places each session, as <see cref="SessionArchive"/> judges an archive, and applied through Sessions'
/// own archive, the door its *Archive*, *Archive what ended…* and *Unarchive* press.
/// </summary>
/// <remarks>
/// <para><b>Archive never hides what needs the person</b> (§5.2): a live session, one waiting on you, one to review and an id no
/// record here has are refused, each in the words the list's own refusal uses. A card never says it archives what its Apply
/// would keep, so one such among several refuses the card, naming each; the helper proposes the rest without them. A
/// teammate's ended session may be archived, since the mark is this machine's and their record is not changed.</para>
///
/// <para><b>Ask Daoris's own conversation is refused</b> at both doors: Ask Daoris → History keeps it, and names, pins and
/// deletes it there (ASKHIST1), as a go to Sessions refuses it (ENTRY1f2). Archive what ended takes what Sessions' *Archive what
/// ended…* takes, every ended row not archived, so the card's count is the list's.</para>
///
/// <para><b>What changes nothing is said, never refused</b> (D48 §6): an unarchive of a session that is not archived, an
/// archive of one archived already, and archive what ended with nothing ended. Such a card's Apply calls no door.</para>
///
/// <para><b>Listed first, then pressed</b> (§5.3): the card says what archive what ended would take and what stays in the list.
/// Its Apply is judged again with the machine as it stands then, and the door judges each session again as it goes. A stop is
/// no door: a session's stop is its own conversation, which a go reaches (D161's SESSUX1h1 note).</para>
/// </remarks>
internal sealed class HelpSessionProposals : IHelpProposalKind
{
    /// <summary>The value that asks for every ended session, as the service's box writes it and <c>sessions archive --ended</c> spells it.</summary>
    public const string Ended = "ended";

    public string Kind => "session";

    // SESSUX1h2b: archive and unarchive; a delete is the `delete` kind's `session` door, and a stop is no door (SESSUX1h1).
    public string Tool => "session_propose";

    public IReadOnlyList<string> Doors { get; } = ["archive", "unarchive"];

    /// <summary>
    /// ASKSCOPE1c2: each session it names by id, filed under the workspace its record says. Archive what ended names none by id,
    /// and takes every ended row Sessions' list places, as before.
    /// </summary>
    public HelpNames Named(HelpProposal proposal, HelpMachineFacts facts) =>
        new([proposal.Workspace], [proposal.Repository]) { Records = [.. Ids(proposal).SelectMany(id => HelpNames.OfSession(facts, id))] };

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        static HelpPlan Refused(string why) => new(why, "", "", null);
        if (proposal.Door is not ("archive" or "unarchive"))
        {
            return Refused($"`{proposal.Door}` is not a session's change — `archive` or `unarchive`; " + (proposal.Door == "stop"
                ? "a stop is the person's own, on the session's page, where a go takes them."
                : proposal.Door == "delete" ? "a delete is `delete_propose`." : "nothing else is proposed of a session."));
        }

        var archive = proposal.Door == "archive";
        var named = Ids(proposal);
        var ended = string.Equals(proposal.Value?.Trim(), Ended, StringComparison.OrdinalIgnoreCase);
        if (named.Count == 0 && ended)
        {
            return archive ? EndedPlan(facts) : Refused("`ended` goes only with `archive`: what ended is archived, never unarchived.");
        }

        if (named.Count == 0) return Refused("a session proposal names the sessions by id, or `ended` for every one that ended.");

        var placed = facts.Placed
            .GroupBy(row => row.Session, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var records = new List<SessionRecord>();
        var kept = new List<string>();
        foreach (var id in named)
        {
            var record = facts.Sessions.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase));
            var why = record is null ? $"there is no session `{id}` on this machine"
                : WordsNever.IsHelp(record) ? $"`{record.Id}` is Ask Daoris's own conversation, which Ask Daoris → History keeps"
                : archive ? Keeps(record.Id, placed.GetValueOrDefault(record.Id))
                : null;
            if (why is not null) kept.Add(why);
            else if (!records.Any(each => each.Id == record!.Id)) records.Add(record!);
        }

        if (kept.Count > 0)
        {
            var rest = records.Count == 0 ? "" : $" Propose the others without {(kept.Count == 1 ? "it" : "them")}.";
            return Refused($"{string.Join("; ", kept)}.{rest}");
        }

        bool Marked(SessionRecord record) => placed.GetValueOrDefault(record.Id)?.Archived == true;
        var (acts, stays) = (records.Where(record => Marked(record) != archive).ToList(), records.Where(record => Marked(record) == archive).ToList());
        var terminal = $"daoris-driver sessions {proposal.Door} {string.Join(' ', records.Select(record => record.Id))}";
        var stand = archive ? "archived already" : "not archived";
        if (acts.Count == 0)
        {
            return new HelpPlan(null, $"{Listed(stays)} {(stays.Count == 1 ? "is" : "are")} {stand}, so applying changes nothing.", terminal, null)
            {
                Sessions = [],
            };
        }

        var said = archive
            ? acts.Count == 1
                ? $"Archive session `{acts[0].Id}` on this machine: it leaves Sessions' list with its record whole, and Show archived, in the list's ⋯, brings it back."
                : $"Archive {acts.Count} sessions on this machine: {Listed(acts)}. Each leaves Sessions' list with its record whole, and Show archived, in the list's ⋯, brings them back."
            : acts.Count == 1
                ? $"Unarchive session `{acts[0].Id}` on this machine: it is back in Sessions' list, in the group its state puts it in."
                : $"Unarchive {acts.Count} sessions on this machine: {Listed(acts)}. Each is back in Sessions' list, in the group its state puts it in.";
        // §5.2: a teammate's record may be archived here too, and only this machine's list changes.
        var teammates = archive ? acts.Where(record => record.Teammate).ToList() : [];
        var theirs = teammates.Count switch
        {
            0 => "",
            1 when acts.Count == 1 => " It is a teammate's: the mark is this machine's alone, and their record is not changed.",
            _ => $" {Listed(teammates)} {(teammates.Count == 1 ? "is a teammate's" : "are teammates'")}: the mark is this machine's alone, and their records are not changed.",
        };
        var also = stays.Count == 0 ? "" : $" {Listed(stays)} {(stays.Count == 1 ? "is" : "are")} {stand}, so nothing changes for {(stays.Count == 1 ? "it" : "them")}.";
        return new HelpPlan(null, said + theirs + also, terminal, null) { Sessions = [.. acts.Select(record => record.Id)] };
    }

    /// <summary>The ids its target names, comma-joined, each with a <c>#</c> before it stripped.</summary>
    private static List<string> Ids(HelpProposal proposal) =>
        [.. (proposal.Target ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(id => id.TrimStart('#').Trim())
            .Where(id => id.Length > 0)];

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var plan = applying.Plan;
        if (plan.Sessions is not { Count: > 0 } sessions)
        {
            return applying.Settled(true, $"Applied: `#{applying.Id}` — nothing changed: {plan.Describe}", null);
        }

        var (ok, message) = await applying.Doors.ArchiveSessionsAsync(sessions, applying.Proposal.Door == "archive", ct).ConfigureAwait(false);
        return applying.Settled(ok, $"{(ok ? "Applied" : "Not applied")}: `#{applying.Id}` (`{plan.Terminal}`) — {message}", message);
    }

    /// <summary>
    /// §5.3: every session in Ended that is not archived, in the list's order, and what stays in the list for the person, as
    /// Sessions' *Archive what ended…* says it before its press.
    /// </summary>
    private static HelpPlan EndedPlan(HelpMachineFacts facts)
    {
        // ASKSCOPE1c3: under a circle, only the workspace's own rows; a row no session record files is left out.
        var mine = facts.Placed.Where(row => HelpProposalScope.InCircle(facts, row.Session)).ToList();
        var ended = mine.Where(row => row.Group == SessionGroup.Ended && !row.Archived).Select(row => row.Session).ToList();
        var (you, review) = (mine.Count(row => row.Group == SessionGroup.You), mine.Count(row => row.Group == SessionGroup.Review));
        var stays = (you, review) switch
        {
            (0, 0) => "",
            (_, 0) => $" Kept in the list: {you} waiting on you.",
            (0, _) => $" Kept in the list: {review} to review.",
            _ => $" Kept in the list: {you} waiting on you, {review} to review.",
        };
        var said = ended.Count == 0
            ? $"Nothing that ended is left to archive, so applying changes nothing.{stays}"
            : $"Archives {ended.Count} session{(ended.Count == 1 ? "" : "s")} that ended.{stays} Each is judged again as Apply archives it, "
              + $"and Show archived, in the list's ⋯, brings {(ended.Count == 1 ? "it" : "them")} back.";
        return new HelpPlan(null, said, "daoris-driver sessions archive --ended --yes", null) { Sessions = ended };
    }

    /// <summary>
    /// Why the list keeps a session an archive names, by where it places it, as <see cref="SessionArchive"/> judges it and in the
    /// words Sessions' own refusal uses; null where it may be archived.
    /// </summary>
    private static string? Keeps(string id, SessionGrouping? placed) => placed?.Group switch
    {
        null => $"there is no session `{id}` on this machine",
        SessionGroup.Working => $"`{id}` is still running: stop it first",
        SessionGroup.You => $"`{id}` is waiting on you, and archive never hides what needs you",
        SessionGroup.Review => $"`{id}` has work to review, and archive never hides what needs you",
        _ => null,
    };

    private static string Listed(IEnumerable<SessionRecord> records) => string.Join(", ", records.Select(record => $"`{record.Id}`"));
}

public interface IHelpSessionDoors
{
    /// <summary>
    /// Sessions' own archive (SESSUX1a, D126 §5.2), the door its *Archive*, *Archive what ended…* and *Unarchive* press: each
    /// session archived or unarchived, judged again by where the list places it as it goes. Its answer comes back as its
    /// sentence, a refusal included.
    /// </summary>
    /// <param name="archived">True to archive, false to unarchive.</param>
    Task<(bool Ok, string Message)> ArchiveSessionsAsync(IReadOnlyList<string> ids, bool archived, CancellationToken ct);
}
