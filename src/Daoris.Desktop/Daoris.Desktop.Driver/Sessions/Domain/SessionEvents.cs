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

/// <summary>
/// The codes a driver's note in a conversation carries where the page words it itself (MSG1c3, D142 point 1): each the page's
/// <c>work.conversation.&lt;code&gt;</c>, worded in both catalogues. A note with none is the driver's English, shown as written.
/// </summary>
/// <remarks>
/// A twin (<c>.claude/knowledge/twins.md</c>): the page's <c>work/conversation.ts</c> keeps the same list
/// (<c>CONVERSATION_CODES</c>), and its test parses this class, one <c>public const string</c> per line, and holds the list
/// and both catalogues to it; <c>ChatTurnsTests</c> holds the catalogues from this side.
/// </remarks>
public static class SessionEventCodes
{
    /// <summary>
    /// A word handed to a turn at its next step that the person's stop cut off on its way (MSG1c, STEER1 §3): the agent may
    /// have read it, and its answer was not kept.
    /// </summary>
    public const string Lost = "lost";

    /// <summary>
    /// A word the person took back, or reworded, while it still waited (QUEUEEDIT1a): the record showed it waiting under its
    /// id, and this settles it as never taken.
    /// </summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>Every code, which both catalogues word.</summary>
    public static IReadOnlyList<string> All { get; } = [Lost, Withdrawn];
}

/// <summary>
/// The notes every door keeps of a word the person took back (WITHDRAWNLINE1): the driver's terminal and conversations, and the
/// modules' bridge, keep one shape, so the page settles the word it showed waiting however it was taken back.
/// </summary>
public static class SessionNotes
{
    /// <summary>The driver's English for <see cref="Withdrawn"/>: what a page that does not know the code shows.</summary>
    public const string WithdrawnLine = "— taken back before it was sent.";

    /// <summary>
    /// The line a record keeps of a word taken back or reworded while it waited (QUEUEEDIT1a): its code, which the page words as
    /// <c>work.conversation.withdrawn</c>, and the word's id, so the page settles the word it showed waiting.
    /// </summary>
    public static SessionEvent Withdrawn(string word) =>
        new() { Kind = SessionEventKind.Note, Text = WithdrawnLine, Words = [word], Code = SessionEventCodes.Withdrawn };
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
/// Counts only. The total is their sum and is not kept twice; the cost stays on the wire, and the record keeps no model's name
/// (D24, TOOL3): the models ride the turn's event to the usage history alone (<see cref="SessionEvent.Models"/>, USAGECAP1a2).
/// Measured on both doors: docs/2026-09-25-stream-json-evidence.md, § CONV5.
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
    /// For the turn Daoris handed a session with another agent's findings (XAGENT1e, D155 point 7): the second opinion they are
    /// from. Its <see cref="Origin"/> is <c>target</c>, since the words are Daoris's fixed words around that agent's claims, so a
    /// page that does not read this field shows them as what Daoris handed the session and never as the person's. And for the
    /// driver's note on how the session answered them, read as its turn ended. Null on every other event.
    /// </summary>
    public string? Opinion { get; init; }

    /// <summary>
    /// For the person's words to a driven session, on the event that shows them the moment they are said (STEER1, D136):
    /// when they reach it, <c>next-step</c> or <c>turn-end</c>, or <c>now</c> where the person's *Send now* steered them into
    /// the running turn (STEER2a). The words where the session took them come again without it, under the same
    /// <see cref="Id"/>, and that one is the turn's ask.
    /// </summary>
    public string? Reaches { get; init; }

    /// <summary>
    /// For the person's words shown the moment they are said to a running session (COWORK1a, D188 point 1): the urgency they
    /// asked for, <c>ordinary</c>, <c>urgent</c> or <c>now</c>, beside <see cref="Reaches"/>, what the door gave. A word whose level
    /// changes while it waits is told again under the same <see cref="Id"/>, and the latest says where it stands. Null on a record
    /// from before, read as it was.
    /// </summary>
    public string? Level { get; init; }

    /// <summary>
    /// For the person's words shown the moment they are said (MSG1d, D137 §3.3): the door they were said at, <c>screen</c> or
    /// <c>terminal</c>, which the machine log's <c>session.reopened</c> names when the words reopen the session. Null where
    /// the door kept none.
    /// </summary>
    public string? Door { get; init; }

    /// <summary>
    /// For the driver's note about the person's words (MSG1d, D137 §3.1): the ids of the words it speaks of, as their record
    /// gave them, so the note pairs with where the words were shown.
    /// </summary>
    public IReadOnlyList<string>? Words { get; init; }

    /// <summary>
    /// For the driver's note that the person's words went to a new session (MSG1d, D137 §3.1): that session's id, which the
    /// page links.
    /// </summary>
    public string? To { get; init; }

    /// <summary>
    /// For the driver's note about the person's words (MSG1d, D137 §5.1): why they did not go on in this session, a code of
    /// <see cref="ContinueWhy"/>. A reason is chrome, so the page says it in its own words from the code; the line beside it
    /// is the driver's English.
    /// </summary>
    public string? Why { get; init; }

    /// <summary>
    /// For a driver's note the page words itself (MSG1c3, D142 point 1): a code of <see cref="SessionEventCodes"/>. Daoris's
    /// own line is chrome, so the page says it from the code in the reader's language; <see cref="Text"/> beside it stays the
    /// driver's English, for the console, the terminal and a page that does not know the code.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// For a driver's note whose lines are worded by code (LAND2b, LANG1a): its parts, as a session record's note carries them
    /// beside its English (<see cref="NotePart"/>), each coded part from <see cref="NoteCodes"/>. <see cref="Text"/> stays the
    /// whole English, for the console, the terminal, the trace and a page that does not read the parts. Null on every other.
    /// </summary>
    public IReadOnlyList<NotePart>? Parts { get; init; }

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

    /// <summary>
    /// For <see cref="SessionEventKind.Turn"/>: which models its tokens were spent on, as its door said them (USAGECAP1a2, D179
    /// point 2), for the usage history alone. Never written: the record and every page keep no model's name (D24, TOOL3), so it
    /// rides in memory from the door to the history's watch, and an event read back from the record has none.
    /// </summary>
    [JsonIgnore]
    public TurnModels? Models { get; init; }

    /// <summary>
    /// For the instruction the driver composed (origin <c>target</c>): what it was handed, section by section (CONTEXT1, D143
    /// point 1): each section's size, its source and what its bound left out, and what was handed beside it. Null on every
    /// other event, and on an instruction handed before the account was kept.
    /// </summary>
    public InstructionAccount? Account { get; init; }

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
/// <param name="Cut">Whether anything was left out: a session on this page held more hits than it shows, or more sessions follow.</param>
/// <param name="Total">The sessions with a hit, all pages (RAILPAGE1a); a search within one session counts it as one.</param>
/// <param name="Next">Where the next page starts, in sessions; null on the last.</param>
public sealed record SessionSearch(IReadOnlyList<SessionHit> Hits, bool Cut, int Total = 0, int? Next = null);

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
/// The rules a session's record keeps apart from the file it is kept in (D76 §2, STRUCT3g-i): its bounds, how it is spelled,
/// what an id may be and how a text is cut or searched. <see cref="SessionEvents"/> keeps the file; a caller names these here.
/// </summary>
public static class SessionEventText
{
    /// <summary>The most a text field keeps: a long answer, not a log file.</summary>
    public const int TextLimit = 64 * 1024;

    /// <summary>The most a raw field keeps — input, output, an unknown frame: enough to recognise.</summary>
    public const int RawLimit = 2000;

    /// <summary>A page, unless the caller asks for fewer.</summary>
    public const int PageLimit = 200;

    /// <summary>The largest page anyone may ask for.</summary>
    public const int MaxPage = 1000;

    /// <summary>How much text a snippet keeps either side of the match.</summary>
    private const int SnippetRadius = 60;

    /// <summary>How the record spells an event: camel-cased, absent fields left out, a Chinese sentence kept readable.</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Machine-local and never HTML: a Chinese sentence stays readable in the file.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>What a session id may be, since one arrives from a page: a name, never a path.</summary>
    private static readonly Regex Id = new(@"^[A-Za-z0-9][A-Za-z0-9_.:-]*\z", RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether this is a session id — the one check for everything named by one under the home (the
    /// record here, a conversation's kept files), so an id that arrives from a page never names a path. It keeps <c>:</c>
    /// for a remote's <c>machine:id</c> form (DEDUP6): each side of one is held to the folder rule, so a trailing newline,
    /// a trailing dot or a device name is no id either.
    /// </summary>
    public static bool IsId(string? sessionId) =>
        !string.IsNullOrEmpty(sessionId) && Id.IsMatch(sessionId) && sessionId.Split(':').All(SafeNames.IsFolderId);

    /// <summary>
    /// Whether this is an id of this machine's, which names a file under the home: <see cref="IsId"/> and the folder rule whole,
    /// so a remote's <c>machine:id</c> (on Windows <c>a:b</c> names a stream of <c>a</c>) never becomes a local file.
    /// </summary>
    public static bool IsLocalId(string? sessionId) => IsId(sessionId) && SafeNames.IsFolderId(sessionId!);

    /// <summary>
    /// Whether these words are enough to search with (ASKHIST1d): two characters, or one Han character, which is a word on its
    /// own (区, 圈). One letter of any other script would find nearly everything, so it finds nothing.
    /// </summary>
    public static bool Searchable(string? words)
    {
        var wanted = words?.Trim() ?? "";
        return wanted.Length >= 2 || (wanted.Length == 1 && IsHan(wanted[0]));
    }

    // The basic block, extension A and the compatibility block; the later extensions are surrogate pairs, two characters already.
    private static bool IsHan(char c) => c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or (>= '豈' and <= '﫿');

    /// <summary>A window of one line around a match, with an ellipsis wherever the text goes on.</summary>
    internal static string Snippet(string text, int at, int length)
    {
        var start = Math.Max(0, at - SnippetRadius);
        var end = Math.Min(text.Length, at + length + SnippetRadius);
        var window = TextBounds.OneLine(text[start..end]);
        return $"{(start > 0 ? "…" : "")}{window}{(end < text.Length ? "…" : "")}";
    }

    /// <summary>An event with each large field cut to its limit, saying how long it was.</summary>
    internal static SessionEvent Bound(SessionEvent e) => e with
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
        TextBounds.ClipCounted(text, limit);
}
