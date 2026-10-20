using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A session's line read back from this machine's event files (SESSCTX1f, D189's SESSCTX1f note; the session-context design §3.5): the
/// parts a session continues, followed back by each record's <c>from</c>, and each part's <c>&lt;id&gt;.events.jsonl</c> under the home's
/// <c>sessions</c> folder, read as the entries a reader reads and paged within a bound. The connector's <c>line_read</c> answers with
/// it, so what a brief's bound left out is one call away.
/// </summary>
/// <remarks>
/// <para><b>Transcript-class, never over HTTP</b> (D76 §2): what a session said and did can carry machine paths and a repository's
/// code, so only the stdio connector of a session on this machine reads it, for the parts of that session's own line, and no host route
/// is handed it.</para>
///
/// <para><b>The driver's file, read with code of its own.</b> The driver's <c>SessionEvents</c> writes the file and this shares no code
/// with it, since the connector references the service alone (D183's HELPMODE1c2a note); each side's tests hold the same text
/// (<c>LineEventsShapeTests</c>, the driver's <c>LineEventFilesTests</c>), as the helpers' box and files are held (D163's MIXAGENT2a-ii
/// note). The line is followed as the driver's <c>ContextBriefs.LineOf</c> follows it, and an id names a file only where the driver's
/// <c>SessionEventText.IsLocalId</c> says it does.</para>
/// </remarks>
/// <param name="home">The home the event files are under, or null when there is none: every reading is then refused.</param>
public sealed class LineEvents(string? home)
{
    /// <summary>What a part's event file is named after its id, as the driver's <c>SessionEvents.PathOf</c> names it.</summary>
    public const string Suffix = ".events.jsonl";

    /// <summary>
    /// The most one answer weighs: the brief's own provisional bound (the design §3.2), a Han character weighing
    /// <see cref="HanWeight"/>, so a page is never heavier than the brief it reads behind. Both move with SESSCTX1a's counts.
    /// </summary>
    public const int Bound = 40_000;

    /// <summary>What a Han character weighs toward the bound, as it weighs toward the brief's.</summary>
    public const int HanWeight = 3;

    /// <summary>The most parts a line is followed back: the driver's guard against a chain no person made.</summary>
    public const int Depth = 50;

    /// <summary>The entries a page holds when the caller names no number, and the most it may name.</summary>
    public const int DefaultLimit = 50;

    public const int MaxLimit = 200;

    /// <summary>The most of a tool call's output shown, the record's own bound on a raw field.</summary>
    public const int OutputLimit = 2000;

    /// <summary>Room kept for the page's heading and for what it says at its end.</summary>
    private const int HeadingRoom = 400;

    private const int FooterRoom = 600;

    private const string NoSession =
        "This connector speaks for no session the driver started here, so there is no line of yours to read.";

    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public string? Home { get; } = home;

    /// <summary>The home the driver names on the connector it offers, else the account's (D63): where the event files are.</summary>
    public static LineEvents FromEnvironment() => new(RuleProposalBox.FromEnvironment().Home);

    /// <summary>Where one part's events are: <c>&lt;home&gt;/sessions/&lt;id&gt;.events.jsonl</c>, the home's <see cref="HomeEntry.Sessions"/>.</summary>
    public static string PathOf(string home, string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>
    /// Whether an id is this machine's and names a file under the home, as the driver's <c>SessionEventText.IsLocalId</c> says: a
    /// letter or digit first, then letters, digits, <c>_</c>, <c>.</c> and <c>-</c>; no <c>..</c>, no trailing dot and no device name.
    /// A remote's <c>machine:id</c> and a teammate's <c>origin/id</c> are none.
    /// </summary>
    public static bool IsLocalId(string? id) =>
        !string.IsNullOrEmpty(id)
        && char.IsAsciiLetterOrDigit(id[0])
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-')
        && !id.Contains("..", StringComparison.Ordinal)
        && !id.EndsWith('.')
        && !DeviceNames.Contains(id.Split('.')[0]);

    /// <summary>What a text weighs toward the bound: a Han character <see cref="HanWeight"/>, every other one.</summary>
    public static int Weight(string text)
    {
        var weight = 0;
        foreach (var c in text) weight += IsHan(c) ? HanWeight : 1;
        return weight;
    }

    // The basic block, extension A and the compatibility block, as the driver's brief weighs a Han character.
    private static bool IsHan(char c) => c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or (>= '豈' and <= '﫿');

    /// <summary>
    /// The line <paramref name="record"/> ends, oldest part first: it and, by each record's <c>from</c>, the parts it continued. A link
    /// that comes back on itself, names a record not held here or a teammate's record ends the line there; a teammate's record has none.
    /// </summary>
    /// <param name="find">The record of a session by its id, as the ledger holds it, or null.</param>
    public static async Task<IReadOnlyList<Session>> LineOfAsync(
        Session record, Func<string, CancellationToken, Task<Session?>> find, CancellationToken ct = default)
    {
        var line = new List<Session>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var at = record;
        while (at is { Origin: null } && line.Count < Depth && seen.Add(at.Id))
        {
            line.Add(at);
            at = at.From is { Length: > 0 } from ? await find(from, ct).ConfigureAwait(false) : null;
        }

        line.Reverse();
        return line;
    }

    /// <summary>
    /// A page of one part of <paramref name="line"/>, or a refusal: answered as the text an agent reads.
    /// </summary>
    /// <param name="line">The line the connector's session ends, oldest first, as <see cref="LineOfAsync"/> answers it; empty where no
    /// record of this machine's is held for it.</param>
    /// <param name="named">The session the connector names, said back when no record is held for it.</param>
    /// <param name="part">The part asked for, by its id; null for the part the session continues, or the session itself where it
    /// continues none.</param>
    /// <param name="before">The entries before this number; null for the part's newest.</param>
    /// <param name="limit">The most entries the page holds, from 1 to <see cref="MaxLimit"/>; null for <see cref="DefaultLimit"/>.</param>
    public (bool Read, string Message) Read(IReadOnlyList<Session> line, string? named, string? part, long? before, int? limit)
    {
        if (string.IsNullOrWhiteSpace(named)) return (false, $"{NoSession} Nothing was read.");
        if (line.Count == 0) return (false, $"No session `{Shown(named.Trim())}` of this machine's is held, so there is no line here to read. Nothing was read.");

        var asked = string.IsNullOrWhiteSpace(part) ? null : part.Trim();
        if (asked is not null && !IsLocalId(asked))
        {
            return (false, $"`{Shown(asked)}` names no record on this machine: a part is named by its id, as an answer lists it. Nothing was read.");
        }

        if (Home is null) return (false, $"{Capital(DaorisHome.Sentence)} Nothing was read.");

        var at = asked is null ? Math.Max(0, line.Count - 2) : IndexOf(line, asked);
        if (at < 0)
        {
            return (false,
                $"`{asked}` is not on this session's line, which is {string.Join(", ", line.Select(record => $"`{record.Id}`"))}: this reads the "
                + "parts this session continues and its own, and no other record. Nothing was read.");
        }

        var id = line[at].Id;
        var head = Head(line);
        if (!IsLocalId(id)) return (false, $"{head}\n\nPart `{Shown(id)}` names no file on this machine. Nothing was read.");

        var entries = Entries(Home, id);
        if (entries.Count == 0) return (true, $"{head}\n\nPart `{id}` keeps no events on this machine.{Earlier(line, at)}");

        var older = before is { } b ? entries.Where(entry => entry.Seq < b).ToList() : entries;
        if (older.Count == 0) return (true, $"{head}\n\nPart `{id}` holds no entry before #{before}.{Earlier(line, at)}");

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var room = Bound - Weight(head) - HeadingRoom - FooterRoom;
        var shown = new List<(LineEntry Entry, string Said)>();
        var used = 0;
        string? cut = null;
        for (var i = older.Count - 1; i >= 0 && shown.Count < take; i--)
        {
            var said = Say(older[i]);
            var weight = Weight(said) + 2;
            if (used + weight > room)
            {
                if (shown.Count > 0) break;

                // One entry heavier than a whole page alone is cut, and the cut said, so a page always moves the reader on.
                said = Clip(said, room - 2);
                var length = older[i].Text?.Length ?? Say(older[i]).Length;
                cut = $" Entry #{older[i].Seq} is {length.ToString("N0", CultureInfo.InvariantCulture)} characters, more than one answer carries, "
                      + "and was cut here.";
            }

            shown.Add((older[i], said));
            used += weight;
        }

        shown.Reverse();
        var first = shown[0].Entry.Seq;
        var earlier = older.Count(entry => entry.Seq < first);
        var self = id == line[^1].Id ? ", this session" : "";
        var heading =
            $"Part `{id}`{self}: {shown.Count} of its {entries.Count} {(entries.Count == 1 ? "entry" : "entries")}, #{first} to "
            + $"#{shown[^1].Entry.Seq}, oldest first. The agent's thoughts, usage readings and receipts are never shown.";
        var footer = earlier > 0
            ? $"{earlier} earlier {(earlier == 1 ? "entry" : "entries")} in part `{id}`: call `line_read` with part `{id}` and before {first}."
            : Earlier(line, at).TrimStart();
        return (true, $"{head}\n\n{heading}\n\n{string.Join("\n\n", shown.Select(entry => entry.Said))}\n\n{footer}{cut}");
    }

    /// <summary>
    /// A part's events as a reader reads them, oldest first: each word where it was taken, each run of the agent's chunks joined into one
    /// message (broken, as the page breaks it, by anything but a usage reading), each tool call with its updates at its first number, the
    /// plan, a turn's end and Daoris's notes. A thought, a usage reading, a word shown while it waited, a receipt and an unknown frame are
    /// no entry, and a blank, torn or foreign line costs itself.
    /// </summary>
    public static IReadOnlyList<LineEntry> Entries(string home, string session)
    {
        var entries = new List<LineEntry>();
        if (!IsLocalId(session)) return entries;

        var calls = new Dictionary<string, int>(StringComparer.Ordinal);
        StringBuilder? message = null;
        var messageAt = -1;
        void Joined()
        {
            if (message is null) return;
            entries[messageAt] = entries[messageAt] with { Text = message.ToString() };
            message = null;
        }

        foreach (var e in Events(PathOf(home, session)))
        {
            if (e.Kind == "usage") continue;
            if (e.Kind == "message")
            {
                if (message is null)
                {
                    message = new StringBuilder();
                    messageAt = entries.Count;
                    entries.Add(new LineEntry(e.Seq, "message") { At = e.At });
                }

                message.Append(e.Text);
                continue;
            }

            Joined();
            switch (e.Kind)
            {
                case "user" when e.Reaches is null:
                    entries.Add(new LineEntry(e.Seq, "user") { At = e.At, Origin = e.Origin, Mark = e.Mark, Text = e.Text, Files = e.Files });
                    break;
                case "tool" when e.Id is { } call && calls.TryGetValue(call, out var known):
                    var was = entries[known];
                    entries[known] = was with
                    {
                        Title = e.Title ?? was.Title,
                        ToolKind = e.ToolKind ?? was.ToolKind,
                        Status = e.Status ?? was.Status,
                        Input = e.Input ?? was.Input,
                        Output = e.Output ?? was.Output,
                        Locations = e.Locations ?? was.Locations,
                    };
                    break;
                case "tool":
                    if (e.Id is { } id) calls[id] = entries.Count;
                    entries.Add(new LineEntry(e.Seq, "tool")
                    {
                        At = e.At, Title = e.Title, ToolKind = e.ToolKind, Status = e.Status, Input = e.Input, Output = e.Output, Locations = e.Locations,
                    });
                    break;
                case "plan":
                    entries.Add(new LineEntry(e.Seq, "plan") { At = e.At, Steps = e.Steps });
                    break;
                case "turn":
                    entries.Add(new LineEntry(e.Seq, "turn") { At = e.At, StopReason = e.StopReason });
                    break;
                case "note":
                    entries.Add(new LineEntry(e.Seq, "note") { At = e.At, Text = e.Text });
                    break;
            }
        }

        Joined();
        return entries;
    }

    private static int IndexOf(IReadOnlyList<Session> line, string id)
    {
        for (var i = 0; i < line.Count; i++)
        {
            if (string.Equals(line[i].Id, id, StringComparison.Ordinal)) return i;
        }

        return -1;
    }

    /// <summary>The line said once, oldest part first, with how each part began.</summary>
    private static string Head(IReadOnlyList<Session> line)
    {
        var self = line[^1].Id;
        if (line.Count == 1 && line[0].From is null) return $"Session `{self}`'s line is this session alone: it continues no other.";

        var parts = line.Select((record, index) =>
        {
            var mark = record.Id == self ? ", this session" : "";
            if (index > 0) return $"`{record.Id}`{mark}, which continued `{line[index - 1].Id}` ({record.FromWhy ?? "no reason kept"})";
            return record.From is { } from
                ? $"`{record.Id}`{mark}, which continued `{Shown(from)}`, a record not held here"
                : $"`{record.Id}`{mark}, which began it";
        });
        return $"Session `{self}`'s line, oldest part first: {string.Join("; ", parts)}.";
    }

    /// <summary>Where the reader goes from the start of a part: the part before it, else the line's start.</summary>
    private static string Earlier(IReadOnlyList<Session> line, int at) =>
        at > 0
            ? $" This is the start of part `{line[at].Id}`; the part before it on the line is `{line[at - 1].Id}`: call `line_read` with part `{line[at - 1].Id}`."
            : line[0].From is { } from
                ? $" This is the start of the line as this machine holds it: `{Shown(from)}`, which it continued, has no record here."
                : " This is the start of the line.";

    /// <summary>One entry as the agent reads it: its number, its moment and whose it is, then what it holds.</summary>
    private static string Say(LineEntry entry)
    {
        var when = entry.At is { } at ? at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) : "no moment kept";
        var body = new List<string>();
        switch (entry.Kind)
        {
            case "user":
                if (entry.Text is { Length: > 0 } words) body.Add(words);
                if (entry.Files is { Count: > 0 } files) body.Add($"attached: {string.Join(", ", files)}");
                break;
            case "message" or "note":
                if (entry.Text is { Length: > 0 } text) body.Add(text);
                break;
            case "tool":
                if (entry.Locations is { Count: > 0 } where) body.Add($"files: {string.Join(", ", where)}");
                if (entry.Input is { Length: > 0 } input) body.Add($"input: {input}");
                if (entry.Output is { Length: > 0 } output)
                {
                    body.Add(output.Length > OutputLimit
                        ? $"output: {output[..OutputLimit]}… ({output.Length.ToString("N0", CultureInfo.InvariantCulture)} characters in all)"
                        : $"output: {output}");
                }

                break;
            case "plan":
                body.AddRange((entry.Steps ?? []).Select(step => step.Status is { } status ? $"- [{status}] {step.Content}" : $"- {step.Content}"));
                break;
        }

        var said = $"#{entry.Seq} · {when} · {Who(entry)}";
        return body.Count == 0 ? said : said + "\n" + string.Join("\n", body);
    }

    /// <summary>
    /// Whose an entry is, by the marks the record keeps, never by its words. A word with no <c>origin</c>, from a record kept before
    /// it was written, is the person's, as the driver's brief reads it.
    /// </summary>
    private static string Who(LineEntry entry) => entry.Kind switch
    {
        "user" when entry.Origin is null or "person" => entry.Mark == "channel" ? "the person, on the ask's channel" : "the person",
        "user" => entry.Mark switch
        {
            "brief" => "Daoris's brief of the line before this part",
            "channel" => "another session on the ask's channel, its claim",
            "opinion" => "a second opinion's findings, another agent's claims",
            "daoris" => "Daoris's own words",
            _ => "Daoris",
        },
        "message" => "the agent",
        "tool" => $"a tool call{(entry.Status is { } status ? $", {status}" : "")}: "
                  + (entry.Title ?? entry.ToolKind ?? "untitled")
                  + (entry.Title is not null && entry.ToolKind is { } kind ? $" ({kind})" : ""),
        "plan" => "the plan",
        "turn" => entry.StopReason is { } reason ? $"the turn ended ({reason})" : "the turn ended",
        _ => "Daoris's note",
    };

    /// <summary>A text cut to a weight, ending with an ellipsis.</summary>
    private static string Clip(string text, int weight)
    {
        var used = 0;
        var end = 0;
        while (end < text.Length && used + (IsHan(text[end]) ? HanWeight : 1) <= weight - 1)
        {
            used += IsHan(text[end]) ? HanWeight : 1;
            end++;
        }

        if (end > 0 && char.IsHighSurrogate(text[end - 1])) end--;
        return text[..end] + "…";
    }

    /// <summary>A word said back with no control character in it, and no longer than a line.</summary>
    private static string Shown(string text)
    {
        var plain = new string(text.Select(c => char.IsControl(c) ? '?' : c).ToArray());
        return plain.Length > 80 ? plain[..80] + "…" : plain;
    }

    private static string Capital(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];

    /// <summary>One event, as much of it as an entry reads.</summary>
    private sealed record Event(long Seq, string Kind)
    {
        public DateTimeOffset? At { get; init; }
        public string? Id { get; init; }
        public string? Text { get; init; }
        public string? Origin { get; init; }
        public string? Mark { get; init; }
        public string? Reaches { get; init; }
        public IReadOnlyList<string>? Files { get; init; }
        public string? Title { get; init; }
        public string? ToolKind { get; init; }
        public string? Status { get; init; }
        public string? Input { get; init; }
        public string? Output { get; init; }
        public IReadOnlyList<string>? Locations { get; init; }
        public IReadOnlyList<LineStep>? Steps { get; init; }
        public string? StopReason { get; init; }
    }

    /// <summary>
    /// A file's readable events, one at a time, read beside the driver's writer and never against it: a line still being written is
    /// torn, and a torn line costs itself.
    /// </summary>
    private static IEnumerable<Event> Events(string path)
    {
        if (!File.Exists(path)) yield break;

        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        using (stream)
        using (var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            while (reader.ReadLine() is { } line)
            {
                if (Parse(line) is { } e) yield return e;
            }
        }
    }

    /// <summary>One line read as its event, or null for a blank, torn or foreign line.</summary>
    private static Event? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (Text(root, "kind") is not { Length: > 0 } kind) return null;
            if (!root.TryGetProperty("seq", out var seq) || seq.ValueKind != JsonValueKind.Number || !seq.TryGetInt64(out var number)) return null;

            return new Event(number, kind)
            {
                At = Text(root, "at") is { } at && DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment) ? moment : null,
                Id = Text(root, "id"),
                Text = Text(root, "text"),
                Origin = Text(root, "origin"),
                Mark = Text(root, "continues") is not null ? "brief"
                    : Text(root, "channel") is not null ? "channel"
                    : Text(root, "opinion") is not null ? "opinion"
                    : Text(root, "from") == "daoris" ? "daoris"
                    : null,
                Reaches = Text(root, "reaches"),
                Files = Texts(root, "files"),
                Title = Text(root, "title"),
                ToolKind = Text(root, "toolKind"),
                Status = Text(root, "status"),
                Input = Text(root, "input"),
                Output = Text(root, "output") ?? Contents(root),
                Locations = Texts(root, "locations"),
                Steps = Steps(root),
                StopReason = Text(root, "stopReason"),
            };
        }
        catch (JsonException)
        {
            // A torn or foreign line costs itself, never the line's reading around it.
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IReadOnlyList<string>? Texts(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : null;

    /// <summary>A tool call's text content, where the wire carried its output there and not as a raw field.</summary>
    private static string? Contents(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return null;
        var texts = content.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => Text(item, "text"))
            .OfType<string>()
            .ToList();
        return texts.Count == 0 ? null : string.Join("\n", texts);
    }

    private static IReadOnlyList<LineStep>? Steps(JsonElement root) =>
        root.TryGetProperty("entries", out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object && Text(item, "content") is not null)
                .Select(item => new LineStep(Text(item, "content")!, Text(item, "status")))]
            : null;
}

/// <summary>
/// One entry of a part's record as a reader reads it (SESSCTX1f): <c>user</c>, <c>message</c>, <c>tool</c>, <c>plan</c>, <c>turn</c> or
/// <c>note</c>, at the number of its first event, with only the fields <c>line_read</c> shows.
/// </summary>
/// <param name="Seq">The number of its first event in the part's record.</param>
/// <param name="Kind">One of the six kinds above.</param>
public sealed record LineEntry(long Seq, string Kind)
{
    public DateTimeOffset? At { get; init; }

    /// <summary>For <c>user</c>: <c>person</c>, or <c>target</c> for words that are not the person's; none on a record from before, read as the person's.</summary>
    public string? Origin { get; init; }

    /// <summary>
    /// For <c>user</c>: what the record marks the words as, <c>brief</c> (a new part's brief), <c>channel</c> (the ask's channel),
    /// <c>opinion</c> (a second opinion's findings) or <c>daoris</c> (Daoris's own words); null for none.
    /// </summary>
    public string? Mark { get; init; }

    /// <summary>The words: the person's or Daoris's, the agent's message, Daoris's note.</summary>
    public string? Text { get; init; }

    /// <summary>For <c>user</c>: the names of the files attached.</summary>
    public IReadOnlyList<string>? Files { get; init; }

    public string? Title { get; init; }

    public string? ToolKind { get; init; }

    public string? Status { get; init; }

    public string? Input { get; init; }

    public string? Output { get; init; }

    /// <summary>For <c>tool</c>: the files the call touched, as the wire named them.</summary>
    public IReadOnlyList<string>? Locations { get; init; }

    /// <summary>For <c>plan</c>: its steps.</summary>
    public IReadOnlyList<LineStep>? Steps { get; init; }

    /// <summary>For <c>turn</c>: the wire's stop reason.</summary>
    public string? StopReason { get; init; }
}

/// <summary>One step of a kept plan, with its status where the plan gave one.</summary>
public sealed record LineStep(string Content, string? Status);
