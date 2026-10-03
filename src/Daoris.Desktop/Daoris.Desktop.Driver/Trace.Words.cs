using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a trace prints (TRACE1, D143): the ask, its quests and their sessions, each section naming the store it was read
/// from, and every link nothing keeps said missing where it would have been. The driver's own sentences, in English, as the
/// headless host's every line is.
/// </summary>
/// <remarks>
/// <para><b>Facts, and moments compared.</b> What stood when a session started is read by comparing the moments each store
/// kept with the moment the session opened, never from what stands now: a standing answer set after a start says that what
/// stood before is not kept, since <c>driver.json</c> keeps the latest; a go-ahead answered after says the same, since an
/// answer replaces the one before.</para>
///
/// <para><b>An instruction by its event and size, never its words</b>: what a session was handed is transcript-class and stays
/// in its record (D76, D47 §4), where the session's page shows it. Its sections are read from the account kept beside it
/// (CONTEXT1): sizes, sources and what each bound left out, in the driver's words.</para>
/// </remarks>
internal static partial class TraceWords
{
    /// <summary>The states a record ends in (<see cref="SessionRecord"/>'s list): anything else still runs or waits.</summary>
    private static readonly HashSet<string> Endings = new(StringComparer.Ordinal)
    {
        "completed", "declined", "failed", "stopped", "stood-down",
    };

    /// <summary>The states a session's process runs in, whose rules file stands while it does.</summary>
    private static readonly HashSet<string> Running = new(StringComparer.Ordinal) { "queued", "starting", "working" };

    /// <summary>Each store that did not answer, said once at the top: its links below are then said not read.</summary>
    internal static IReadOnlyList<string> Unread(TraceFacts facts)
    {
        var lines = new List<string>();
        if (facts.SessionsUnread is { } sessions) lines.Add($"the service's session records could not be read: {sessions}");
        if (facts.QuestsUnread is { } quests) lines.Add($"the service's quests could not be read: {quests}");
        if (facts.LandingsUnread is { } landings) lines.Add($"{LandedBranches.FileName} under the home does not read, so no landing is read from it: {landings}");
        return lines;
    }

    /// <summary>The asks the chain names, in the order it reaches them: each quest's sender's, then an intake's own.</summary>
    internal static IReadOnlyList<string> AsksNamed(TraceFound found, TraceFacts facts) =>
    [
        .. found.Quests.Select(id => QuestOf(facts, id)).OfType<TracedQuest>().Select(quest => AskWords.AskOf(quest.From))
            .Concat(found.Sessions.Where(session => session.Quest is null).Select(session => session.Ask))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>The chain, whole: the heading and how the id was found, then each ask, its quests and their sessions.</summary>
    internal static string Say(
        TraceFound found, TraceFacts facts, IReadOnlyDictionary<string, TraceAskRead> asks, TraceSources sources, IReadOnlyList<string> unread)
    {
        var text = new StringBuilder();
        var shown = found.Kind == TraceEntry.Quest ? $"#{found.Id}" : found.Id;
        text.Append($"trace: {found.Kind} {shown} — each link read from the store that keeps it; a link nothing keeps is said missing.\n");
        foreach (var line in found.Found.Concat(unread)) text.Append($"  {line}\n");

        var printed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var questId in found.Quests)
        {
            var quest = QuestOf(facts, questId);
            var askId = quest is null ? null : AskWords.AskOf(quest.From);
            if (askId is not null && printed.Add(askId))
            {
                text.Append('\n');
                Ask(text, asks[askId], $"quest #{quest!.Id}'s sender", facts);
            }

            text.Append('\n');
            var namedBy = found.Sessions.FirstOrDefault(session => Same(session.Quest, questId)) is { } by ? $"session {by.Id}'s record"
                : facts.Landings.FirstOrDefault(landing => Same(landing.Quest, questId)) is { } landing ? $"the landing of session {landing.Session}"
                : "the trace";
            Quest(text, questId, quest, askId, namedBy, facts);

            foreach (var session in found.Sessions.Where(session => Same(session.Quest, questId)))
            {
                text.Append('\n');
                Session(text, session, facts, askId is null ? null : asks[askId], sources);
            }

            foreach (var landed in found.Unrecorded.Where(id => facts.Landings.Any(each => Same(each.Session, id) && Same(each.Quest, questId))))
            {
                text.Append('\n');
                Unrecorded(text, landed, facts);
            }
        }

        foreach (var session in found.Sessions.Where(session => session.Quest is null))
        {
            if (session.Ask is { } askId && printed.Add(askId))
            {
                text.Append('\n');
                Ask(text, asks[askId], $"intake session {session.Id}'s record", facts);
            }

            text.Append('\n');
            Session(text, session, facts, null, sources);
        }

        foreach (var landed in found.Unrecorded.Where(id => !facts.Landings.Any(each => Same(each.Session, id) && each.Quest is not null)))
        {
            text.Append('\n');
            Unrecorded(text, landed, facts);
        }

        return text.ToString();
    }

    /// <summary>The ask, from its door: the person's words verbatim with how and when each was given, and its go-aheads.</summary>
    private static void Ask(StringBuilder text, TraceAskRead read, string namedBy, TraceFacts facts)
    {
        if (read.Unread is { } why)
        {
            text.Append($"ask #{read.Id} · could not be read: {why}\n");
            return;
        }

        if (read.Ask is not { } ask)
        {
            text.Append($"ask #{read.Id} · not found: the service holds no ask by that id, which {namedBy} names\n");
            return;
        }

        text.Append($"ask #{ask.Id} · the service's ask record (GET /api/asks/{ask.Id})\n");
        text.Append($"  workspace {ask.Workspace} · {ask.State} · answered by the {ask.Tier} tier\n");
        if (ask.Intake is { } intake)
        {
            var record = SessionOf(facts, intake);
            text.Append(record is null
                ? $"  intake session {intake} · the service holds no record of it\n"
                : $"  intake session {intake} · {StateOf(record)} · {Agent(record)}\n");
        }

        if (ask.Quests.Count > 0) text.Append($"  quests it became: {string.Join(", ", ask.Quests.Select(id => $"#{id}"))}\n");
        if (ask.Note is { Length: > 0 } note) text.Append($"  its close said: {note}\n");

        if (ask.Words is not { } words)
        {
            text.Append("  the person's words: this service answers none, a host from before they were kept, which is not the same as none said\n");
            text.Append("  its sentence:\n").Append(Quoted(ask.Sentence, "    "));
        }
        else
        {
            text.Append($"  the person's words ({words.Count}), oldest first:\n");
            foreach (var word in words)
            {
                text.Append($"    {When(word.At)} · {Given(word)}\n").Append(Quoted(word.Text, "      "));
            }

            if (ask.WordsKeptFrom is { } from)
            {
                text.Append($"  their words are kept from {When(from)}: what they said before then is in its sessions' records\n");
            }
        }

        if (ask.GoAheads is not { } goAheads)
        {
            text.Append("  go-aheads: this service answers none, a host from before them\n");
        }
        else if (goAheads.Count == 0)
        {
            text.Append("  go-aheads: none asked\n");
        }
        else
        {
            text.Append($"  go-aheads ({goAheads.Count}):\n");
            foreach (var goAhead in goAheads)
            {
                var first = goAhead.Asked.MinBy(request => request.At);
                var asked = first is null ? "" : $" · first asked by session {first.Session}, {When(first.At)}";
                var near = goAhead.Near is { } other ? $" · asked again beside go-ahead {other}, whose words it shared" : "";
                text.Append($"    {goAhead.Number} · {goAhead.Named} · {Answered(goAhead.Answer)}{asked}{near}\n");
            }
        }
    }

    /// <summary>The quest as its operations replay, from its door: where it came from, its requirements and answers, its records.</summary>
    private static void Quest(StringBuilder text, string id, TracedQuest? quest, string? askId, string namedBy, TraceFacts facts)
    {
        if (quest is null)
        {
            text.Append(facts.QuestsUnread is not null
                ? $"quest #{id} · could not be read: the service's quests did not answer\n"
                : $"quest #{id} · not found: the service holds no quest by that id, which {namedBy} names\n");
            SessionsOn(text, id, facts);
            return;
        }

        text.Append($"quest #{quest.Id} → {quest.Address} · the service's quest, as its operations replay; ")
            .Append("no door on a local host reads the operations themselves\n");
        text.Append($"  \"{quest.Title}\"\n");
        text.Append($"  {quest.Status} · filed {WhenOr(quest.Filed)} · last moved {WhenOr(quest.Updated)}\n");
        text.Append(askId is not null
            ? $"  asked by ask #{askId}\n"
            : $"  on no ask: quest #{quest.Id} was asked by `{quest.From}` (its sender), so no person's words, go-aheads or requirements on an ask hold it\n");
        if (quest.Parent is { } parent) text.Append($"  a step of quest #{parent}, published when that one closed\n");
        if (quest.PublishedBy is { } by) text.Append($"  published by session {by}\n");
        if (quest.Awaits is { } awaits) text.Append($"  waits on quest #{awaits}, asked of the repository that knows\n");
        if (quest.Held) text.Append($"  held: its done departed from what you required, and waits for your yes (`daoris-driver quest accept {quest.Id}`)\n");
        if (quest.Accepted is { } accepted) text.Append($"  accepted {When(accepted)}: your yes to its departure\n");
        if (quest.Note is { } note) text.Append($"  its close said: {OneLine(note)}\n");

        if (quest.Requirements.Count > 0)
        {
            text.Append($"  requirements ({quest.Requirements.Count}), each the person's words and its check:\n");
            for (var number = 1; number <= quest.Requirements.Count; number++)
            {
                var requirement = quest.Requirements[number - 1];
                text.Append($"    {number} · \"{requirement.Quote}\" · check: {requirement.Check}\n");
                var answer = quest.Answers.FirstOrDefault(each => each.Requirement == number);
                text.Append(answer switch
                {
                    { Met: { } met } => $"        met: {met}\n",
                    { Departed: { } departed } => $"        departed: {departed}, on their words \"{answer.Quote}\"\n",
                    _ when quest.Status == "Done" => "        not answered: its done carries no answer to it\n",
                    _ => "        not answered yet: a done answers it\n",
                });
            }
        }

        if (quest.Then.Count > 0)
        {
            text.Append($"  then ({quest.Then.Count}): {string.Join("; ", quest.Then.Select(step => $"{step.To} · \"{step.Title}\""))}\n");
        }

        SessionsOn(text, quest.Id, facts);
    }

    /// <summary>The records that name a quest, oldest first, by id and state: each one's own trace is a word away.</summary>
    private static void SessionsOn(StringBuilder text, string questId, TraceFacts facts)
    {
        if (facts.SessionsUnread is not null)
        {
            text.Append("  its session records could not be read\n");
            return;
        }

        var on = facts.Sessions.Where(session => Same(session.Quest, questId)).OrderBy(session => session.Created ?? DateTimeOffset.MaxValue).ToList();
        text.Append(on.Count == 0
            ? "  no session record names it\n"
            : $"  session records on it, oldest first: {string.Join(", ", on.Select(session => $"{session.Id} {session.State}"))}\n");
    }

    /// <summary>
    /// One session: its record's facts, then this machine's (its events, its rules, its landing), then what stood when it started.
    /// </summary>
    private static void Session(StringBuilder text, TracedSession session, TraceFacts facts, TraceAskRead? ask, TraceSources sources)
    {
        text.Append($"session {session.Id} · {session.Kind} · {StateOf(session)} · the service's session record\n");
        text.Append($"  opened {WhenOr(session.Created)} · last moved {WhenOr(session.Updated)}\n");
        text.Append($"  {Agent(session)}\n");
        if (session.Teammate)
        {
            text.Append("  a teammate's record: it ran on another machine, which keeps its account, tree, events and rules\n");
        }
        else
        {
            text.Append($"  {(session.Tree is { } tree ? $"tree {tree}" : "tree not recorded")} · ")
                .Append($"{(session.BaseCommit is { } commit ? $"grew from commit {commit}" : "base commit not recorded")}\n");
        }

        if (session.Quest is null && session.Ask is null) text.Append("  a conversation on no quest: no quest and no ask to read\n");
        Before(text, session, facts);
        if (session.Took) text.Append("  it took the quest through its own connector\n");
        if (session.Note is { } note) text.Append($"  its record's note: {OneLine(note)}\n");
        if (session.Answer is { } answer) text.Append("  the person's answer to its park:\n").Append(Quoted(answer, "    "));
        Evidence(text, session);

        if (!session.Teammate)
        {
            var accepted = Events(text, session, sources.Home);
            Rules(text, session, sources.Home);
            Landing(text, session, facts, accepted);
        }

        Stood(text, session, ask, sources.Config);
    }

    /// <summary>The record before it on its quest, by when each opened: its ending, and whether it ran in the same tree.</summary>
    private static void Before(StringBuilder text, TracedSession session, TraceFacts facts)
    {
        if (session.Quest is null || session.Created is not { } opened) return;
        var before = facts.Sessions
            .Where(each => Same(each.Quest, session.Quest) && each.Created is { } at && at < opened)
            .MaxBy(each => each.Created);
        if (before is null)
        {
            text.Append("  the first session record on its quest\n");
            return;
        }

        var tree = before.Tree is null || session.Tree is null ? "its tree not recorded"
            : SamePath(before.Tree, session.Tree) ? "in the same tree"
            : "in another tree";
        text.Append(Endings.Contains(before.State)
            ? $"  before it on this quest: session {before.Id}, ended {before.State} {WhenOr(before.Updated)}, {tree}\n"
            : $"  before it on this quest: session {before.Id}, still {before.State}, {tree}\n");
    }

    /// <summary>The commits the driver read off its tree at its end, as its record keeps them.</summary>
    private static void Evidence(StringBuilder text, TracedSession session)
    {
        if (session.Evidence is not { } evidence)
        {
            text.Append("  evidence: none on its record\n");
            return;
        }

        var lines = evidence.ReplaceLineEndings("\n").Split('\n');
        text.Append($"  evidence: {lines[0]}\n");
        foreach (var line in lines.Skip(1).Where(line => line.Trim().Length > 0)) text.Append($"    {line.Trim()}\n");
    }

    /// <summary>
    /// What a landing keeps in the session's record (D100), read from the writer itself so a reworded note is read the same: the
    /// person's acceptance, a merge into the line's included, an acceptance at the quest's done (LAND2b), and a hand-off after it.
    /// </summary>
    private static readonly string[] Acceptances =
    [
        LandingRules.PersonAccepted,
        LandingRules.AutoAccepted,
        LandingRules.HandNote(new TreeHand(true, "")).Text!,
    ];

    /// <summary>
    /// This machine's record of it (D76): the driver's notes before its instruction, which name the account a start chose and
    /// why; each instruction it was handed, by its event and size; and each acceptance of its work a landing's press kept.
    /// </summary>
    /// <returns>Those acceptances, none included, or null where no record of it here could be read.</returns>
    private static IReadOnlyList<SessionEvent>? Events(StringBuilder text, TracedSession session, string home)
    {
        if (!SessionEvents.IsId(session.Id))
        {
            text.Append("  events: its id names no record on this machine\n");
            return null;
        }

        var events = new SessionEvents(Path.Combine(home, "sessions"));
        IReadOnlyList<SessionEvent> all;
        try
        {
            if (!File.Exists(events.PathOf(session.Id)))
            {
                text.Append("  events: none on this machine for it\n");
                return null;
            }

            all = events.After(session.Id, 0).Events;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            text.Append($"  events: the record here does not read: {error.Message}\n");
            return null;
        }

        text.Append($"  events (sessions/{session.Id}.events.jsonl, this machine's):\n");
        foreach (var note in all.TakeWhile(e => e.Kind != SessionEventKind.User).Where(e => e.Kind == SessionEventKind.Note && e.Text is { Length: > 0 }))
        {
            text.Append($"    its start, in the driver's words: {OneLine(note.Text!)}\n");
        }

        var instructions = all.Where(e => e.Kind == SessionEventKind.User && e.Origin == "target").ToList();
        if (instructions.Count > 0)
        {
            text.Append($"    instruction handed: {string.Join("; then ", instructions.Select(Size))} (its words stay in the record and are not printed)\n");
            foreach (var instruction in instructions) Sections(text, instruction);
        }
        else
        {
            text.Append(session.Kind == "chat"
                ? "    instruction: none; a conversation opens on the person's own words\n"
                : "    instruction handed: no instruction event in its record\n");
        }

        var accepted = all
            .Where(e => e.Kind == SessionEventKind.Note && e.Text is { } said && Acceptances.Any(start => said.StartsWith(start, StringComparison.Ordinal)))
            .ToList();
        foreach (var note in accepted) text.Append($"    {When(note.At)} · {OneLine(note.Text!)}\n");
        return accepted;
    }

    /// <summary>
    /// What an instruction was composed of (CONTEXT1, D143 point 1), from the account kept beside it on its event: each section
    /// in the driver's words with what its bound left out, what was handed beside it, then what could have been handed and was
    /// not. An instruction from before the account was kept says so (point 3), and nothing is rebuilt from what stands now.
    /// </summary>
    private static void Sections(StringBuilder text, SessionEvent instruction)
    {
        if (instruction.Account is not { } account)
        {
            text.Append("    its sections: not kept, since it was handed before the driver kept an account of them\n");
            return;
        }

        text.Append($"    its sections, as the driver composed them (event {instruction.Seq}, {Count(account.Chars)} characters):\n");
        void Section(HandedSection section, string indent)
        {
            text.Append($"{indent}{OneLine(section.Said)}\n");
            foreach (var cut in section.Cuts ?? []) text.Append($"{indent}  left out: {OneLine(cut.Said)}\n");
        }

        foreach (var section in account.Sections.Where(section => section.None is null)) Section(section, "      ");
        var absent = account.Sections.Where(section => section.None is not null).ToList();
        if (absent.Count == 0) return;
        text.Append("      not handed:\n");
        foreach (var section in absent) Section(section, "        ");
    }

    /// <summary>One instruction by its event and its size: the length written, or, where the record cut it, the length it said.</summary>
    private static string Size(SessionEvent instruction)
    {
        var said = instruction.Text ?? "";
        if (CutSaid().Match(said) is { Success: true } cut && cut.Groups["kept"].Length == SessionEvents.TextLimit
            && int.TryParse(cut.Groups["chars"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var whole))
        {
            return $"event {instruction.Seq}, {Count(whole)} characters, of which the record keeps the first {Count(SessionEvents.TextLimit)}";
        }

        return $"event {instruction.Seq}, {Count(said.Length)} characters";
    }

    /// <summary>
    /// The rules it was handed (PERM1, D72), from the file the driver wrote under the home for it, while that file stands: it is
    /// the session's own and goes when its run ends.
    /// </summary>
    private static void Rules(StringBuilder text, TracedSession session, string home)
    {
        var name = $"{session.Id}.settings.json";
        var path = Path.Combine(home, SpawnServers.Folder, name);
        if (!File.Exists(path))
        {
            text.Append(Running.Contains(session.State)
                ? "  rules handed: no file under the home while it runs: its agent takes no rules file, or there was nothing to hand\n"
                : "  rules handed: not on this machine: the file goes when its session ends, and a parked session's when its run does\n");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var permissions = root.TryGetProperty("permissions", out var lists) ? lists : default;
            int Listed(JsonElement element, string list) =>
                element.ValueKind == JsonValueKind.Object && element.TryGetProperty(list, out var items) && items.ValueKind == JsonValueKind.Array
                    ? items.GetArrayLength()
                    : 0;
            var hard = root.TryGetProperty("autoMode", out var auto) && auto.TryGetProperty("hard_deny", out var denials)
                       && denials.ValueKind == JsonValueKind.Array
                ? denials.EnumerateArray().Count(entry => entry.ValueKind == JsonValueKind.String && entry.GetString() != SpawnSettings.HarnessDefaults)
                : 0;
            var guarded = root.TryGetProperty("hooks", out var hooks) && hooks.ValueKind == JsonValueKind.Object && hooks.TryGetProperty("PreToolUse", out _);
            text.Append($"  rules handed ({SpawnServers.Folder}/{name}, kept while it runs): ")
                .Append($"{Listed(permissions, "allow")} allowed, {Listed(permissions, "ask")} asked, {Listed(permissions, "deny")} denied")
                .Append(hard == 0 ? "" : $"; {hard} hard denial{(hard == 1 ? "" : "s")} beside the harness's own")
                .Append(guarded ? "; the tree guard holds it to its tree" : "")
                .Append('\n');
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            text.Append($"  rules handed: the file under the home does not read: {error.Message}\n");
        }
    }

    /// <summary>
    /// Where its work landed, from <c>landings.json</c> (WSR5): each branch a landing made of it, standing or gone. A landing
    /// into the line records no branch, so where none is, the acceptance its record keeps is pointed to, or said absent.
    /// </summary>
    /// <param name="accepted">The acceptances its record keeps, or null where no record of it here could be read.</param>
    private static void Landing(StringBuilder text, TracedSession session, TraceFacts facts, IReadOnlyList<SessionEvent>? accepted)
    {
        if (facts.LandingsUnread is not null)
        {
            text.Append($"  landing: {LandedBranches.FileName} does not read, so no branch landing of it is read\n");
            return;
        }

        var landings = facts.Landings.Where(each => Same(each.Session, session.Id)).ToList();
        foreach (var landing in landings) text.Append($"  landing: {Landed(landing)}\n");
        DueToLand(text, session, facts);
        if (landings.Count > 0) return;

        text.Append(accepted switch
        {
            null => "  landing: no branch landing on this machine names it, and no record of it here could say whether its work was accepted\n",
            { Count: 0 } => "  landing: none: no branch landing on this machine names it, and its record keeps no acceptance of its work\n",
            _ => "  landing: no branch landing on this machine names it; its record keeps the acceptance above, which is all a "
                 + "landing into the line (merge) keeps, without the merge's own commit\n",
        });
    }

    /// <summary>A landing whose session the service holds no record of: what the landing alone says.</summary>
    private static void Unrecorded(StringBuilder text, string session, TraceFacts facts)
    {
        text.Append($"session {session} · its landing names it, and the service holds no record of it\n");
        foreach (var landing in facts.Landings.Where(each => Same(each.Session, session))) text.Append($"  landing: {Landed(landing)}\n");
    }

    private static string Landed(LandedBranch landing)
    {
        var said = new StringBuilder($"branch {landing.Branch} at {landing.Tip}, ");
        said.Append(landing.Line is { } line ? $"from line {line}" : "from a line git named none of").Append($", {When(landing.LandedAt)}");
        if (landing.From is { } from) said.Append($"; grew from {from}");
        if (landing.Plugin is { } plugin)
        {
            said.Append(landing.Pushed ? $"; pushed by plugin {plugin}" : $"; handed to plugin {plugin}, which did not push it");
            if (landing.PullRequest is { } pull) said.Append($", pull request {pull}");
        }

        if (landing.GoneAt is { } gone)
        {
            said.Append($"; gone since {When(gone)}");
            if (landing.RemovedAs is { } kind) said.Append($", removed as {kind}{(landing.RemovedOn is { } on ? $" on {on}" : "")}");
        }

        // Who accepted it, and the rule it was made under (LAND2b, D145 point 6): kept since then, and said missing before (D143).
        said.Append(landing.AcceptedBy switch
        {
            AcceptedBy.Auto => "; accepted automatically when its quest was done",
            AcceptedBy.Person => "; accepted by the person's press",
            _ => "; who accepted it is not kept: it landed before landings kept it",
        });
        if (landing.Rule is { } rule)
        {
            said.Append($"; under the {rule.Source}'s rule, ")
                .Append(rule.Plugin is { } named ? $"naming plugin {named}" : "naming no plugin")
                .Append(rule.AutoAccept ? ", accepting automatically" : ", accepting at a press");
        }

        return said.ToString();
    }

    /// <summary>
    /// Its entry on the due list (LAND2b, design §8), where it has one: when it became due, and each try by its code, with the
    /// branch it made or met. A session never due has none, and says nothing here.
    /// </summary>
    private static void DueToLand(StringBuilder text, TracedSession session, TraceFacts facts)
    {
        if (facts.AutoLandings.FirstOrDefault(each => Same(each.Session, session.Id)) is not { } entry) return;
        text.Append($"  due to land automatically since {When(entry.DueAt)} ({AutoLandings.FileName}, this machine's)")
            .Append(entry.Closed is { } closed ? $", closed {When(closed)}\n" : ", still waiting\n");
        if (entry.Tries.Count == 0) text.Append("    not tried yet\n");
        foreach (var tried in entry.Tries)
        {
            text.Append($"    {When(tried.At)} · {tried.Code}")
                .Append(tried.Branch is { } branch ? $" · branch {branch}" : "")
                .Append(tried.Commits is { } commits ? $" · {commits} commit(s)" : "")
                .Append(tried.Uncommitted is { } paths ? $" · {paths} uncommitted path(s)" : "")
                .Append(tried.Tip is { } tip ? $" · at {tip[..Math.Min(12, tip.Length)]}" : "")
                .Append('\n');
        }
    }

    /// <summary>
    /// What stood when a driven session on a quest started: the standing answer for its repository, each go-ahead on its ask,
    /// and how many of the person's words were said before it, each by the moments kept and nothing else.
    /// </summary>
    private static void Stood(StringBuilder text, TracedSession session, TraceAskRead? ask, DriverConfig config)
    {
        if (session.Kind != "driven" || session.Quest is null) return;
        if (session.Created is not { } opened)
        {
            text.Append("  what stood when it started: its record says no moment it opened, so nothing is read against one\n");
            return;
        }

        text.Append("  what stood when it started:\n");
        if (!session.Teammate)
        {
            var standing = config.StandingFor(session.Repository);
            var named = $"    standing answer for {session.Repository}";
            if (standing is null) text.Append($"{named}: none set now, and one cleared since is not kept\n");
            else if (standing.At is not { } set) text.Append($"{named}: set at a moment driver.json does not say, so whether it stood then is not known\n");
            else if (set < opened) text.Append($"{named}: set {When(set)}, before it started:\n").Append(Quoted(standing.Says, "      "));
            else text.Append($"{named}: set {When(set)}, after it started; what stood before is not kept: driver.json keeps the latest\n");
        }

        if (ask?.Ask is not { } held) return;
        foreach (var goAhead in held.GoAheads ?? [])
        {
            var first = goAhead.Asked.MinBy(request => request.At);
            var mine = goAhead.Asked.Any(request => Same(request.Session, session.Id)) ? ", by this session" : "";
            text.Append($"    go-ahead {goAhead.Number}: ").Append(
                first is null || first.At >= opened ? $"first asked after it started{mine}"
                : goAhead.Answer is not { } answer ? "waiting on the person when it started, and still"
                : answer.At < opened ? $"{(answer.Approved ? "approved" : "refused")} {When(answer.At)}, before it started"
                : $"answered {When(answer.At)}, after it started; whether an earlier answer stood then is not kept: an answer replaces the one before")
                .Append('\n');
        }

        if (held.Words is { } words)
        {
            text.Append($"    the person's words on ask #{held.Id}: {words.Count(word => word.At < opened)} of {words.Count} said before it started\n");
        }
    }

    /// <summary>A record's state, with what its flags add to it.</summary>
    private static string StateOf(TracedSession session) => session.State switch
    {
        "failed" when session.Limit => "failed, by an account's limit",
        "stopped" when session.Interrupted => "stopped, not by the person: the driver closed, or the sweep found it orphaned",
        "awaiting-person" when session.Answer is not null => "awaiting-person, answered: it goes on at the driver's next look",
        "" => "state not recorded",
        var state => state,
    };

    /// <summary>What ran it, as its record names it: the adapter, the harness's version, the account; each said missing where unsaid.</summary>
    private static string Agent(TracedSession session)
    {
        var agent = $"agent {session.Adapter ?? "not recorded"} · {(session.HarnessVersion is { } version ? $"harness {version}" : "harness not recorded")}";
        if (session.Teammate) return agent;
        return agent + (session.Profile is { } account ? $" · account {account}" : " · account none named: the tool's own sign-in");
    }

    /// <summary>How a word was given (DRIFT1a's kinds), and to which session on which quest.</summary>
    private static string Given(AskWordView word)
    {
        var on = word.Quest is { Length: > 0 } quest ? $", on quest #{quest}" : "";
        var to = word.Session is { Length: > 0 } session ? session : "a session";
        return word.Kind switch
        {
            AskWordView.Asked => "asked",
            AskWordView.Answered => $"answered session {to}{on}",
            AskWordView.Added => $"added while session {to} ran{on}",
            AskWordView.Reopened => $"said to session {to} after it ended{on}",
            var kind => $"{kind}, to session {to}{on}",
        };
    }

    private static string Answered(GoAheadAnswerView? answer) => answer switch
    {
        null => "waiting on the person",
        _ => $"{(answer.Approved ? "approved" : "refused")} {When(answer.At)}{(answer.Words is { Length: > 0 } words ? $", saying: \"{words}\"" : "")}",
    };

    private static TracedQuest? QuestOf(TraceFacts facts, string id) => facts.Quests.FirstOrDefault(quest => Same(quest.Id, id));

    private static TracedSession? SessionOf(TraceFacts facts, string id) => facts.Sessions.FirstOrDefault(session => Same(session.Id, id));

    private static bool Same(string? a, string? b) => a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b) =>
        string.Equals(a.Replace('\\', '/').TrimEnd('/'), b.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>Someone's words, line by line beneath their item, as the instructions quote them.</summary>
    private static string Quoted(string words, string indent) =>
        string.Join("\n", words.ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? $"{indent}>" : $"{indent}> {line}")) + "\n";

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();

    /// <summary>A moment as the instructions say it: to the minute, in UTC, the same on every machine.</summary>
    private static string When(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string WhenOr(DateTimeOffset? at) => at is { } moment ? When(moment) : "at a moment not recorded";

    private static string Count(int number) => number.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A field the record cut (<see cref="SessionEvents.Cut"/>): what it kept, then how long the original was.</summary>
    [GeneratedRegex(@"^(?<kept>.*)… \((?<chars>\d+) chars\)$", RegexOptions.Singleline)]
    private static partial Regex CutSaid();
}
