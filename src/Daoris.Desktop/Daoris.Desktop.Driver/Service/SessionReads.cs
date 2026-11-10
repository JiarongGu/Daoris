using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Sessions;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// What the driver reads of the service's session records, each read from the records as the contracts type them (<see cref="SessionResponse"/>):
/// the planner's inputs, the strikes, the last runs and the carry-ons' takes. A teammate's record comes down with the sync keyed
/// <c>origin/id</c>, the same rule the platform reads it by, and holds nothing here: counted, it would spend this machine's
/// cap on work another machine is doing, so the reads of this machine's work leave it out.
/// </summary>
internal static class SessionReads
{
    /// <summary>The session list the service wrote, as its records; a text that is no list throws.</summary>
    internal static SessionResponse[] Parse(string json) =>
        JsonSerializer.Deserialize(json, ContractJson.Options.GetTypeInfo(typeof(SessionResponse[]))) as SessionResponse[] ?? [];

    /// <summary>The same, where a text that is no list is none.</summary>
    internal static SessionResponse[] Listed(string json)
    {
        try
        {
            return Parse(json);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>A record that comes down from the team: keyed <c>origin/id</c>, the id this machine's own never has.</summary>
    internal static bool IsTeams(SessionResponse session) => session.Id?.Contains('/') == true;

    private static bool IsState(SessionResponse session, string state) => string.Equals(session.State, state, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<NotePart>? PartsOf(SessionResponse session) => session.NoteParts is { } parts ? NotePart.ReadParts(parts) : null;

    private static DateTimeOffset? MomentOf(DateTimeOffset moment) => moment == default ? null : moment;

    /// <summary>Whether a record's note says its account is refused, by an account's line's code and never its words.</summary>
    private static bool AccountRefused(SessionResponse session) =>
        PartsOf(session) is { } parts && parts.Any(part => part.Code is { } code && RecordNotes.AccountsOwn.Contains(code));

    /// <summary>
    /// Whether a record ended <c>choice-not-offered</c>, by its end code: its agent refused the choice at the
    /// open, before any prompt, so it is no strike, no last run and no start the walk counts.
    /// </summary>
    internal static bool NotOffered(SessionResponse session) =>
        PartsOf(session)?.Any(part => part.Code == RecordNotes.EndedChoiceNotOffered) == true;

    /// <summary>Whether a raw record says it ended <c>choice-not-offered</c>, for a reader that holds the record as JSON.</summary>
    internal static bool NotOffered(JsonElement session) =>
        NotePart.Read(session)?.Any(part => part.Code == RecordNotes.EndedChoiceNotOffered) == true;

    internal static bool IsTeams(JsonElement session) => Text(session, "id")?.Contains('/') == true;

    /// <summary>This machine's records as the planner's views, the active list's or all.</summary>
    internal static IReadOnlyList<SessionView> Views(IEnumerable<SessionResponse> listed)
    {
        var sessions = new List<SessionView>();
        foreach (var session in listed)
        {
            if (IsTeams(session)) continue;
            sessions.Add(new SessionView(session.Id ?? "", session.Repository ?? "", session.State ?? "", session.Note)
            {
                Ask = session.Ask,
                Tree = session.Tree,
                Quest = session.Quest,
                Adapter = session.Door is { Length: > 0 } adapter ? adapter : null,
                // Where its tree stood when it opened: what the sweep reads a lost done's evidence as changed from.
                BaseCommit = session.BaseCommit is { Length: > 0 } baseCommit ? baseCommit : null,
                // Its note's lines by code, handed on wherever the note is; null for a record without parts.
                NoteParts = PartsOf(session),
                // Its words, read as SessionRecords reads them: the person's make an answered park.
                Said = session.Said is { } said ? [.. said.Select(ReadWord).OfType<SaidWordView>()] : null,
                Workspace = RemoteTarget.Workspace(session.Workspace),
                // What the cap counts by: its kind, and the mark of each conversation the driver opens for itself, an intake's ask
                // above, a helper's serves and a reviewer's opinion. A conversation the person holds has none of them.
                Kind = session.Kind ?? SessionKinds.Driven,
                Opinion = session.Opinion is { Length: > 0 } opinion ? opinion : null,
                Serves = session.Serves is { Length: > 0 } serves ? serves : null,
                Piece = session.Piece is { Length: > 0 } piece ? piece : null,
                Decides = session.Decides is { Length: > 0 } decides ? decides : null,
                HoldsPlace = session.HoldsPlace is { Count: > 0 } kept ? kept : null,
            });
        }

        return sessions;
    }

    /// <summary>
    /// This machine's active sessions as the downloads' rule reads them: each id, when its record opened, and the
    /// downloaded versions its starts are handed, null for a record without that field, which keeps the start-time rule.
    /// </summary>
    internal static IReadOnlyList<LiveSession> Running(IEnumerable<SessionResponse> listed) =>
        [.. listed.Where(session => !IsTeams(session))
            .Select(session => new LiveSession(session.Id ?? "", MomentOf(session.Created), session.Resolved is { } resolved ? [.. resolved.OfType<string>()] : null))];

    /// <summary>
    /// An end that is another count's or no end of the work: a limit, a refused account, a choice not offered, a transient end, an update's
    /// close, or the quiet end (the <c>quiet</c> rule's own). It is no failure and breaks no run of them.
    /// </summary>
    private static bool IsNeutral(SessionResponse session) =>
        session.Limit || AccountRefused(session) || NotOffered(session)
        || PartsOf(session)?.Any(part => part.Code is RecordNotes.EndedTransient or RecordNotes.UpdateClosed or RecordNotes.EndedQuiet) == true;

    /// <summary>The one definition of a failure, for the quest's count and its run on one choice: a <c>failed</c> end or a cut-off, never a neutral one.</summary>
    private static bool IsFailure(SessionResponse session) =>
        !IsNeutral(session) && (IsState(session, "failed") || (IsState(session, "stopped") && session.Interrupted));

    /// <summary>
    /// How often each quest has failed. Only <c>failed</c> counts, and a stop that is not the person's: a
    /// <c>stood-down</c> session means somebody else took the quest first, a <c>declined</c> one closes it, a <c>stopped</c> one the person
    /// stopped is theirs. A failure that says <c>limit</c> is not a strike, nor is one whose account is refused, whose
    /// end is past a transient bound, whose update closed (<see cref="RecordNotes.UpdateClosed"/>) or whose choice its agent
    /// refused at the open: counted, a healthy quest parks for succeeding.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> Strikes(IEnumerable<SessionResponse> listed)
    {
        var strikes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in listed)
        {
            // A strike is this driver's own judgement of its own attempts: a teammate's failure there says nothing about this machine's next try.
            if (IsTeams(session) || !IsFailure(session)) continue;
            if (session.Quest is not { Length: > 0 } quest) continue;

            strikes[quest] = strikes.TryGetValue(quest, out var seen) ? seen + 1 : 1;
        }

        return strikes;
    }

    /// <summary>
    /// How many strikes in a row each quest's last choice has made: the trailing failed records on the same agent and asked
    /// model, in the order they were made. A limit, a refused account, a transient end and a choice not offered are no strike and break
    /// nothing; any other end that is not a failure, or a failure on another choice, starts the count again.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> FailedRuns(IEnumerable<SessionResponse> listed)
    {
        var runs = new List<(string Quest, DateTimeOffset At, string State, string Choice)>();
        foreach (var session in listed)
        {
            if (IsTeams(session) || session.Quest is not { Length: > 0 } quest) continue;
            var state = session.State ?? "";
            if (state is "stood-down" or "" || IsNeutral(session)) continue;
            var asked = Wired<ActorAsked>(session.Asked);
            runs.Add((quest, session.Created, IsFailure(session) ? "failed" : state, $"{session.Door}|{asked?.NamedModel}|{asked?.Effort.Value}"));
        }

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var choices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs.OrderBy(each => each.At))
        {
            if (!string.Equals(run.State, "failed", StringComparison.OrdinalIgnoreCase))
            {
                counts[run.Quest] = 0;
                continue;
            }

            counts[run.Quest] = choices.TryGetValue(run.Quest, out var was) && was == run.Choice && counts.GetValueOrDefault(run.Quest) > 0 ? counts[run.Quest] + 1 : 1;
            choices[run.Quest] = run.Choice;
        }

        return counts.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The session this machine last ran on each quest, and where. A stand-down is not a run: it means somebody else had the quest, so
    /// resuming it here would double another taker's work.
    /// </summary>
    internal static IReadOnlyDictionary<string, PriorSession> LastRun(IEnumerable<SessionResponse> listed)
    {
        var last = new Dictionary<string, (PriorSession Session, DateTimeOffset At)>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in listed)
        {
            if (IsTeams(session)) continue;
            if (session.Quest is not { Length: > 0 } quest) continue;
            if (IsState(session, "stood-down")) continue;
            // A record its agent's refusal of the choice ended before any prompt ran nothing, so a carry-on carries on from
            // the record before it, never from the empty one.
            if (NotOffered(session)) continue;

            if (last.TryGetValue(quest, out var seen) && seen.At > session.Created) continue;
            last[quest] = (Prior(session), session.Created);
        }

        return last.ToDictionary(pair => pair.Key, pair => pair.Value.Session, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The newest record of this machine's on each quest where it stood down, newer by that alone than the quest's
    /// <see cref="LastRun"/>, which skips it: read so a look asks the claim of those quests.
    /// </summary>
    internal static IReadOnlyDictionary<string, PriorSession> StoodDown(IEnumerable<SessionResponse> listed)
    {
        var newest = new Dictionary<string, SessionResponse>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in listed)
        {
            if (IsTeams(session)) continue;
            if (session.Quest is not { Length: > 0 } quest) continue;

            if (newest.TryGetValue(quest, out var seen) && seen.Created > session.Created) continue;
            newest[quest] = session;
        }

        return newest
            .Where(pair => IsState(pair.Value, "stood-down"))
            .ToDictionary(pair => pair.Key, pair => Prior(pair.Value), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The record each taken quest's standing reopen names, where it is this machine's and not the quest's last record
    /// here (<paramref name="lastRun"/>). Nothing is read where no taken quest's reopen names such a session.
    /// </summary>
    internal static IReadOnlyDictionary<string, PriorSession> NamedByReopen(
        IEnumerable<SessionResponse> listed, IReadOnlyList<QuestView> quests, IReadOnlyDictionary<string, PriorSession> lastRun)
    {
        var named = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);
        var wanted = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var quest in quests)
        {
            // A teammate's session (origin/id) is never this machine's to go on with; the last one here is the planner's already.
            if (quest is not { Status: "Taken", Reopened.Session: { Length: > 0 } session } || session.Contains('/')
                || string.Equals(lastRun.GetValueOrDefault(quest.Id)?.Session, session, StringComparison.Ordinal))
            {
                continue;
            }

            if (!wanted.TryGetValue(session, out var naming)) wanted[session] = naming = [];
            naming.Add(quest.Id);
        }

        if (wanted.Count == 0) return named;

        foreach (var session in listed)
        {
            if (IsTeams(session)) continue;
            if (session.Id is not { } id || !wanted.TryGetValue(id, out var naming)) continue;

            var record = Prior(session);
            foreach (var quest in naming) named[quest] = record;
        }

        return named;
    }

    /// <summary>
    /// Whose each take is, for the quests whose claim is read: whether a record of this machine's, in any state, marks that it took
    /// the quest, and the newest teammate's record on it that did not stand down, which names another machine's take.
    /// </summary>
    /// <param name="claims">This machine's claim on each quest read, by quest id.</param>
    /// <param name="whys">Why a lost claim lost, by quest id, where the host said.</param>
    /// <param name="forwards">The bring-forward that made each take, by quest id, where the host names one.</param>
    /// <param name="taken">When each take happened, by quest id, where the host says.</param>
    internal static IReadOnlyDictionary<string, QuestTake> Takes(
        IEnumerable<SessionResponse> listed, IReadOnlyDictionary<string, string> claims, IReadOnlyDictionary<string, string?>? whys = null,
        IReadOnlyDictionary<string, ForwardTake?>? forwards = null, IReadOnlyDictionary<string, DateTimeOffset?>? taken = null)
    {
        var took = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var teammates = new Dictionary<string, (string Id, DateTimeOffset At)>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in listed)
        {
            if (session.Quest is not { Length: > 0 } quest) continue;
            if (!IsTeams(session))
            {
                if (session.Took) took.Add(quest);
                continue;
            }

            if (IsState(session, "stood-down")) continue;
            // The newest by when it is made, a later one in the answer on a tie, as the ledger orders them.
            if (teammates.TryGetValue(quest, out var seen) && seen.At > session.Created) continue;
            teammates[quest] = (session.Id!, session.Created);
        }

        return claims.ToDictionary(
            pair => pair.Key,
            pair => new QuestTake(pair.Value, took.Contains(pair.Key), teammates.TryGetValue(pair.Key, out var teammate) ? teammate.Id : null,
                whys is not null && whys.TryGetValue(pair.Key, out var why) ? why : null,
                forwards?.GetValueOrDefault(pair.Key), taken?.GetValueOrDefault(pair.Key)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>This machine's records by id, read as <see cref="Prior"/> reads one: what a brief follows a line's <c>from</c> links through.</summary>
    internal static IReadOnlyDictionary<string, PriorSession> Records(IEnumerable<SessionResponse> listed)
    {
        var records = new Dictionary<string, PriorSession>(StringComparer.Ordinal);
        foreach (var session in listed)
        {
            if (IsTeams(session) || session.Id is not { Length: > 0 } id) continue;
            records.TryAdd(id, Prior(session));
        }

        return records;
    }

    /// <summary>The record with this id, as a run that goes on reads it; a teammate's is none here.</summary>
    internal static PriorSession? Record(IEnumerable<SessionResponse> listed, string id)
    {
        foreach (var session in listed)
        {
            if (IsTeams(session)) continue;
            if (string.Equals(session.Id, id, StringComparison.Ordinal)) return Prior(session);
        }

        return null;
    }

    /// <summary>The trees of this machine's records the person's words wait on.</summary>
    internal static IReadOnlyList<string> WaitingTrees(IEnumerable<SessionResponse> listed)
    {
        var trees = new List<string>();
        foreach (var session in listed)
        {
            if (IsTeams(session)) continue;
            if (Prior(session) is { WordsWaiting: true, Tree: { Length: > 0 } tree }) trees.Add(tree);
        }

        return trees;
    }

    /// <summary>Every record's entry key by its id: this machine's and a teammate's mirror, keyed <c>origin/id</c>.</summary>
    internal static IReadOnlyDictionary<string, string> Entries(IEnumerable<SessionResponse> listed)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var session in listed)
        {
            if (session.Id is { Length: > 0 } id && session.Entry is { Length: > 0 } entry) entries[id] = entry;
        }

        return entries;
    }

    /// <summary>
    /// This machine's records as the goal's walk counts them: each one's adapter, account, when it opened, and whether
    /// it runs now, which is the active list's word. A record naming no adapter names no owner to count it for.
    /// </summary>
    internal static IReadOnlyList<SessionStarted> Started(IEnumerable<SessionResponse> listed, IReadOnlyList<SessionView> active)
    {
        var running = active.Select(session => session.Id).ToHashSet(StringComparer.Ordinal);
        var started = new List<SessionStarted>();
        foreach (var session in listed)
        {
            if (IsTeams(session) || session.Door is not { Length: > 0 } adapter) continue;
            started.Add(new SessionStarted(adapter, session.Profile, MomentOf(session.Created), running.Contains(session.Id ?? ""))
            {
                Id = session.Id,
                // A record its agent's refusal of the choice ended at the open, read by its end code.
                NotOffered = NotOffered(session),
            });
        }

        return started;
    }

    /// <summary>A record as what a later run on it is compared with and told.</summary>
    internal static PriorSession Prior(SessionResponse session) =>
        new(session.Id ?? "", session.Tree, session.State ?? "", session.Note, session.Repository, session.Interrupted)
        {
            // Which account it ran on and whether a limit cut it off: what a carry-on is compared with and told.
            Profile = session.Profile,
            Actor = session.Actor,
            Choice = session.Choice,
            Asked = Wired<ActorAsked>(session.Asked),
            Override = session.Override is { ValueKind: JsonValueKind.Object } over ? over : null,
            Ran = Wired<ActorRan>(session.Ran),
            Chosen = Wired<ActorChosen>(session.Chosen),
            Limit = session.Limit,
            NotOffered = NotOffered(session),
            // What an answered park is compared with, and where its evidence counts from.
            Adapter = session.Door,
            DoorVersion = session.DoorVersion,
            BaseCommit = session.BaseCommit,
            // The person's words waiting on it: what goes on with it, parked or ended.
            Said = session.Said is { } said ? [.. said.Select(ReadWord).OfType<SaidWordView>()] : null,
            Ask = session.Ask,
            // The work its marks read as: served on loopback, none for a mirror and from a host without the field.
            Work = session.Work is { Length: > 0 } work ? work : null,
            // What a helper may be asked of, and where its rule resolves: served on loopback with the record.
            Serves = session.Serves,
            // A partner's piece: served on loopback beside its serves.
            Piece = session.Piece is { Length: > 0 } piece ? piece : null,
            Opinion = session.Opinion,
            Decides = session.Decides is { Length: > 0 } decides ? decides : null,
            // The session it continues, why and who asked: served on loopback with the record, for the head to say.
            From = session.From,
            FromWhy = session.FromWhy,
            FromBy = session.FromBy,
            Workspace = session.Workspace,
            // A chat goes on through the chat runner, never the planner.
            Kind = session.Kind ?? "driven",
            // Its note's lines by code, which a note added to it carries on; null for a record without parts.
            NoteParts = PartsOf(session),
            // When it last moved, which a take here is compared with.
            Updated = MomentOf(session.Updated),
            // Its creation time, which a curve move's hold is counted from.
            Created = MomentOf(session.Created),
            // Its session entry and its quest: what a later quest of the same line goes into its tree by.
            Entry = session.Entry is { Length: > 0 } entry ? entry : null,
            Quest = session.Quest is { Length: > 0 } quest ? quest : null,
            TookQuest = session.TookQuest is { Length: > 0 } took ? took : null,
            // The mode an Ask Daoris record last ran under, which a conversation going on is compared with.
            HelpMode = session.HelpMode is { Length: > 0 } mode ? mode : null,
            // The setup a driven record started under, each pause that is a word: what a run going on from it compares its own with.
            Access = session.Access is { } access
                ? access.Where(pause => pause.Value is { Length: > 0 }).ToDictionary(pause => pause.Key, pause => pause.Value, StringComparer.Ordinal)
                : null,
        };

    /// <summary>An actor's provenance as the record carries it, read as stored; null where it is no object or does not read.</summary>
    internal static T? Wired<T>(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.Object } wired) return default;
        try { return JsonSerializer.Deserialize<T>(wired.GetRawText(), ActorWire.Json); }
        catch (JsonException) { return default; }
    }

    /// <summary>A raw record's actor provenance, for a reader that holds the record as JSON.</summary>
    internal static T? ActorRead<T>(JsonElement session, string name) =>
        session.TryGetProperty(name, out var value) ? Wired<T>(value) : default;

    /// <summary>The words waiting on a raw record; null where the record answers no list, which is not the same as nothing waiting.</summary>
    internal static IReadOnlyList<SaidWordView>? ReadSaid(JsonElement session)
    {
        if (!session.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array) return null;
        return [.. said.EnumerateArray().Select(ReadWord).OfType<SaidWordView>()];
    }

    /// <summary>
    /// One word of a raw record: its id, its words, when, its files' names and whether it is said after the record ended; and the
    /// second opinion whose findings it carries (<c>by</c>). Null for one without its id, its words or a moment that reads.
    /// </summary>
    internal static SaidWordView? ReadWord(JsonElement word) =>
        word.ValueKind == JsonValueKind.Object
        && Text(word, "id") is { Length: > 0 } id && Text(word, "text") is { } words && Moments.Read(word, "at") is { } at
            ? ReadWord(new SaidWordResponse(
                id, words, at, Json.Strings(word, "files", keepEmpty: true), Flag(word, "reopens"), Text(word, "by"), Text(word, "helper"), Text(word, "from"),
                Text(word, "key"), Text(word, "level"), Moments.Read(word, "attempt"), Text(word, "decision")))
            : null;

    /// <summary>
    /// A word as the typed answer carries it: the marks that say whose it is are absent when blank, a helper's answer is its own mark (never
    /// <c>by</c>), and a host without a mark says none, so its word is the person's.
    /// </summary>
    internal static SaidWordView? ReadWord(SaidWordResponse word) =>
        word.Id is { Length: > 0 } id && word.Text is { } words && word.At != default
            ? new SaidWordView(id, words, word.At, word.Files ?? [], word.Reopens)
            {
                By = word.By is { Length: > 0 } by ? by : null,
                Helper = word.Helper is { Length: > 0 } helper ? helper : null,
                From = word.From is { Length: > 0 } from ? from : null,
                Key = word.Key is { Length: > 0 } key ? key : null,
                Level = MessageLevels.Read(word.Level),
                Attempt = word.Attempt,
                Decision = word.Decision is { Length: > 0 } decision ? decision : null,
            }
            : null;

    /// <summary>
    /// The closed quests whose last session here has the person's words waiting: an open one is planned from the open list,
    /// and one with nothing waiting is not looked at.
    /// </summary>
    /// <param name="open">The ids of the quests the open list holds.</param>
    internal static IReadOnlyList<QuestView> Closed(
        IEnumerable<QuestView> quests, IReadOnlyDictionary<string, PriorSession> lastRun, IReadOnlyCollection<string> open)
    {
        var listed = new HashSet<string>(open, StringComparer.OrdinalIgnoreCase);
        return [.. quests.Where(quest => !listed.Contains(quest.Id) && lastRun.TryGetValue(quest.Id, out var last) && last.WordsWaiting)];
    }
}
