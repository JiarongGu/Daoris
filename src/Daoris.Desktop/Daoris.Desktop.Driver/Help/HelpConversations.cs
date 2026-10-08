using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the person keeps of one Ask Daoris conversation (ASKHIST1): the name they gave it, when they pinned it, and the earlier
/// conversation it started from with what it was handed. <see cref="None"/> where they kept nothing.
/// </summary>
public sealed record HelpKept(string? Name, DateTimeOffset? Pinned, string? From, string? Handed)
{
    public static HelpKept None { get; } = new(null, null, null, null);
}

/// <summary>One row of Ask Daoris's history (ASKHIST1): a conversation of this machine's the person spoke in.</summary>
/// <param name="Session">Its record's id.</param>
/// <param name="State">Its record's state, in the public spelling.</param>
public sealed record HelpConversation(string Session, string State)
{
    /// <summary>The name the person gave it, or null.</summary>
    public string? Name { get; init; }

    /// <summary>The first thing the person asked in it, its first line, cut to a title's length.</summary>
    public string? Opening { get; init; }

    /// <summary>What the list calls it: the person's name for it, else its first question.</summary>
    public string Title => Name ?? Opening ?? "";

    /// <summary>
    /// A line of what it was about: the first line of Ask Daoris's last answer in it. Daoris makes no model call (D24), so it
    /// writes no summary of its own; where the conversation got to is the agent's own words.
    /// </summary>
    public string? About { get; init; }

    /// <summary>When it was opened.</summary>
    public DateTimeOffset Created { get; init; }

    /// <summary>When it was last spoken in: its record's last word here, else when its record last moved.</summary>
    public DateTimeOffset Last { get; init; }

    /// <summary>When the person pinned it, or null.</summary>
    public DateTimeOffset? Pinned { get; init; }

    /// <summary>Whether it still runs.</summary>
    public bool Live { get; init; }

    /// <summary>
    /// Whether words written to it once it ended go on in its own conversation (D137 §2.2): its harness conversation's id was
    /// kept, on an agent whose door resumes. One from before ids were kept for it is offered a new conversation from its words.
    /// </summary>
    public bool Resumable { get; init; }

    /// <summary>The earlier conversation it started from, or null.</summary>
    public string? From { get; init; }

    /// <summary>What it was handed of that one: <see cref="HelpConversations.Transcript"/>, or null.</summary>
    public string? Handed { get; init; }

    /// <summary>Where a search found its words: a snippet around the match, or null.</summary>
    public string? Found { get; init; }
}

/// <summary>A listing of Ask Daoris's history, and whether it left conversations out.</summary>
public sealed record HelpListing(IReadOnlyList<HelpConversation> Conversations, bool Cut);

/// <summary>
/// Ask Daoris's conversations, kept (ASKHIST1): each is a help record the host already keeps (its words this machine's own
/// record of it, D76), and what the person adds to it is <c>&lt;home&gt;/sessions/&lt;id&gt;.help.json</c> beside its other files.
/// </summary>
/// <remarks>
/// <para><b>This machine's, and the person's.</b> A help record belongs to no workspace, so no feed pushes it (SYNC4 pushes a
/// workspace's records), and its words, its name and its pin are files under the home (D63), never the record, which travels
/// where a record does (D47 §4), and never a repository. RAIL1 rejected hand-naming a session because its derived name made it
/// unneeded; a conversation the person goes back to is named by them as a chat history is, and the name stays here.</para>
///
/// <para><b>Listed from the records and the words</b>: a record of Ask Daoris's of this machine's the person spoke in, or named,
/// by its name or first question, when it was last spoken in, and the first line of its last answer. One opened ahead of the
/// person (HELP5) and never spoken in is no conversation, so it is not listed.</para>
///
/// <para><b>A file that does not read is nothing kept</b>, as the archive's marks are: the conversation lists by its question,
/// and the next write starts the file again. A file holding nothing is removed.</para>
/// </remarks>
public sealed class HelpConversations(string home)
{
    public const string Suffix = ".help.json";

    /// <summary>What a conversation started from an earlier one is handed: that one's words, both sides, as kept here.</summary>
    public const string Transcript = "transcript";

    /// <summary>A name's longest: a title, not a note.</summary>
    public const int NameLimit = 80;

    /// <summary>How many records a listing reads, newest first: the rest are said to be left out.</summary>
    public const int ListLimit = 200;

    /// <summary>How long the line of what a conversation was about may be before it is cut.</summary>
    public const int AboutLimit = 160;

    // One writer at a time in this process: a rename and a pin pressed together would otherwise each drop the other's field.
    private static readonly object Gate = new();

    /// <summary>Where a conversation's file is kept, beside its transcript and its harness conversation's id.</summary>
    public string PathOf(string session) => Path.Combine(home, "sessions", Checked(session) + Suffix);

    /// <summary>What the person keeps of a conversation; <see cref="HelpKept.None"/> for nothing, or a file that does not read.</summary>
    public HelpKept Read(string session)
    {
        if (!SessionEvents.IsId(session)) return HelpKept.None;
        try
        {
            var path = PathOf(session);
            if (!File.Exists(path)) return HelpKept.None;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return HelpKept.None;
            return new HelpKept(Text(root, "name"), Time(root, "pinned"), Text(root, "from"), Text(root, "handed"));
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return HelpKept.None;
        }
    }

    /// <summary>Name a conversation, or clear its name with null or blank words: its first question is its title again.</summary>
    /// <exception cref="DriverException">An id that names no session, or a name that is not one short line.</exception>
    public void Rename(string session, string? name)
    {
        var named = name?.Trim() is { Length: > 0 } words ? words : null;
        if (named is not null && (named.Contains('\n') || named.Contains('\r')))
        {
            throw new DriverException("a conversation's name is one line; it was not renamed.");
        }

        if (named is { Length: > NameLimit })
        {
            throw new DriverException($"a conversation's name is at most {NameLimit} characters; this one is {named.Length}, and it was not renamed.");
        }

        Change(session, kept => kept with { Name = named });
    }

    /// <summary>Pin a conversation to the top of the list, or unpin it, which puts it back among the rest.</summary>
    /// <exception cref="DriverException">An id that names no session.</exception>
    public void Pin(string session, bool pinned, DateTimeOffset now) =>
        Change(session, kept => kept with { Pinned = pinned ? kept.Pinned ?? now : null });

    /// <summary>Say that a conversation started from an earlier one, and what it was handed of it.</summary>
    public void StartedFrom(string session, string from, string handed) =>
        Change(session, kept => kept with { From = Checked(from), Handed = handed });

    /// <summary>
    /// Ask Daoris's conversations among <paramref name="records"/>: pinned first, the newest pin first, then the newest by when
    /// each was last spoken in. With <paramref name="search"/>, only those whose name or words hold it, each with where.
    /// </summary>
    /// <param name="resumes">Whether an adapter's door resumes a conversation by its id, by the adapter's name.</param>
    /// <param name="search">Words to find; under two characters, nothing is found, as the rail's search answers.</param>
    public HelpListing List(
        IEnumerable<SessionRecord> records, SessionEvents events, Func<string, bool> resumes, string? search = null)
    {
        var wanted = search?.Trim();
        if (wanted is not null && wanted.Length < 2) return new([], false);

        var help = records
            .Where(record => record.Repository == HelpRoom.Repository && !record.Teammate && SessionEvents.IsId(record.Id))
            .OrderByDescending(record => record.Created)
            .ToList();
        var cut = help.Count > ListLimit;
        var conversations = new HarnessConversations(home);
        var openings = events.Openings(help.Take(ListLimit).Select(record => record.Id));

        var rows = new List<HelpConversation>();
        foreach (var record in help.Take(ListLimit))
        {
            var kept = Read(record.Id);
            var opening = openings.GetValueOrDefault(record.Id);
            if (opening is null && kept.Name is null) continue;

            var (said, last) = events.Spoken(record.Id);
            var answer = said.LastOrDefault(passage => passage.Kind == SessionEventKind.Message).Text;
            var conversation = conversations.Read(record.Id);
            var row = new HelpConversation(record.Id, record.State)
            {
                Name = kept.Name,
                Opening = opening,
                About = FirstLine(answer),
                Created = record.Created,
                Last = last ?? record.Updated,
                Pinned = kept.Pinned,
                Live = record.Live,
                Resumable = conversation is not null && resumes(conversation.Adapter),
                From = kept.From,
                Handed = kept.Handed,
            };

            if (wanted is null)
            {
                rows.Add(row);
                continue;
            }

            if (row.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(row with { Found = row.Title });
            }
            else if (events.Within(record.Id, wanted, limit: 1).Hits.FirstOrDefault() is { } hit)
            {
                rows.Add(row with { Found = hit.Snippet });
            }
        }

        var ordered = rows
            .OrderByDescending(row => row.Pinned.HasValue)
            .ThenByDescending(row => row.Pinned ?? DateTimeOffset.MinValue)
            .ThenByDescending(row => row.Last)
            .ToList();
        return new(ordered, cut);
    }

    /// <summary>The first non-blank line of an answer, cut to a line's length; null for none.</summary>
    private static string? FirstLine(string? text)
    {
        var line = text?.Split('\n').Select(each => each.Trim()).FirstOrDefault(each => each.Length > 0);
        return line is null ? null : line.Length <= AboutLimit ? line : $"{line[..AboutLimit]}…";
    }

    private void Change(string session, Func<HelpKept, HelpKept> edit)
    {
        var path = PathOf(session);
        lock (Gate)
        {
            var next = edit(Read(session));
            if (next == HelpKept.None)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, ToJson(next));
        }
    }

    /// <summary>An id that names a session, or the refusal: nothing under the home is reached through any other.</summary>
    private static string Checked(string session) =>
        SessionEvents.IsId(session) ? session : throw new DriverException($"`{session}` is not a session id, so it names no conversation.");

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(HelpKept kept)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            if (kept.Name is { } name) writer.WriteString("name", name);
            if (kept.Pinned is { } pinned) writer.WriteString("pinned", pinned.ToString("O", CultureInfo.InvariantCulture));
            if (kept.From is { } from) writer.WriteString("from", from);
            if (kept.Handed is { } handed) writer.WriteString("handed", handed);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static DateTimeOffset? Time(JsonElement element, string name) =>
        DateTimeOffset.TryParse(Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
}

/// <summary>
/// An earlier Ask Daoris conversation's words, handed to a new one as a file (ASKHIST1): the person's words and Ask Daoris's
/// answers in order, as this machine kept them, under its title and id.
/// </summary>
/// <remarks>
/// <para><b>The transcript, never a summary.</b> Daoris makes no model call (D24), so it has no summary of its own to give; one
/// asked of the earlier conversation's agent would spend a turn and reopen that conversation, which the person did not ask for.
/// The transcript is what was said, read the same way every time, and the new conversation's agent reads it as a file its rules
/// already let it read (CONV4c), handed with the person's first message.</para>
///
/// <para><b>Bounded by its newest words.</b> A long conversation keeps its end, where it got to, and says the start was left
/// out. The tools it used and what Daoris told it (where the person was) are left out: they are the record's, not the talk.</para>
/// </remarks>
public static class HelpTranscript
{
    /// <summary>The most a transcript carries, in characters: a long conversation's end, not a log.</summary>
    public const int Limit = 48_000;

    /// <summary>The name of the file the new conversation is handed.</summary>
    public static string FileName(string session) => $"earlier-conversation-{session}.md";

    /// <summary>The transcript, or null where nothing was said.</summary>
    /// <param name="title">The conversation's title, its name or first question, for the head.</param>
    public static string? Of(SessionEvents events, string session, string? title, int limit = Limit)
    {
        var (said, _) = events.Spoken(session);
        if (said.Count == 0) return null;

        var turns = said
            .Where(passage => passage.Text.Trim().Length > 0)
            .Select(passage => $"## {(passage.Kind == SessionEventKind.User ? "The person" : "Ask Daoris")}\n\n{passage.Text.Trim().ReplaceLineEndings("\n")}\n")
            .ToList();
        if (turns.Count == 0) return null;

        // The newest turns that fit, whole where they fit; the start is what goes.
        var kept = new List<string>();
        var length = 0;
        for (var at = turns.Count - 1; at >= 0; at--)
        {
            var turn = turns[at];
            if (length + turn.Length + 1 > limit)
            {
                if (kept.Count == 0) kept.Insert(0, turn[^Math.Min(turn.Length, limit)..]);
                break;
            }

            kept.Insert(0, turn);
            length += turn.Length + 1;
        }

        var cut = kept.Count < turns.Count || kept[0] != turns[turns.Count - kept.Count];
        var head = new StringBuilder()
            .Append("# An earlier Ask Daoris conversation: ").Append(title is { Length: > 0 } named ? named : session).Append('\n')
            .Append('\n')
            .Append("Session `").Append(session).Append("`, as this machine kept it: the person's words and Ask Daoris's answers, in ")
            .Append("order. The tools it used and what Daoris told it are left out.")
            .Append(cut ? " It was longer than this file carries, so its start is left out and it begins part of the way through." : "")
            .Append("\n\n");
        return head + string.Join("\n", kept);
    }
}
