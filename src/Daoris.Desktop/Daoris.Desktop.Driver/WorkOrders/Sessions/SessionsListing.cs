namespace Daoris.Driver;

/// <summary>The listing <c>sessions</c> prints: this machine's sessions by what they need.</summary>
internal static class SessionsListing
{
    internal static async Task<int> ListAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct)
    {
        // The running Daoris's own look where it answers, its lines made by ListLines; this process's look otherwise.
        if (world.Instance is { } instance && await InstanceDoor.TryReadAsync(
                OpenRoutes.SessionGroups, new { group = ask.Group, repository = ask.Repository, json = ask.Json, said = true },
                TreesSaid.ReadWords(output, instance.Error), instance, ct).ConfigureAwait(false) is { } viaInstance)
        {
            return viaInstance;
        }

        // A fresh plan, which sees no cool-off: the look reads the home's for words resumed on an account.
        var look = await SessionGroups.LookAsync(world.Service, world.Config, world.Unadopted, lastLook: null, world.Home, ct: ct, coolingOf: world.CoolingOf)
            .ConfigureAwait(false);
        look = look with { RunWaits = WorkOrdersWorkflows.Current.PersonWaits(world.Home) };
        foreach (var line in ListLines(look, ask, world.Home, DateTimeOffset.UtcNow)) output.WriteLine(line);
        return 0;
    }

    /// <summary>
    /// The listing's lines from one look: what <c>sessions</c> prints, which the instance's <c>SESSION_GROUPS</c> answers as
    /// <c>said</c> from its own look, so the verb that asks it prints the same words. The <c>--json</c> form is one element, whole.
    /// </summary>
    internal static IReadOnlyList<string> ListLines(SessionLook look, SessionsAsk ask, string home, DateTimeOffset now)
    {
        var output = new List<string>();
        var records = look.Records.GroupBy(record => record.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var rows = SessionGroups.Read(look)
            .Where(row => ask.Group is null || row.Group == ask.Group)
            .Where(row => ask.Repository is null
                || (records.TryGetValue(row.Session, out var record) && string.Equals(record.Repository, ask.Repository, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // By entry, the rows kept for the page and for --json. An entry is in the group, and the repository, it is.
        var entries = SessionGroups.ReadEntries(look)
            .Where(entry => ask.Group is null || entry.Group == ask.Group)
            .Where(entry => ask.Repository is null || string.Equals(entry.Repository, ask.Repository, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (ask.Json)
        {
            output.Add(SessionsJson.Json(rows, entries));
            return output;
        }

        if (entries.Count == 0)
        {
            output.Add($"sessions: {Nothing(ask)}.");
            return output;
        }

        var quests = look.Quests.GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var openings = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions))
            .Openings(entries.Select(entry => entry.Leading).Where(SessionEventText.IsId));
        var placed = SessionGroups.Read(look).GroupBy(row => row.Session, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var group in entries.GroupBy(entry => entry.Group))
        {
            var each = group.ToList();
            output.Add($"{GroupName(group.Key)} ({each.Count})");
            foreach (var entry in each)
            {
                var record = records[entry.Leading];
                var row = placed[entry.Leading];
                var line = string.Join(" · ", new[] { Title(record, quests, openings), Span(record, now) }.Concat(Facts(row, record))
                    .Concat(entry.Parts.Count > 1 ? [$"{entry.Parts.Count} parts"] : []));
                output.Add($"  {entry.Leading}  {Word(row.Shown)}  {record.Repository} — {line}");
                if (entry.Parts.Count > 1)
                {
                    foreach (var part in entry.Parts)
                    {
                        output.Add($"      {part.Session}  {Word(part.Shown)}  {(part.Quest is { } quest ? $"#{quest}" : "conversation")} · {Began(part)}");
                    }
                }

                foreach (var coworker in entry.Coworkers)
                {
                    output.Add($"      {coworker.Session}  {Word(coworker.Shown)}  {coworker.Role} of {coworker.Serves}");
                }
            }
        }

        return output;
    }

    /// <summary>How a part began, in the terminal's words: what it followed or who asked; a word this build does not know, opened.</summary>
    internal static string Began(EntryPart part) => part.Began switch
    {
        PartBegan.Retried => $"retried after {part.After}",
        PartBegan.Resumed => $"resumed after {part.After}",
        PartBegan.CarriedOn => part.After is { } after ? $"carried on after {after}" : "carried on",
        PartBegan.HandedOver => $"handed over from {part.After}",
        // A part moved on from one whose own conversation is lost, as the page's `work.parts.began.moved` says it.
        PartBegan.Moved => $"moved on from {part.After}: its own conversation was lost",
        PartBegan.Asked => part.AskedBy is { } by ? $"asked by {by}" : "asked by a sibling",
        _ => "opened",
    };

    private static string Nothing(SessionsAsk ask) => (ask.Group, ask.Repository) switch
    {
        (SessionGroup.NeedsYou, _) => "nothing needs you",
        (SessionGroup.Working, _) => "nothing is working",
        (SessionGroup.Pending, _) => "nothing is pending",
        (SessionGroup.Failed, _) => "nothing has failed",
        (SessionGroup.Done, _) => "nothing is done",
        (SessionGroup.Archived, _) => "nothing is archived",
        (_, { } repository) => $"no session of {repository} on this machine",
        _ => "no session on this machine",
    };

    private static string GroupName(string group) => group switch
    {
        SessionGroup.NeedsYou => "Needs you",
        SessionGroup.Working => "Working",
        SessionGroup.Pending => "Pending",
        SessionGroup.Failed => "Failed",
        SessionGroup.Done => "Done",
        _ => "Archived",
    };

    /// <summary>The reason inside its state where no other fact of its line says it, in the terminal's words.</summary>
    private static string? ReasonWords(SessionGrouping row) => row.Reason switch
    {
        SessionReason.Unclosed => "its quest waits on your done",
        SessionReason.StewardStop => "the steward stopped it; it waits on your try again",
        SessionReason.SignIn => "its next start waits on a sign-in",
        SessionReason.CarriedOn => "its quest carries on",
        SessionReason.Opinion => "its next step waits on a second opinion",
        SessionReason.Elsewhere => "its quest is taken elsewhere",
        _ => null,
    };

    /// <summary>The word a row shows, as the page says it in English: the record's state, or the reader's own word.</summary>
    internal static string Word(string shown) => shown switch
    {
        "awaiting-person" => "waiting on you",
        "stood-down" => "stood down",
        ShownState.AwaitingReply => "awaiting reply",
        ShownState.GoingOn => "going on",
        // A limit's wait; the time and the account are on the line's held fact.
        ShownState.LimitWaits => "waiting for a limit to reset",
        _ => shown,
    };

    /// <summary>What a session is for: its quest's name, a conversation's opening line, Ask Daoris, an ask's intake.</summary>
    private static string Title(SessionRecord record, IReadOnlyDictionary<string, QuestView> quests, IReadOnlyDictionary<string, string> openings) =>
        record.Quest is { } quest ? quests.TryGetValue(quest, out var view) ? view.Name : $"#{quest}"
        : record.Repository == KnowledgeRoomIdentity.Repository ? "Ask Daoris"
        : record.Ask is { } ask ? $"Intake for ask #{ask}"
        : openings.TryGetValue(record.Id, out var opening) ? opening
        : $"a conversation in {record.Repository}";

    /// <summary>How long: a live session since it began, an ended one from its start to its end.</summary>
    private static string Span(SessionRecord record, DateTimeOffset now)
    {
        var to = record.Live || record.Updated == DateTimeOffset.MinValue ? now : record.Updated;
        var span = record.Created == DateTimeOffset.MinValue || to < record.Created ? TimeSpan.Zero : to - record.Created;
        return span.TotalMinutes < 1 ? $"{(int)span.TotalSeconds}s"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes}m"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}m"
            : $"{(int)span.TotalDays}d {span.Hours}h";
    }

    /// <summary>What a row's second line says on the screen, in the terminal's words.</summary>
    internal static IEnumerable<string> Facts(SessionGrouping row, SessionRecord record)
    {
        if (row.Teammate) yield return $"on {record.Id[..record.Id.IndexOf('/')]}";
        // Its line's state where another part or a co-worker decides it, named.
        if (row.Line is { } standing && standing.By != row.Session)
        {
            yield return $"its line is {GroupName(standing.State).ToLowerInvariant()}: {(standing.Role == LineRole.Part ? "part" : $"its {standing.Role}")} {standing.By}";
        }
        if (row.Shown == ShownState.Parked)
        {
            yield return row.Strikes is { } strikes
                ? $"after {strikes} failed session{(strikes == 1 ? "" : "s")}"
                : "after its failed sessions";
        }

        if (row.Shown == ShownState.AwaitingReply && row.Awaits is { } awaits)
        {
            yield return row.AwaitsOf is { } of ? $"waits on #{awaits}, asked of {of}" : $"waits on #{awaits}";
        }

        if (row.Shown == ShownState.Answered) yield return "your answer goes on at the driver's next look";
        if (ReasonWords(row) is { } reason) yield return reason;
        foreach (var said in RunWaitWords.Of(row)) yield return said;
        if (row.Reason == SessionReason.Review && row.Work is { } work)
        {
            yield return work.Commits is > 0 and var commits ? $"{commits} commit{(commits == 1 ? "" : "s")} to review"
                : work.Commits == 0 && work.Uncommitted > 0 ? "uncommitted changes"
                : "work to review";
        }

        if (row.Lands is { } lands) yield return LandingHint.Of(row, lands);

        // Commits the line holds by content are never offered to land again; the door that discards the tree is, with the ref its
        // commits stay at, or why the tree stays.
        if (row.Discards is { Work: var held } discards)
        {
            yield return held.Keeps is { } keeps
                ? $"{held.Held.Said}, in its tree {discards.Tree}: daoris tree remove {row.Session} discards it, keeping its "
                  + $"commits at `{keeps}`"
                : $"{held.Held.Said}, in its tree {discards.Tree}, which stays: {held.Stays}";
        }

        // A transient hold (a failed record) is released for its end by the plain retry, never the stop's `--session`.
        if (row.HoldsQuest && record.Quest is { } quest)
        {
            yield return record.State == "failed"
                ? $"held here until you try again: daoris quest retry {quest}"
                : $"held here until you try again: daoris quest retry {quest} --session {row.Session}";
        }
        // A pause, never a hold, in its words: Resume is the press that moves it.
        if (row.PausedBy is { } pause)
        {
            yield return pause.Scope == WorkScope.Ask
                ? $"paused with ask #{pause.Id}; Resume carries it on: {pause.Door}"
                : $"paused with quest #{pause.Id}; Resume carries it on: {pause.Door}";
        }

        // What holds the person's words, in the words `sessions say` prints for them; a pause's is the line above.
        if (row.Holds is { Why: not WordsHold.Paused } holds) yield return $"held: {holds.Sentence}";
        if (row.Archived && row.Group != SessionGroup.Archived) yield return "archived";
    }
}
