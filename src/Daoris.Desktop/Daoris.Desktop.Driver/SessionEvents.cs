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

    /// <summary>A tool call's id: its later updates carry the same one.</summary>
    public string? Id { get; init; }

    public string? Text { get; init; }

    /// <summary>For <see cref="SessionEventKind.User"/>: <c>person</c>, or <c>target</c> for the driver's composed prompt.</summary>
    public string? Origin { get; init; }

    /// <summary>
    /// For <see cref="SessionEventKind.User"/>: the names of the files the person attached (CONV4c) —
    /// names, never the kept paths or the lines Daoris added to reach them.
    /// </summary>
    public IReadOnlyList<string>? Files { get; init; }

    public string? Title { get; init; }

    /// <summary>ACP's tool kind — read, edit, delete, move, search, execute, think, fetch, other.</summary>
    public string? ToolKind { get; init; }

    public string? Status { get; init; }

    /// <summary>The files a tool call touched, as the wire named them.</summary>
    public IReadOnlyList<string>? Locations { get; init; }

    public IReadOnlyList<ToolContent>? Content { get; init; }

    /// <summary>A tool call's input as the wire carried it, compact and bounded.</summary>
    public string? Input { get; init; }

    /// <summary>A tool call's raw output, compact and bounded.</summary>
    public string? Output { get; init; }

    public IReadOnlyList<PlanEntry>? Entries { get; init; }

    public long? Used { get; init; }

    public long? Size { get; init; }

    public string? StopReason { get; init; }

    /// <summary>What an unknown or unreadable frame said, compact and bounded.</summary>
    public string? Raw { get; init; }
}

/// <summary>One page of a session's events, oldest first.</summary>
/// <param name="Earlier">Whether events older than this page exist — what "load earlier" asks after.</param>
/// <param name="Latest">The newest sequence the session has — what a live page merges after.</param>
public sealed record EventPage(IReadOnlyList<SessionEvent> Events, bool Earlier, long Latest);

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
        return new EventPage(page, older.Count > page.Count, latest);
    }

    /// <summary>Everything newer than <paramref name="after"/> — how a page that missed a batch closes the gap.</summary>
    public EventPage After(string sessionId, long after)
    {
        var all = Read(PathOf(sessionId));
        return new EventPage([.. all.Where(e => e.Seq > after)], Earlier: false, all.Count == 0 ? 0 : all[^1].Seq);
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
    private static List<SessionEvent> Read(string path)
    {
        if (!File.Exists(path)) return [];

        var events = new List<SessionEvent>();
        // Read beside a writer, never against it (see Append): a line still being written is torn, and a
        // torn line costs itself below.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Utf8);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                if (JsonSerializer.Deserialize<SessionEvent>(line, Json) is { Kind.Length: > 0 } e) events.Add(e);
            }
            catch (JsonException)
            {
                // A torn or foreign line costs itself, never the conversation around it.
            }
        }

        return events;
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
