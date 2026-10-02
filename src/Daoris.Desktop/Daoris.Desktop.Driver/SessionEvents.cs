using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The kinds a <see cref="SessionEvent"/> can be — Daoris's own vocabulary (D76 §1).</summary>
/// <remarks>
/// Small on purpose. Each door maps its own wire into these (ACP's <c>session/update</c> here, Claude
/// Code's <c>stream-json</c> in its adapter), so the page learns one model and never a harness's.
/// </remarks>
public static class SessionEventKind
{
    /// <summary>What was asked: the person's message, or the target the driver composed.</summary>
    public const string User = "user";

    /// <summary>The agent's words — a chunk; consecutive chunks are one message.</summary>
    public const string Message = "message";

    /// <summary>The agent's reasoning, where the wire carries it — a chunk, like a message.</summary>
    public const string Thought = "thought";

    /// <summary>A tool call, or an update to one: the same <see cref="SessionEvent.Id"/> merges them.</summary>
    public const string Tool = "tool";

    /// <summary>The agent's plan, whole each time it changes.</summary>
    public const string Plan = "plan";

    /// <summary>Context pressure as the harness reported it (TOOL3) — never computed here.</summary>
    public const string Usage = "usage";

    /// <summary>A turn ended, with the wire's stop reason: a self-report, never the record's conclusion.</summary>
    public const string Turn = "turn";

    /// <summary>The driver's own sentence about this session — a refusal, a missing connector.</summary>
    public const string Note = "note";

    /// <summary>Something the wire said that this build has no kind for — kept, never dropped.</summary>
    public const string Raw = "raw";
}

/// <summary>What a tool call carries: text, a diff, or a terminal, in ACP's own three shapes.</summary>
public sealed record ToolContent(
    string Type, string? Text = null, string? Path = null, string? OldText = null, string? NewText = null);

/// <summary>One line of the agent's plan.</summary>
public sealed record PlanEntry(string Content, string? Status = null, string? Priority = null);

/// <summary>
/// What one turn consumed, as the harness reported it for the whole turn (CONV5): new input, output,
/// input read from its cache, and input written to it. Each is null where the wire did not say — never
/// zero (TOOL3).
/// </summary>
/// <remarks>
/// Counts only. The total is their sum and is not kept twice; the cost and the models' names stay on
/// the wire (D24, TOOL3). Measured on both doors: docs/2026-09-25-stream-json-evidence.md, § CONV5.
/// </remarks>
public sealed record TurnTokens(long? Input, long? Output, long? CacheRead, long? CacheWrite)
{
    /// <summary>
    /// Four counts from a wire's usage object, by that wire's own names — or null when it named none, or
    /// named every one as zero.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>All zero is no report</b> (seen on the window, CONV5): <c>claude-code-acp</c> tallies a turn
    /// at its <c>result</c>, so a turn cancelled before one answers with every count zero, after reading
    /// the whole context. No turn that ran read nothing, and a zero here would claim one did.
    /// </remarks>
    public static TurnTokens? Read(
        JsonElement usage, string input, string output, string cacheRead, string cacheWrite)
    {
        if (usage.ValueKind != JsonValueKind.Object) return null;
        long? Count(string name) =>
            usage.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n)
                ? n
                : null;
        var tokens = new TurnTokens(Count(input), Count(output), Count(cacheRead), Count(cacheWrite));
        long?[] counts = [tokens.Input, tokens.Output, tokens.CacheRead, tokens.CacheWrite];
        return counts.All(count => count is null or 0) ? null : tokens;
    }
}

/// <summary>
/// One thing a session did, in Daoris's vocabulary (D76 §1): what the page renders a conversation
/// from, live and after a restart.
/// </summary>
/// <remarks>
/// Every field but <see cref="Kind"/> is optional, because each kind uses a few and the record is
/// written as JSON with the absent ones left out. <see cref="Seq"/> and <see cref="At"/> are stamped by
/// <see cref="SessionEvents"/>, never by a door.
/// </remarks>
public sealed record SessionEvent
{
    /// <summary>Monotonic within a session, from 1 — how a page asks for what it has not seen.</summary>
    public long Seq { get; init; }

    public DateTimeOffset At { get; init; }

    public required string Kind { get; init; }

    /// <summary>
    /// A tool call's id: its later updates carry the same one. For the person's words to a driven session (STEER1, D136):
    /// the id that pairs the words shown while they wait (<see cref="Reaches"/>) with the same words where it took them.
    /// </summary>
    public string? Id { get; init; }

    public string? Text { get; init; }

    /// <summary>For <see cref="SessionEventKind.User"/>: <c>person</c>, or <c>target</c> for the driver's composed prompt.</summary>
    public string? Origin { get; init; }

    /// <summary>
    /// For the person's words to a driven session, on the event that shows them the moment they are said (STEER1, D136):
    /// when they reach it, <c>next-step</c> or <c>turn-end</c>. The words where the session took them come again without
    /// it, under the same <see cref="Id"/>, and that one is the turn's ask.
    /// </summary>
    public string? Reaches { get; init; }

    /// <summary>
    /// For <see cref="SessionEventKind.User"/>: the names of the files the person attached (CONV4c) —
    /// names, never the kept paths or the lines Daoris added to reach them.
    /// </summary>
    public IReadOnlyList<string>? Files { get; init; }

    public string? Title { get; init; }

    /// <summary>ACP's tool kind — read, edit, delete, move, search, execute, think, fetch, other.</summary>
    public string? ToolKind { get; init; }

    public string? Status { get; init; }

    /// <summary>
    /// For a refused call (<see cref="Status"/> <c>refused</c>): the tool's own name, where the wire named one
    /// (UNBLOCK5). The native door's refusal carries it (<c>tool_name</c>, such as <c>Bash</c>). The protocol
    /// door's wire names a call's kind and title and never its tool, so its refusals carry none.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// For a refused call: what decided it, in the wire's own word where it gave one (UNBLOCK5). The native
    /// door's refusal carries <c>decision_reason_type</c> (<c>rule</c>, <c>mode</c>, <c>classifier</c>,
    /// <c>asyncAgent</c>), never the reason's sentence. Null where the wire said nothing, as the protocol
    /// door's permission request does not.
    /// </summary>
    public string? RefusedBy { get; init; }

    /// <summary>The files a tool call touched, as the wire named them.</summary>
    public IReadOnlyList<string>? Locations { get; init; }

    /// <summary>
    /// The line the first of <see cref="Locations"/> names, where the wire carried one (ACP's
    /// <c>locations[].line</c>, LEFT2): the file a card opens, at the place the adapter says. In the adapter's own
    /// terms, never inferred; the native door carries none.
    /// </summary>
    public long? Line { get; init; }

    public IReadOnlyList<ToolContent>? Content { get; init; }

    /// <summary>A tool call's input as the wire carried it, compact and bounded.</summary>
    public string? Input { get; init; }

    /// <summary>A tool call's raw output, compact and bounded.</summary>
    public string? Output { get; init; }

    /// <summary>
    /// For a card that stands for something the session runs beside itself (CONSOLE2): the stream its
    /// own console is kept under, as <see cref="SessionStream.Id"/> names it.
    /// </summary>
    public string? Stream { get; init; }

    public IReadOnlyList<PlanEntry>? Entries { get; init; }

    public long? Used { get; init; }

    public long? Size { get; init; }

    public string? StopReason { get; init; }

    /// <summary>For <see cref="SessionEventKind.Turn"/>: what the turn consumed, where the wire said (CONV5).</summary>
    public TurnTokens? Tokens { get; init; }

    /// <summary>What an unknown or unreadable frame said, compact and bounded.</summary>
    public string? Raw { get; init; }
}

/// <summary>
/// Where a search found its words (RAIL1): the session, the event its passage began at, whose words
/// they were (<see cref="SessionEventKind.User"/> or <see cref="SessionEventKind.Message"/>), and a
/// window of the text around the match.
/// </summary>
public sealed record SessionHit(string Session, long Seq, string Kind, string Snippet);

/// <summary>What a search found, and whether its bounds left anything out.</summary>
public sealed record SessionSearch(IReadOnlyList<SessionHit> Hits, bool Cut);

/// <summary>One page of a session's events, oldest first.</summary>
/// <param name="Earlier">Whether events older than this page exist — what "load earlier" asks after.</param>
/// <param name="Latest">The newest sequence the session has — what a live page merges after.</param>
public sealed record EventPage(IReadOnlyList<SessionEvent> Events, bool Earlier, long Latest)
{
    /// <summary>
    /// What the session was first asked, where this page does not hold it (SESS1): a long run is read
    /// from its ask, and the newest page of one is hundreds of events past it. Null when the page holds
    /// it, or nothing was asked.
    /// </summary>
    public SessionEvent? Opening { get; init; }

    /// <summary>
    /// Where the session's first failed call began (SESS1 S9), wherever it is in the run — the call's
    /// first event, since the failure is an update to it — or null when no call failed.
    /// </summary>
    public long? FirstFailure { get; init; }
}

/// <summary>
/// A session's structure, kept on the machine (D76 §2): <c>&lt;id&gt;.events.jsonl</c> beside the
/// verbatim transcript, one event per line.
/// </summary>
/// <remarks>
/// <para><b>Transcript-class.</b> What a session said and did can carry machine paths and a
/// repository's code, so it never leaves the machine (D47 §4): no HTTP surface reads this, only the
/// shell's bridge and the headless host's own terminal.</para>
///
/// <para><b>The file is the record; the event is the window's.</b> Every append writes the line
/// before telling anyone, so a subscriber that fails costs its own view, never the record — the same
/// order the console keeps with its transcript.</para>
///
/// <para><b>Bounded per line.</b> A tool that printed a megabyte would make the record unreadable a
/// page at a time, so large fields are cut, and the cut says how long the original was.</para>
/// </remarks>
public sealed class SessionEvents(string directory)
{
    /// <summary>The most a text field keeps: a long answer, not a log file.</summary>
    public const int TextLimit = 64 * 1024;

    /// <summary>The most a raw field keeps — input, output, an unknown frame: enough to recognise.</summary>
    public const int RawLimit = 2000;

    /// <summary>A page, unless the caller asks for fewer.</summary>
    public const int PageLimit = 200;

    /// <summary>The largest page anyone may ask for.</summary>
    public const int MaxPage = 1000;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Machine-local and never HTML: a Chinese sentence stays readable in the file.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>What a session id may be, since one arrives from a page: a name, never a path.</summary>
    private static readonly Regex Id = new("^[A-Za-z0-9][A-Za-z0-9_.:-]*$", RegexOptions.CultureInvariant);

    private readonly object _gate = new();
    private readonly Dictionary<string, long> _latest = new(StringComparer.OrdinalIgnoreCase);

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
            if (!_latest.TryGetValue(sessionId, out var latest)) latest = Last(path);
            stamped = Bound(e) with
            {
                Seq = latest + 1,
                At = e.At == default ? DateTimeOffset.UtcNow : e.At,
            };

            Directory.CreateDirectory(directory);
            // 🔴 Shared both ways, reads and writes: a page reads this record while a session appends to
            // it, and a writer or reader that denied the other failed with a sharing violation — which
            // the caller, rightly unwilling to fail a session over its record, turned into a lost event
            // (CONV3b: an agent's answer missing from a turn that had ended).
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            using (var writer = new StreamWriter(stream, Utf8))
            {
                writer.Write(JsonSerializer.Serialize(stamped, Json) + "\n");
            }

            _latest[sessionId] = stamped.Seq;
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
    /// does not run it (D76 §2). Four callers had written this guard for themselves (REV3 CLEAN1).
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
    public EventPage Page(string sessionId, long? before = null, int limit = PageLimit)
    {
        var all = Read(PathOf(sessionId));
        var latest = all.Count == 0 ? 0 : all[^1].Seq;
        var older = before is { } b ? all.Where(e => e.Seq < b).ToList() : all;
        var take = Math.Clamp(limit, 1, MaxPage);
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
        var all = Read(PathOf(sessionId));
        return new EventPage([.. all.Where(e => e.Seq > after)], Earlier: false, all.Count == 0 ? 0 : all[^1].Seq);
    }

    /// <summary>How long a session's opening may be before it is cut: a row's title, not a paragraph.</summary>
    public const int OpeningLimit = 120;

    /// <summary>A search's hits from one session, at most — the rest of that session is one press away.</summary>
    public const int HitsPerSession = 3;

    /// <summary>A search's hits in all, unless the caller asks for fewer.</summary>
    public const int SearchLimit = 50;

    /// <summary>How many records a search reads, newest first — a person typing is not waiting on a crawl.</summary>
    public const int SearchScan = 200;

    /// <summary>How much text a snippet keeps either side of the match.</summary>
    private const int SnippetRadius = 60;

    /// <summary>
    /// What a person first said in each of these sessions (RAIL1) — a conversation's identity, as the
    /// working-surface design (§3) names it: its first line, cut to a title's length.
    /// </summary>
    /// <remarks>
    /// Machine-local, like the record it is read from (D47 §4): what a session said never rides the
    /// session record, which travels. A driven session's composed target is not the person speaking, so
    /// it has no opening; nor has a session with no record here, nor an id that is not one.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Openings(IEnumerable<string> sessionIds)
    {
        var openings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in sessionIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!IsId(id)) continue;
            var path = Path.Combine(directory, $"{id}.events.jsonl");
            // Only as far as the first thing the person said, which is usually the file's first line.
            var asked = Lines(path).FirstOrDefault(e => e.Kind == SessionEventKind.User && e.Origin == "person");
            if (asked?.Text is not { } text) continue;

            var first = text.Split('\n', 2)[0].Trim();
            if (first.Length == 0) continue;
            openings[id] = first.Length <= OpeningLimit ? first : $"{first[..OpeningLimit]}…";
        }

        return openings;
    }

    /// <summary>
    /// The sessions whose words hold <paramref name="query"/> (RAIL1): the person's and the agent's,
    /// each with a snippet around the match, newest sessions first.
    /// </summary>
    /// <remarks>
    /// <para><b>An agent's message is searched whole.</b> It is streamed in chunks and kept as them, so a
    /// word can straddle two lines of this record; the chunks are joined, as the page joins them, before
    /// anything is matched.</para>
    ///
    /// <para><b>What was said, not what a tool printed</b>: a tool's output is a file's contents or a
    /// command's noise, and would bury the conversation that mentioned it.</para>
    ///
    /// <para><b>Bounded, and it says so</b> (<see cref="SessionSearch.Cut"/>): a few hits per session, a
    /// limit in all, and the newest <see cref="SearchScan"/> records read. Machine-local, like the record.</para>
    /// </remarks>
    public SessionSearch Search(string query, int limit = SearchLimit)
    {
        var wanted = query.Trim();
        if (wanted.Length < 2 || !Directory.Exists(directory)) return new([], false);

        var records = new DirectoryInfo(directory).EnumerateFiles("*.events.jsonl")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();
        var cut = records.Count > SearchScan;
        var hits = new List<SessionHit>();

        foreach (var record in records.Take(SearchScan))
        {
            var session = record.Name[..^".events.jsonl".Length];
            var found = 0;
            foreach (var (seq, kind, text) in Passages(record.FullName))
            {
                var at = text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                if (found == HitsPerSession || hits.Count == limit)
                {
                    cut = true;
                    break;
                }

                hits.Add(new SessionHit(session, seq, kind, Snippet(text, at, wanted.Length)));
                found++;
            }

            if (hits.Count == limit && cut) break;
        }

        return new SessionSearch(hits, cut);
    }

    /// <summary>A search within one session's record, at most — a long run's every mention of a word.</summary>
    public const int WithinLimit = 100;

    /// <summary>
    /// One session's record searched (SESS1 S9): what was said, and each call by its title, in the order
    /// they came — a hit's sequence is where its message or its call began, the block the page draws.
    /// </summary>
    public SessionSearch Within(string sessionId, string query, int limit = WithinLimit)
    {
        var wanted = query.Trim();
        var path = PathOf(sessionId);
        if (wanted.Length < 2 || !File.Exists(path)) return new([], false);

        var hits = new List<SessionHit>();
        var cut = false;
        foreach (var (seq, kind, text) in Passages(path).Concat(Calls(path)).OrderBy(passage => passage.Seq))
        {
            var at = text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase);
            if (at < 0) continue;
            if (hits.Count == limit)
            {
                cut = true;
                break;
            }

            hits.Add(new SessionHit(sessionId, seq, kind, Snippet(text, at, wanted.Length)));
        }

        return new SessionSearch(hits, cut);
    }

    /// <summary>
    /// The agent's last message, whole (PARK1): what a session that parks to ask the person said to them,
    /// joined from its chunks as the page joins them. Null when the person spoke after it (that turn said
    /// nothing), and for no record, an unreadable one, or an id that is not one.
    /// </summary>
    /// <remarks>
    /// The transcript is console lines, where an indented line is a tool's output; a question's own
    /// indented list read as that, and the card kept only what came after it.
    /// </remarks>
    public string? LastSaid(string sessionId)
    {
        if (!IsId(sessionId)) return null;
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
    /// The agent's plan as the record last kept it (TOOL4f, D125 §3.5): the newest plan event's entries, whole, since a
    /// plan is written whole each time it changes. Empty for no plan, no record, an unreadable one, or an id that is not
    /// one.
    /// </summary>
    public IReadOnlyList<PlanEntry> LastPlan(string sessionId)
    {
        if (!IsId(sessionId)) return [];
        try
        {
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

            // The words a driven session was told while it worked are found once, where it took them (STEER1): the event
            // that showed them waiting carries `Reaches`, and the same words follow it.
            if (e.Kind == SessionEventKind.User && e.Reaches is null && e.Text is { Length: > 0 } said) yield return (e.Seq, e.Kind, said);
        }

        if (message is { } last) yield return (last.Seq, SessionEventKind.Message, last.Text.ToString());
    }

    /// <summary>A window of one line around a match, with an ellipsis wherever the text goes on.</summary>
    private static string Snippet(string text, int at, int length)
    {
        var start = Math.Max(0, at - SnippetRadius);
        var end = Math.Min(text.Length, at + length + SnippetRadius);
        var window = text[start..end].ReplaceLineEndings(" ").Trim();
        return $"{(start > 0 ? "…" : "")}{window}{(end < text.Length ? "…" : "")}";
    }

    /// <summary>
    /// Remove a session's kept conversation (SESSUX1f, D126 §5.4), once its record is deleted, and forget where its numbering
    /// stood. True when there was a file to remove; an id that names no session removes nothing.
    /// </summary>
    public bool Forget(string sessionId)
    {
        if (!IsId(sessionId)) return false;
        lock (_gate)
        {
            _latest.Remove(sessionId);
            var path = PathOf(sessionId);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    /// <summary>Where a session's events are kept — refused for anything that is not an id.</summary>
    public string PathOf(string sessionId)
    {
        if (!IsId(sessionId))
        {
            throw new DriverException($"`{sessionId}` is not a session id, so it names no record.");
        }

        return Path.Combine(directory, $"{sessionId}.events.jsonl");
    }

    /// <summary>
    /// Whether this is a session id — the one check for everything named by one under the home (the
    /// record here, a conversation's kept files), so an id that arrives from a page never names a path.
    /// </summary>
    public static bool IsId(string? sessionId) =>
        !string.IsNullOrEmpty(sessionId) && Id.IsMatch(sessionId) && !sessionId.Contains("..");

    private static long Last(string path)
    {
        var all = Read(path);
        return all.Count == 0 ? 0 : all[^1].Seq;
    }

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
            if (string.IsNullOrWhiteSpace(line)) continue;
            SessionEvent? e = null;
            try
            {
                e = JsonSerializer.Deserialize<SessionEvent>(line, Json);
            }
            catch (JsonException)
            {
                // A torn or foreign line costs itself, never the conversation around it.
            }

            if (e is { Kind.Length: > 0 }) yield return e;
        }
    }

    private static SessionEvent Bound(SessionEvent e) => e with
    {
        Text = Cut(e.Text, TextLimit),
        Content = e.Content?.Select(c => c with
        {
            Text = Cut(c.Text, TextLimit),
            OldText = Cut(c.OldText, TextLimit),
            NewText = Cut(c.NewText, TextLimit),
        }).ToList(),
        Input = Cut(e.Input, RawLimit),
        Output = Cut(e.Output, RawLimit),
        Raw = Cut(e.Raw, RawLimit),
    };

    /// <summary>A field cut to a limit, saying how long it was — never silently shortened.</summary>
    internal static string? Cut(string? text, int limit) =>
        text is null || text.Length <= limit ? text : $"{text[..limit]}… ({text.Length} chars)";
}
