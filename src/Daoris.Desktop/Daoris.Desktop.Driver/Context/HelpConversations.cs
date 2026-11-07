using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the person keeps of one Ask Daoris conversation: the name they gave it, when they pinned it, and the earlier
/// conversation it started from with what it is handed. <see cref="None"/> where they kept nothing.
/// </summary>
public sealed record HelpKept(string? Name, DateTimeOffset? Pinned, string? From, string? Handed)
{
    public static HelpKept None { get; } = new(null, null, null, null);
}

/// <summary>One row of Ask Daoris's history: a conversation of this machine's the person spoke in.</summary>
/// <param name="Session">Its record's id.</param>
/// <param name="State">Its record's state, in the public spelling.</param>
public sealed record HelpConversation(string Session, string State)
{
    /// <summary>The records of this conversation in order; a briefed continuation stays one history row.</summary>
    public IReadOnlyList<string> Parts { get; init; } = [];
    /// <summary>The name the person gave it, or null.</summary>
    public string? Name { get; init; }

    /// <summary>The first thing the person asked in it, its first line, cut to a title's length.</summary>
    public string? Opening { get; init; }

    /// <summary>What the list calls it: the person's name for it, else its first question.</summary>
    public string Title => Name ?? Opening ?? "";

    /// <summary>
    /// A line of what it is about: the first line of Ask Daoris's last answer in it, as Markdown, whole as far as
    /// <see cref="HelpConversations.PreviewLimit"/>, so the page parses it before it cuts it to a row. Daoris makes
    /// no model call, so it writes no summary of its own; where the conversation got to is the agent's own words.
    /// </summary>
    public string? About { get; init; }

    /// <summary>When it is opened.</summary>
    public DateTimeOffset Created { get; init; }

    /// <summary>When it is last spoken in: its record's last word here, else when its record last moved.</summary>
    public DateTimeOffset Last { get; init; }

    /// <summary>When the person pinned it, or null.</summary>
    public DateTimeOffset? Pinned { get; init; }

    /// <summary>Whether it still runs.</summary>
    public bool Live { get; init; }

    /// <summary>
    /// The access mode it ran under, its record's: <see cref="HelpModes.Restricted"/> or
    /// <see cref="HelpModes.Full"/>, the mode its last start got, never the mode chosen now; a record from before the modes ran restricted.
    /// </summary>
    public string Mode { get; init; } = HelpModeNames.Restricted;

    /// <summary>
    /// The workspace it started in, its record's: kept from its start and never
    /// moved; a record with none is <c>default</c>'s, as it is recorded.
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>
    /// Whether words written to it once it ended go on in its own conversation: its harness conversation's id is
    /// kept, on an agent whose door resumes. One from before ids were kept for it is offered a new conversation from its words.
    /// </summary>
    public bool Resumable { get; init; }

    /// <summary>The earlier conversation it started from, or null.</summary>
    public string? From { get; init; }

    /// <summary>What it is handed of that one: <see cref="HelpConversations.Transcript"/>, <see cref="HelpConversations.Brief"/>, or null.</summary>
    public string? Handed { get; init; }

    /// <summary>Where a search found its words: its title where that holds them, else a snippet around the match, or null.</summary>
    public string? Found { get; init; }

    /// <summary>
    /// Where a search found its words in what is said: the line they are on, as Markdown, whole as far as
    /// <see cref="HelpConversations.PreviewLimit"/>, for the page to parse before it cuts around them; <see cref="Found"/>'s
    /// snippet is cut first, and a delimiter could lose its partner. Null where the title holds them, or for no search.
    /// </summary>
    public string? FoundLine { get; init; }
}

/// <summary>
/// A page of Ask Daoris's history: its conversations, how many the whole list holds (or the search found), and the
/// offset the next page starts at, null for the last page.
/// </summary>
public sealed record HelpListing(IReadOnlyList<HelpConversation> Conversations, int Total, int? Next)
{
    /// <summary>Whether this page left conversations out, so more follow at <see cref="Next"/>: its <c>cut</c>, read as it is.</summary>
    public bool Cut => Next is not null;
}

/// <summary>
/// Ask Daoris's conversations, kept: each is a help record the host already keeps (its words this machine's own
/// record of it), and what the person adds to it is <c>&lt;home&gt;/sessions/&lt;id&gt;.help.json</c> beside its other files.
/// </summary>
/// <remarks>
/// <para><b>This machine's, and the person's.</b> A help record keeps the workspace it started in, and no feed pushes it whatever
/// that is (the sync skips it by its kind). Its words, its name and its pin are files under the home, never the
/// record, which travels where a record does, and never a repository. A session's derived name makes hand-naming it
/// unneeded; a conversation the person goes back to is named by them as a chat history is, and the name stays here.</para>
///
/// <para><b>Listed from the records and the words</b>: a record of Ask Daoris's of this machine's the person spoke in, or named,
/// by its name or first question, when it is last spoken in, and the first line of its last answer. One opened ahead of the
/// person and never spoken in is no conversation, so it is not listed.</para>
///
/// <para><b>A file that does not read is nothing kept</b>, as the archive's marks are: the conversation lists by its question,
/// and the next write starts the file again. A file holding nothing is removed.</para>
/// </remarks>
public sealed class HelpConversations(string home)
{
    public const string Suffix = SessionHomeFiles.HelpConversationsSuffix;

    /// <summary>
    /// What a conversation started from an earlier one, or one that went on from it in a new part, is handed: Daoris's
    /// brief of that line, then the person's words.
    /// </summary>
    public const string Brief = "brief";

    /// <summary>
    /// What a replacement is handed: the words that waited on an earlier one whose agent refused its model at
    /// the open, before any prompt, moved to this one opened on the walk's next answer.
    /// </summary>
    public const string NotOffered = "not-offered";

    /// <summary>A name's longest: a title, not a note.</summary>
    public const int NameLimit = 80;

    /// <summary>A page's most, and a page where none is asked for: every record is read, and a page is what is answered of them.</summary>
    public const int PageLimit = 200;

    /// <summary>
    /// How much of a line the page is given to parse before it cuts it: an answer's first line, or the line a search
    /// found its words on. Far past the two lines a row shows, so a delimiter cut here is never in a row's view.
    /// </summary>
    public const int PreviewLimit = 1000;

    // One writer at a time in this process: a rename and a pin pressed together would otherwise each drop the other's field.
    private static readonly object Gate = new();

    /// <summary>Where a conversation's file is kept, beside its transcript and its harness conversation's id.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, Checked(session) + Suffix);

    /// <summary>What the person keeps of a conversation; <see cref="HelpKept.None"/> for nothing, or a file that does not read.</summary>
    public HelpKept Read(string session)
    {
        if (!SessionEventText.IsId(session)) return HelpKept.None;
        try
        {
            var path = PathOf(session);
            if (!File.Exists(path)) return HelpKept.None;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return HelpKept.None;
            return new HelpKept(
                Json.NonEmpty(root, "name"), Time(root, "pinned"), Json.NonEmpty(root, "from"), Json.NonEmpty(root, "handed"));
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

    /// <summary>Say that a conversation started from an earlier one, and what it is handed of it.</summary>
    public void StartedFrom(string session, string from, string handed) =>
        Change(session, kept => kept with { From = Checked(from), Handed = handed });

    /// <summary>
    /// A page of Ask Daoris's conversations among <paramref name="records"/>: pinned first, the newest pin first, then the newest
    /// by when each is last spoken in. With <paramref name="search"/>, only those whose name or words hold it, each with where.
    /// </summary>
    /// <remarks>
    /// <para><b>Every record is ordered and searched before a page is taken</b>. The list read the newest 200 records
    /// by when each is opened and only then read pins, last words and the search, so a pinned or lately spoken conversation
    /// opened long ago fell off it, and an older one is never searched. What orders a record is cheap to read for each: its kept
    /// file, its first question and its record's last line. What only a row shows (its last answer, whether it goes on) is read
    /// for the page alone. A search reads each record's words, as a search must.</para>
    ///
    /// <para><b>An offset into one order.</b> Ties go by when each is opened, newest first, then by id, so a page's edge is the
    /// same each time it is asked. A conversation that moves between two asks (spoken in, pinned) can show on two pages or on
    /// neither until the list is asked again from its start.</para>
    ///
    /// <para><b>One workspace's, or every one's</b>. A workspace named keeps the records
    /// its conversations started in, its name matched whatever its letters' case, before anything is ordered, searched or
    /// counted, so another workspace's never fill its page, and its total and next page are its own. None named is every
    /// workspace's, the list as it is. A record from before keeps no workspace and is <c>default</c>'s, never guessed from its
    /// words.</para>
    /// </remarks>
    /// <param name="resumes">Whether an adapter's door resumes a conversation by its id, by the adapter's name.</param>
    /// <param name="search">Words to find; too few to search with (<see cref="SessionEventText.Searchable"/>), nothing is found.</param>
    /// <param name="offset">Where the page starts in the whole list; below 0 is 0.</param>
    /// <param name="limit">The most the page holds; below 1, or above <see cref="PageLimit"/>, is <see cref="PageLimit"/>.</param>
    /// <param name="workspace">The workspace whose conversations are listed; null, or blank, for every workspace's.</param>
    public HelpListing List(
        IEnumerable<SessionRecord> records, SessionEvents events, Func<string, bool> resumes, string? search = null,
        int offset = 0, int limit = PageLimit, string? workspace = null)
    {
        var wanted = search?.Trim();
        if (wanted is not null && !SessionEventText.Searchable(wanted)) return new([], 0, null);
        offset = Math.Max(0, offset);
        limit = limit is < 1 or > PageLimit ? PageLimit : limit;
        var circle = string.IsNullOrWhiteSpace(workspace) ? null : RemoteTarget.Workspace(workspace);

        var help = records
            .Where(record => record.Repository == KnowledgeRoomIdentity.Repository && !record.Teammate && SessionEventText.IsId(record.Id))
            .Where(record => circle is null || string.Equals(record.Workspace, circle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var openings = events.Openings(help.Select(record => record.Id));

        // Fold continuations before ordering, searching and paging. A fresh start handed a transcript remains independent.
        // Only links within this machine's same workspace count; a damaged cycle is bounded by the ids already read.
        var byId = help.ToDictionary(record => record.Id, StringComparer.Ordinal);
        var keptById = help.ToDictionary(record => record.Id, record => Read(record.Id), StringComparer.Ordinal);
        string? Previous(string id) => keptById[id] is { Handed: Brief, From: { } from }
            && byId.TryGetValue(from, out var previous)
            && string.Equals(previous.Workspace, byId[id].Workspace, StringComparison.OrdinalIgnoreCase) ? from : null;
        var continued = help.Select(record => Previous(record.Id)).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var parts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var record in help)
        {
            var line = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (string? id = record.Id; id is not null && seen.Add(id); id = Previous(id)) line.Add(id);
            line.Reverse();
            parts[record.Id] = line;
        }

        HelpKept KeptOf(string id)
        {
            var line = parts[id].Reverse().Select(part => keptById[part]).ToList();
            return keptById[id] with
            {
                Name = line.FirstOrDefault(each => each.Name is not null)?.Name,
                Pinned = line.FirstOrDefault(each => each.Pinned is not null)?.Pinned,
            };
        }

        // Every record by what orders it; one the person never spoke in nor named is no conversation.
        var ordered = help
            .Where(record => !continued.Contains(record.Id))
            .Select(record => (Record: record, Kept: KeptOf(record.Id), Opening: parts[record.Id]
                .Select(id => openings.GetValueOrDefault(id)).FirstOrDefault(opening => opening is not null)))
            .Where(each => each.Opening is not null || each.Kept.Name is not null)
            .Select(each => (each.Record, each.Kept, each.Opening, Last: events.LastAt(each.Record.Id) ?? each.Record.Updated))
            .OrderByDescending(each => each.Kept.Pinned.HasValue)
            .ThenByDescending(each => each.Kept.Pinned ?? DateTimeOffset.MinValue)
            .ThenByDescending(each => each.Last)
            .ThenByDescending(each => each.Record.Created)
            .ThenBy(each => each.Record.Id, StringComparer.Ordinal);

        // The search, over every one: the title first, then what is said.
        var listed = new List<(SessionRecord Record, HelpKept Kept, string? Opening, DateTimeOffset Last, string? Found, string? FoundLine)>();
        foreach (var (record, kept, opening, last) in ordered)
        {
            if (wanted is null)
            {
                listed.Add((record, kept, opening, last, null, null));
                continue;
            }

            var title = kept.Name ?? opening ?? "";
            if (title.Contains(wanted, StringComparison.OrdinalIgnoreCase))
            {
                listed.Add((record, kept, opening, last, title, null));
            }
            else if (parts[record.Id].Select(id => events.FirstFound(id, wanted)).FirstOrDefault(hit => hit is not null) is { } found)
            {
                listed.Add((record, kept, opening, last, found.Hit.Snippet, LineAround(found.Passage, found.At, wanted.Length)));
            }
        }

        // Only now the page, and what only a row shows.
        var conversations = new HarnessConversations(home);
        var page = listed.Skip(offset).Take(limit).Select(each =>
        {
            var (said, _) = events.Spoken(each.Record.Id);
            var answer = said.LastOrDefault(passage => passage.Kind == SessionEventKind.Message).Text;
            var conversation = conversations.Read(each.Record.Id);
            return new HelpConversation(each.Record.Id, each.Record.State)
            {
                Parts = parts[each.Record.Id],
                Name = each.Kept.Name,
                Opening = each.Opening,
                About = FirstLine(answer),
                Created = each.Record.Created,
                Last = each.Last,
                Pinned = each.Kept.Pinned,
                Live = each.Record.Live,
                Mode = HelpModeNames.Recorded(each.Record.HelpMode),
                Workspace = each.Record.Workspace,
                Resumable = conversation is not null && resumes(conversation.Adapter),
                From = each.Kept.From,
                Handed = each.Kept.Handed,
                Found = each.Found,
                FoundLine = each.FoundLine,
            };
        }).ToList();

        var end = offset + page.Count;
        return new(page, listed.Count, end < listed.Count ? end : null);
    }

    /// <summary>The first non-blank line of an answer, whole as far as <see cref="PreviewLimit"/>; null for none.</summary>
    private static string? FirstLine(string? text)
    {
        var line = text?.Split('\n').Select(each => each.Trim()).FirstOrDefault(each => each.Length > 0);
        return line is null ? null : TextBounds.Clip(line, PreviewLimit);
    }

    /// <summary>
    /// The line a match is on, whole as far as <see cref="PreviewLimit"/>. A longer line is a window around the
    /// match, cut between words, with an ellipsis where it goes on: far past what a row shows, so the page's own cut comes first.
    /// </summary>
    private static string LineAround(string text, int at, int length)
    {
        var start = at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
        var stop = text.IndexOf('\n', at + length);
        var end = stop < 0 ? text.Length : stop;
        if (end - start <= PreviewLimit) return text[start..end].Trim();

        var half = PreviewLimit / 2;
        var from = Math.Max(start, at - half);
        var to = Math.Min(end, at + length + half);
        if (from > start && text.IndexOf(' ', from, at - from) is >= 0 and var space) from = space + 1;
        if (to < end && text.LastIndexOf(' ', to - 1, to - (at + length)) is >= 0 and var last) to = last;
        return $"{(from > start ? "…" : "")}{text[from..to]}{(to < end ? "…" : "")}";
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

            JsonFile.Write(path, writer => WriteKept(writer, next));
        }
    }

    /// <summary>An id that names a session, or the refusal: nothing under the home is reached through any other.</summary>
    private static string Checked(string session) =>
        SessionEventText.IsId(session) ? session : throw new DriverException($"`{session}` is not a session id, so it names no conversation.");

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static void WriteKept(Utf8JsonWriter writer, HelpKept kept)
    {
        writer.WriteStartObject();
        if (kept.Name is { } name) writer.WriteString("name", name);
        if (kept.Pinned is { } pinned) writer.WriteString("pinned", Moments.Write(pinned));
        if (kept.From is { } from) writer.WriteString("from", from);
        if (kept.Handed is { } handed) writer.WriteString("handed", handed);
        writer.WriteEndObject();
    }

    private static DateTimeOffset? Time(JsonElement element, string name) => Moments.Read(element, name);
}
