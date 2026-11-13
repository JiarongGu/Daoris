using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A session's structure, kept on the machine: <c>&lt;id&gt;.events.jsonl</c> beside the
/// verbatim transcript, one event per line.
/// </summary>
/// <remarks>
/// <para><b>Transcript-class.</b> What a session said and did can carry machine paths and a
/// repository's code, so it never leaves the machine: no HTTP surface reads this, only the
/// shell's bridge and the headless host's own terminal.</para>
///
/// <para><b>The file is the record; the event is the window's.</b> Every append writes the line
/// before telling anyone, so a subscriber that fails costs its own view, never the record — the same
/// order the console keeps with its transcript.</para>
///
/// <para><b>Bounded per line.</b> A tool that printed a megabyte would make the record unreadable a
/// page at a time, so large fields are cut, and the cut says how long the original is.</para>
/// </remarks>
public sealed class SessionEvents(string directory) : IWorkplaceEventHistory
{
    IReadOnlyList<SessionEvent> ISessionEventHistory.ReadAll(string sessionId) => After(sessionId, 0).Events;

    bool IWorkplaceEventHistory.Exists(string sessionId) => File.Exists(PathOf(sessionId));

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // Shared by every instance: callers each build their own over one folder, so a gate or a numbering of one instance's own would let two of
    // them write the same number. The file's length says whether another writer, in this process or another, has moved the record on.
    private static readonly object _gate = new();
    private static readonly Dictionary<string, (long Seq, long Length)> _latest = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raised for every event, after it is written. The shell turns it into its IPC event; a handler
    /// that throws costs its own view, never the record or the session.
    /// </summary>
    public event Action<string, SessionEvent>? Evented;

    /// <summary>Record one event, numbered and stamped, and tell whoever is watching.</summary>
    public SessionEvent Append(string sessionId, SessionEvent e)
    {
        var path = PathOf(sessionId);
        SessionEvent stamped;
        lock (_gate)
        {
            var length = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (!_latest.TryGetValue(path, out var seen) || seen.Length != length) seen = (Last(path), length);
            stamped = SessionEventText.Bound(e) with
            {
                Seq = seen.Seq + 1,
                At = e.At == default ? DateTimeOffset.UtcNow : e.At,
            };

            Directory.CreateDirectory(directory);
            // 🔴 Shared both ways, reads and writes: a page reads this record while a session appends to
            // it, and a writer or reader that denied the other failed with a sharing violation — which
            // the caller, rightly unwilling to fail a session over its record, turned into a lost event
            // (an agent's answer missing from a turn that had ended).
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            using (var writer = new StreamWriter(stream, Utf8))
            {
                // A last line a crash tore has no newline: the event starts its own line rather than joining it and being lost with it.
                writer.Write((length > 0 && !EndsInNewline(path) ? "\n" : "") + JsonSerializer.Serialize(stamped, SessionEventText.Json) + "\n");
            }

            _latest[path] = (stamped.Seq, new FileInfo(path).Length);
        }

        try
        {
            Evented?.Invoke(sessionId, stamped);
        }
        catch (Exception)
        {
            // A watcher's failure is its own: the event is already in the record.
        }

        return stamped;
    }

    /// <summary>
    /// Record one event, or say why it could not be kept — never throwing. A record that cannot be
    /// written costs its caller a console line, never the session: the conversation enriches a run, it
    /// does not run it. Four callers had written this guard for themselves.
    /// </summary>
    /// <param name="say">Where the reason goes — the session's console — or null to drop it.</param>
    public void Keep(string sessionId, SessionEvent e, Action<string>? say)
    {
        try
        {
            Append(sessionId, e);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            say?.Invoke($"[the conversation record could not keep an event: {error.Message}]");
        }
    }

    /// <summary>
    /// The newest <paramref name="limit"/> events, or those before <paramref name="before"/> — how a
    /// page opens a session, and how it loads earlier ones.
    /// </summary>
    public EventPage Page(string sessionId, long? before = null, int limit = SessionEventText.PageLimit)
    {
        if (Headed(sessionId) is { } head) return SessionEventHeadedReads.Page(PathOf(sessionId), head, before, Math.Clamp(limit, 1, SessionEventText.MaxPage));
        var all = Read(PathOf(sessionId));
        var latest = all.Count == 0 ? 0 : all[^1].Seq;
        var older = before is { } b ? all.Where(e => e.Seq < b).ToList() : all;
        var take = Math.Clamp(limit, 1, SessionEventText.MaxPage);
        var page = older.Skip(Math.Max(0, older.Count - take)).ToList();
        var opening = all.FirstOrDefault(e => e.Kind == SessionEventKind.User);
        var failed = all.FirstOrDefault(e => e.Kind == SessionEventKind.Tool && e.Status == "failed" && e.Id is not null);
        return new EventPage(page, older.Count > page.Count, latest)
        {
            Opening = opening is not null && page.Count > 0 && opening.Seq < page[0].Seq ? opening : null,
            FirstFailure = failed is null ? null : all.First(e => e.Kind == SessionEventKind.Tool && e.Id == failed.Id).Seq,
        };
    }

    /// <summary>Everything newer than <paramref name="after"/> — how a page that missed a batch closes the gap.</summary>
    public EventPage After(string sessionId, long after)
    {
        if (after > 0 && Headed(sessionId) is { } head) return SessionEventHeadedReads.After(PathOf(sessionId), head, after);
        var all = Read(PathOf(sessionId));
        return new EventPage([.. all.Where(e => e.Seq > after)], Earlier: false, all.Count == 0 ? 0 : all[^1].Seq);
    }

    /// <summary>How long a session's opening may be before it is cut: a row's title, not a paragraph.</summary>
    public const int OpeningLimit = 120;

    /// <summary>A search's hits from one session, at most — the rest of that session is one press away.</summary>
    public const int HitsPerSession = 3;

    /// <summary>
    /// A search page's sessions when the caller names no limit: the rail's old page, whole sessions that fit its 50 hits
    /// (<see cref="HitsPerSession"/> each) as a page is a page of sessions.
    /// </summary>
    public const int SearchSessions = 16;

    /// <summary>The most sessions one search page holds, as <c>help list</c>'s page does (ASKHIST1d1).</summary>
    public const int SearchPageLimit = 200;

    /// <summary>
    /// What a person first said in each of these sessions — a conversation's identity:
    /// its first line, cut to a title's length.
    /// </summary>
    /// <remarks>
    /// Machine-local, like the record it is read from: what a session said never rides the
    /// session record, which travels. A driven session's composed target is not the person speaking, so
    /// it has no opening, and the person's words to it later, shown waiting or taken, do not make it one;
    /// nor has a session with no record here, nor an id that is not one.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Openings(IEnumerable<string> sessionIds)
    {
        var openings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in sessionIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!SessionEventText.IsId(id)) continue;
            var path = Path.Combine(directory, $"{id}.events.jsonl");
            // Only as far as its first prompt, which is usually the file's first line, and only where the person said it. Words
            // shown waiting are no prompt, and a driven session the person wrote to later is still opened by its target.
            // A handed-over conversation's brief is no one's words: it is named by what the person said after it.
            var asked = Lines(path).FirstOrDefault(Asked);
            if (asked?.Origin != "person" || asked.Text is not { } text) continue;

            var first = text.Split('\n', 2)[0].Trim();
            if (first.Length == 0) continue;
            openings[id] = TextBounds.Clip(first, OpeningLimit);
        }

        return openings;
    }

    /// <summary>
    /// The brief each of these sessions' starts is given (<see cref="StartBrief"/>), bounded and with its cut said: the first
    /// event of the part that continues another. Machine-local as <see cref="Openings"/> is; a session whose start carried none has none.
    /// </summary>
    public IReadOnlyDictionary<string, string> Briefs(IEnumerable<string> sessionIds)
    {
        var briefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in sessionIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!SessionEventText.IsId(id)) continue;
            var brief = Lines(Path.Combine(directory, $"{id}.events.jsonl")).Select(StartBrief.Of).FirstOrDefault(said => said is not null);
            if (brief is not null) briefs[id] = brief;
        }

        return briefs;
    }

    /// <summary>
    /// What the person first said in a session, whole: the request a conversation handed to another agent is told
    /// of. Null where the person said none (a driven session's composed target is not them), no record, or an id that is not one.
    /// </summary>
    public string? FirstAsked(string sessionId)
    {
        if (!SessionEventText.IsId(sessionId)) return null;
        var asked = Lines(Path.Combine(directory, $"{sessionId}.events.jsonl")).FirstOrDefault(Asked);
        return asked?.Origin == "person" ? asked.Text : null;
    }

    /// <summary>A session's ask: words it took, never words shown waiting, nor the brief a new part is handed.</summary>
    private static bool Asked(SessionEvent e) => e.Kind == SessionEventKind.User && e.Reaches is null && e.Continues is null;

    /// <summary>
    /// The door the first of these words is said at, read from the event that showed it waiting: what the
    /// machine log's <c>session.reopened</c> names. Null where no such event names one: words said before doors were kept,
    /// a door that wrote no record, no record, or an id that is not one.
    /// </summary>
    public string? DoorOf(string sessionId, IReadOnlyCollection<string> words)
    {
        if (!SessionEventText.IsId(sessionId) || words.Count == 0) return null;
        var wanted = new HashSet<string>(words, StringComparer.Ordinal);
        try
        {
            return Lines(PathOf(sessionId)).FirstOrDefault(e =>
                e.Kind == SessionEventKind.User && e.Reaches is not null && e.Door is { Length: > 0 }
                && e.Id is { } id && wanted.Contains(id))?.Door;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The sessions whose words hold <paramref name="query"/>: the person's and the agent's,
    /// each with a snippet around the match, newest sessions first.
    /// </summary>
    /// <remarks>
    /// <para><b>An agent's message is searched whole.</b> It is streamed in chunks and kept as them, so a
    /// word can straddle two lines of this record; the chunks are joined, as the page joins them, before
    /// anything is matched.</para>
    ///
    /// <para><b>What is said, not what a tool printed</b>: a tool's output is a file's contents or a
    /// command's noise, and would bury the conversation that mentioned it.</para>
    ///
    /// <para><b>Bounded, and it says so</b> (<see cref="SessionSearch.Cut"/>): a few hits per session, and a page
    /// of sessions (<c>offset</c>, <c>limit</c>) with the total and where the next starts. Every record is read, newest first, and the query is two characters or one Han
    /// character (<see cref="SessionEventText.Searchable"/>). Machine-local, like the record.</para>
    /// </remarks>
    public SessionSearch Search(string query, int offset = 0, int limit = SearchSessions)
    {
        var wanted = query.Trim();
        if (!SessionEventText.Searchable(wanted) || !Directory.Exists(directory)) return new([], false);

        offset = Math.Max(offset, 0);
        limit = limit is < 1 or > SearchPageLimit ? SearchPageLimit : limit;
        var records = new DirectoryInfo(directory).EnumerateFiles("*.events.jsonl")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();
        var cut = false;
        var hits = new List<SessionHit>();
        var total = 0;

        // Every record is read, so the total counts what the page leaves out; a session outside the page is read
        // only as far as its first hit, and one on the page up to a fourth, which is how a page knows it cut a session.
        foreach (var record in records)
        {
            var onPage = total >= offset && total < offset + limit;
            var session = record.Name[..^".events.jsonl".Length];
            var found = 0;
            foreach (var (seq, kind, text) in Passages(record.FullName))
            {
                var at = text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                if (!onPage)
                {
                    found++;
                    break;
                }

                if (found == HitsPerSession)
                {
                    cut = true;
                    break;
                }

                hits.Add(new SessionHit(session, seq, kind, SessionEventText.Snippet(text, at, wanted.Length)));
                found++;
            }

            if (found > 0) total++;
        }

        int? next = offset + limit < total ? offset + limit : null;
        // Cut is anything left out: a session's fourth hit, or sessions past this page.
        return new SessionSearch(hits, cut || next is not null, total, next);
    }

    /// <summary>A search within one session's record, at most — a long run's every mention of a word.</summary>
    public const int WithinLimit = 100;

    /// <summary>
    /// One session's record searched: what is said, and each call by its title, in the order
    /// they came — a hit's sequence is where its message or its call began, the block the page draws.
    /// </summary>
    public SessionSearch Within(string sessionId, string query, int limit = WithinLimit)
    {
        var wanted = query.Trim();
        var path = PathOf(sessionId);
        if (!SessionEventText.Searchable(wanted) || !File.Exists(path)) return new([], false);

        var hits = new List<SessionHit>();
        var cut = false;
        foreach (var (seq, kind, text, at) in Holding(path, wanted))
        {
            if (hits.Count == limit)
            {
                cut = true;
                break;
            }

            hits.Add(new SessionHit(sessionId, seq, kind, SessionEventText.Snippet(text, at, wanted.Length)));
        }

        return new SessionSearch(hits, cut, hits.Count > 0 ? 1 : 0);
    }

    /// <summary>
    /// Where one session's record first holds these words, as <see cref="Within"/> finds it: the hit, with its
    /// snippet, and the whole passage it is in with where the words start, for a reader that cuts its own view of it. Null where
    /// the record does not hold them, there is no record, or the words are too few to search with.
    /// </summary>
    public (SessionHit Hit, string Passage, int At)? FirstFound(string sessionId, string query)
    {
        var wanted = query.Trim();
        var path = PathOf(sessionId);
        if (!SessionEventText.Searchable(wanted) || !File.Exists(path)) return null;

        foreach (var (seq, kind, text, at) in Holding(path, wanted))
        {
            return (new SessionHit(sessionId, seq, kind, SessionEventText.Snippet(text, at, wanted.Length)), text, at);
        }

        return null;
    }

    /// <summary>What is said, and each call by its title, that holds the words, in the order they came, with where they start.</summary>
    private static IEnumerable<(long Seq, string Kind, string Text, int At)> Holding(string path, string wanted) =>
        Passages(path).Concat(Calls(path))
            .OrderBy(passage => passage.Seq)
            .Select(passage => (passage.Seq, passage.Kind, passage.Text, At: passage.Text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase)))
            .Where(passage => passage.At >= 0);

    /// <summary>
    /// The agent's last message, whole: what a session that parks to ask the person said to them,
    /// joined from its chunks as the page joins them. Null when the person spoke after it (that turn said
    /// nothing), and for no record, an unreadable one, or an id that is not one.
    /// </summary>
    /// <remarks>
    /// The transcript is console lines, where an indented line is a tool's output; a question's own
    /// indented list read as that, and the card kept only what came after it.
    /// </remarks>
    public string? LastSaid(string sessionId)
    {
        if (!SessionEventText.IsId(sessionId)) return null;
        try
        {
            string? said = null;
            foreach (var (_, kind, text) in Passages(PathOf(sessionId)))
            {
                said = kind == SessionEventKind.Message ? text : null;
            }

            return said?.Trim() is { Length: > 0 } words ? words : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// What is said in a session, as a reader reads it: each thing the person said and each of the agent's messages,
    /// joined from its chunks, in order, and never a tool's output nor the driver's notes; and when its record is last written.
    /// Nothing for no record, an unreadable one, or an id that is not one.
    /// </summary>
    public (IReadOnlyList<(string Kind, string Text)> Said, DateTimeOffset? Last) Spoken(string sessionId)
    {
        if (!SessionEventText.IsId(sessionId)) return ([], null);
        try
        {
            var path = PathOf(sessionId);
            if (!File.Exists(path)) return ([], null);
            var said = Passages(path).Select(passage => (passage.Kind, passage.Text)).ToList();
            DateTimeOffset? last = null;
            foreach (var e in Lines(path)) last = e.At;
            return (said, last);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return ([], null);
        }
    }

    /// <summary>
    /// When a session's record is last written: its last readable event's time, as <see cref="Spoken"/> reads it, but
    /// read from the file's end, so a list that orders every conversation by it reads a line of each rather than the whole of
    /// each. Null for no record, an unreadable one, one with no readable event, or an id that is not one.
    /// </summary>
    public DateTimeOffset? LastAt(string sessionId) => Newest<DateTimeOffset>(sessionId, e => e.At);

    /// <summary>
    /// When the person last spoke to a session: its newest <c>user</c> event of origin <c>person</c>, read from the file's end.
    /// Never Daoris's, a helper's or the supervisor's word. Null for none, no record, an unreadable one, or an id that is not one.
    /// </summary>
    public DateTimeOffset? LastPersonAt(string sessionId) =>
        Newest<DateTimeOffset>(sessionId, e => e is { Kind: SessionEventKind.User, Origin: "person" } ? e.At : null);

    /// <summary>
    /// When its record last shows words handed to its agent: the newest prompt Daoris composed (a
    /// <c>user</c> event of origin <c>target</c>), word kept under an id (a <c>user</c> event with one), or word written to its harness (a
    /// <c>sent</c> receipt), read from the file's end. Never the protocol door's echo of the prompt it is handed, which carries no id
    /// and says nothing new is handed. What a question it asked must come after to be its last turn's. Null for
    /// none, no record, an unreadable one, or an id that is not one.
    /// </summary>
    public DateTimeOffset? LastHandedAt(string sessionId) => Newest<DateTimeOffset>(sessionId, e =>
        (e.Kind == SessionEventKind.User && (e.Id is { Length: > 0 } || e.Origin == "target"))
        || (e.Kind == SessionEventKind.Receipt && e.Receipt == Receipts.Sent)
            ? e.At
            : null);

    /// <summary>
    /// The answer each word is pressed as, by the word's id: the <see cref="SessionEvent.Choice"/> the
    /// person's word is shown under as it is kept. Empty for none, no record, an unreadable one, or an id that is not one.
    /// </summary>
    public IReadOnlyDictionary<string, string> ChoicesOf(string sessionId)
    {
        var choices = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!SessionEventText.IsId(sessionId)) return choices;
        try
        {
            var path = PathOf(sessionId);
            if (!File.Exists(path)) return choices;
            if (Headed(sessionId) is { } head) return new Dictionary<string, string>(head.Choices, StringComparer.Ordinal);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Utf8);
            while (reader.ReadLine() is { } line)
            {
                if (Event(line) is { Kind: SessionEventKind.User, Id: { Length: > 0 } id, Choice: { Length: > 0 } choice }) choices[id] = choice;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }

        return choices;
    }

    /// <summary>
    /// Whether a conversation's last turn has ended with nothing said since, read from its record's end as
    /// <see cref="LastAt"/> reads it: the newest event that says where its turns stand is a turn's end. A word said after it, or the
    /// agent's words, a call, its reasoning or a plan (a turn its harness woke by itself), is a turn under way; a note, a usage or
    /// anything this build has no kind for says nothing of a turn. False for no turn yet, no record, an unreadable one, or an id
    /// that is not one. What a process that does not hold the conversation reads in place of its runner's word.
    /// </summary>
    public bool TurnEnded(string sessionId) => Newest<bool>(sessionId, e => e.Kind switch
    {
        SessionEventKind.Turn => true,
        SessionEventKind.User or SessionEventKind.Message or SessionEventKind.Thought or SessionEventKind.Tool or SessionEventKind.Plan => false,
        _ => (bool?)null,
    }) ?? false;

    /// <summary>The newest readable event's answer to <paramref name="read"/>, read from the file's end (a line of each record, never the
    /// whole), one answered null passed over; null for no record, an unreadable one, none answered, or an id that is not one.</summary>
    private T? Newest<T>(string sessionId, Func<SessionEvent, T?> read) where T : struct
    {
        if (!SessionEventText.IsId(sessionId)) return null;
        try
        {
            return NewestIn(PathOf(sessionId), read);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static T? NewestIn<T>(string path, Func<SessionEvent, T?> read) where T : struct
    {
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = stream.Length;
        // A window from the end, grown until it holds a line answered for: a line longer than the window is read in a later one.
        for (var window = 4096L; ; window *= 4)
        {
            var start = Math.Max(0, length - window);
            var bytes = new byte[length - start];
            stream.Seek(start, SeekOrigin.Begin);
            stream.ReadExactly(bytes);
            var lines = Utf8.GetString(bytes).Split('\n');

            // A window's first line is part of one unless the window starts the file.
            for (var at = lines.Length - 1; at >= (start == 0 ? 0 : 1); at--)
            {
                if (Event(lines[at]) is { } e && read(e) is { } answered) return answered;
            }

            if (start == 0) return null;
        }
    }

    /// <summary>
    /// The agent's plan as the record last kept it: the newest plan event's entries, whole, since a
    /// plan is written whole each time it changes. Empty for no plan, no record, an unreadable one, or an id that is not
    /// one.
    /// </summary>
    public IReadOnlyList<PlanEntry> LastPlan(string sessionId)
    {
        if (!SessionEventText.IsId(sessionId)) return [];
        try
        {
            if (Headed(sessionId) is { } head) return SessionEventHeadedReads.Plan(PathOf(sessionId), head);
            IReadOnlyList<PlanEntry> plan = [];
            foreach (var e in Lines(PathOf(sessionId)))
            {
                if (e.Kind == SessionEventKind.Plan && e.Entries is { } entries) plan = entries;
            }

            return plan;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// What a session spent, by its record (<see cref="SessionSpentKept"/>): its turns ended, those whose door said what they consumed and
    /// the four counts summed, a part's cost on the *Parts* strip's own row. None for no record, an unreadable one, or no id.
    /// </summary>
    public SessionSpent Spent(string sessionId)
    {
        if (!SessionEventText.IsId(sessionId)) return SessionSpent.None;
        try
        {
            return SessionSpentKept.Shared.Of(PathOf(sessionId));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return SessionSpent.None;
        }
    }

    /// <summary>
    /// The turns a session's record says ended — what a refused turn is counted after. Unreadable is none. The
    /// record's, read by a driven start and a conversation alike, so not the loop's.
    /// </summary>
    internal long TurnsEnded(string sessionId)
    {
        try
        {
            if (SessionEventText.IsId(sessionId) && Headed(sessionId) is { } head) return head.Turns;
            return After(sessionId, 0).Events.LongCount(e => e.Kind == SessionEventKind.Turn);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return 0;
        }
    }

    /// <summary>Each call once, by the title its updates last gave it, at the sequence where it began.</summary>
    private static IEnumerable<(long Seq, string Kind, string Text)> Calls(string path)
    {
        var calls = new Dictionary<string, (long Seq, string? Title)>(StringComparer.Ordinal);
        foreach (var e in Lines(path))
        {
            if (e.Kind != SessionEventKind.Tool || e.Id is not { } id) continue;
            calls[id] = calls.TryGetValue(id, out var known) ? (known.Seq, e.Title ?? known.Title) : (e.Seq, e.Title);
        }

        return calls.Values.Where(call => call.Title is { Length: > 0 }).Select(call => (call.Seq, SessionEventKind.Tool, call.Title!));
    }

    /// <summary>
    /// A record's words as a reader reads them: each thing the person said, and each run of the agent's
    /// chunks joined into one message — broken, as the page breaks it, by anything but a usage reading.
    /// </summary>
    private static IEnumerable<(long Seq, string Kind, string Text)> Passages(string path)
    {
        (long Seq, System.Text.StringBuilder Text)? message = null;
        foreach (var e in Lines(path))
        {
            if (e.Kind == SessionEventKind.Usage) continue;
            if (e.Kind == SessionEventKind.Message)
            {
                message ??= (e.Seq, new System.Text.StringBuilder());
                message.Value.Text.Append(e.Text);
                continue;
            }

            if (message is { } run)
            {
                yield return (run.Seq, SessionEventKind.Message, run.Text.ToString());
                message = null;
            }

            // The words a driven session is told while it worked are found once, where it took them: the event
            // that showed them waiting carries `Reaches`, and the same words follow it.
            if (e.Kind == SessionEventKind.User && e.Reaches is null && e.Text is { Length: > 0 } said) yield return (e.Seq, e.Kind, said);
        }

        if (message is { } last) yield return (last.Seq, SessionEventKind.Message, last.Text.ToString());
    }

    /// <summary>
    /// Remove a session's kept conversation, once its record is deleted, and forget where its numbering
    /// stood. True when there is a file to remove; an id that names no session removes nothing.
    /// </summary>
    public bool Forget(string sessionId)
    {
        if (!SessionEventText.IsId(sessionId)) return false;
        lock (_gate)
        {
            var path = PathOf(sessionId);
            _latest.Remove(path);
            SessionSpentKept.Shared.Forget(path);
            SessionEventHeads.In(directory)?.Forget(sessionId, path);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    /// <summary>Where a session's events are kept — refused for anything that is not an id.</summary>
    public string PathOf(string sessionId)
    {
        if (!SessionEventText.IsId(sessionId))
        {
            throw new DriverException($"`{sessionId}` is not a session id, so it names no record.");
        }

        return Path.Combine(directory, $"{sessionId}.events.jsonl");
    }

    /// <summary>The head standing for a long record's file (<see cref="SessionEventHeads"/>), or null where the file is read whole.</summary>
    private SessionEventHead? Headed(string sessionId) => SessionEventHeads.In(directory)?.Of(sessionId, PathOf(sessionId));

    private static bool EndsInNewline(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length == 0) return true;
        stream.Seek(-1, SeekOrigin.End);
        return stream.ReadByte() == '\n';
    }

    private static long Last(string path) => NewestIn(path, e => (long?)e.Seq) ?? 0;

    /// <summary>Every readable event in a file, in order; a line that is not one is skipped.</summary>
    private static List<SessionEvent> Read(string path) => [.. Lines(path)];

    /// <summary>
    /// A file's readable events, one at a time — so a question answered near the top (an opening) reads
    /// no further than it has to.
    /// </summary>
    private static IEnumerable<SessionEvent> Lines(string path)
    {
        if (!File.Exists(path)) yield break;

        // Read beside a writer, never against it (see Append): a line still being written is torn, and a
        // torn line costs itself below.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Utf8);
        while (reader.ReadLine() is { } line)
        {
            if (Event(line) is { } e) yield return e;
        }
    }

    /// <summary>One line of a record read as its event, or null for a blank, torn or foreign line.</summary>
    internal static SessionEvent? Event(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            return JsonSerializer.Deserialize<SessionEvent>(line.TrimEnd('\r'), SessionEventText.Json) is { Kind.Length: > 0 } e ? e : null;
        }
        catch (JsonException)
        {
            // A torn or foreign line costs itself, never the conversation around it.
            return null;
        }
    }
}
