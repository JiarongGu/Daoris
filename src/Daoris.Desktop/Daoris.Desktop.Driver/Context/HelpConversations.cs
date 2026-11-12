using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

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

    /// <summary>The first line of Ask Daoris's last answer in it, as Markdown, whole as far as <see cref="HelpConversations.PreviewLimit"/>.</summary>
    public string? About { get; init; }

    /// <summary>When it is opened.</summary>
    public DateTimeOffset Created { get; init; }

    /// <summary>When it is last spoken in: its record's last word here, else when its record last moved.</summary>
    public DateTimeOffset Last { get; init; }

    /// <summary>When the person pinned it, or null.</summary>
    public DateTimeOffset? Pinned { get; init; }

    /// <summary>Whether it still runs.</summary>
    public bool Live { get; init; }

    /// <summary>The access mode its last start got, never the mode chosen now; a record from before the modes ran restricted.</summary>
    public string Mode { get; init; } = HelpModeNames.Restricted;

    /// <summary>The workspace it started in, never moved; a record with none is <c>default</c>'s.</summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    /// <summary>Whether words written to it once it ended go on in its own conversation: its harness id is kept, on an agent whose door resumes.</summary>
    public bool Resumable { get; init; }

    /// <summary>The place its last turn ran under, its home's until one is applied (<see cref="HelpFoci"/>).</summary>
    public HelpFocus Focus { get; init; } = HelpFocus.Machine;

    /// <summary>The place chosen for its next words, or null.</summary>
    public HelpFocus? FocusNext { get; init; }

    public IReadOnlyList<HelpFocusMark> Marks { get; init; } = [];

    /// <summary>The earlier conversation it started from, or null.</summary>
    public string? From { get; init; }

    /// <summary>What it is handed of that one: <see cref="HelpConversations.Transcript"/>, <see cref="HelpConversations.Brief"/>, or null.</summary>
    public string? Handed { get; init; }

    /// <summary>Where a search found its words: its title where that holds them, else a snippet around the match, or null.</summary>
    public string? Found { get; init; }

    /// <summary>The line a search found its words on in what is said, as Markdown; null where the title holds them, or for no search.</summary>
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
/// Ask Daoris's conversations, listed: each is a help record the host already keeps (its words this machine's own
/// record of it), and what the person adds to it is its row of <see cref="ConversationFacts"/>.
/// </summary>
/// <remarks>
/// This machine's, and the person's: no feed pushes a help record, and its name and pin are the facts' row, never the record's. One
/// opened ahead of the person and never spoken in or named is no conversation, so it is not listed.
/// </remarks>
public sealed class HelpConversations(string home)
{

    /// <summary>
    /// What a conversation started from an earlier one, or one that went on from it in a new part, is handed: Daoris's
    /// brief of that line, then the person's words.
    /// </summary>
    public const string Brief = "brief";

    /// <summary>What a replacement is handed: the words that waited on an earlier one whose agent refused its model at the open.</summary>
    public const string NotOffered = "not-offered";

    /// <summary>A page's most, and a page where none is asked for.</summary>
    public const int PageLimit = 200;

    /// <summary>How much of a line the page is given to parse before it cuts it, far past the two lines a row shows.</summary>
    public const int PreviewLimit = 1000;

    private readonly ConversationFacts _facts = new(home);

    /// <summary>
    /// A page of Ask Daoris's conversations among <paramref name="records"/>: pinned first, the newest pin first, then the newest
    /// by when each is last spoken in. With <paramref name="search"/>, only those whose name or words hold it, each with where.
    /// </summary>
    /// <remarks>
    /// Every record is ordered and searched before a page is taken, so a pinned or lately spoken conversation opened long ago is
    /// never left off; ties go by when each is opened, then by id, so a page's edge is the same each time. A workspace named keeps
    /// only its records, before anything is ordered, searched or counted; a record that keeps none is <c>default</c>'s.
    /// </remarks>
    /// <param name="resumes">Whether an adapter's door resumes a conversation by its id, by the adapter's name.</param>
    /// <param name="search">Words to find; too few to search with (<see cref="SessionEventText.Searchable"/>), nothing is found.</param>
    /// <param name="offset">Where the page starts in the whole list; below 0 is 0.</param>
    /// <param name="limit">The most the page holds; below 1, or above <see cref="PageLimit"/>, is <see cref="PageLimit"/>.</param>
    /// <param name="workspace">The workspace whose conversations are listed; null, or blank, for every workspace's.</param>
    /// <param name="about">The place whose conversations are listed (<see cref="HelpFoci.About"/>), kept as a workspace is; null for any.</param>
    public HelpListing List(
        IEnumerable<SessionRecord> records, SessionEvents events, Func<string, bool> resumes, string? search = null,
        int offset = 0, int limit = PageLimit, string? workspace = null, HelpFocus? about = null)
    {
        var wanted = search?.Trim();
        if (wanted is not null && !SessionEventText.Searchable(wanted)) return new([], 0, null);
        offset = Math.Max(0, offset);
        limit = limit is < 1 or > PageLimit ? PageLimit : limit;
        var circle = string.IsNullOrWhiteSpace(workspace) ? null : RemoteTarget.Workspace(workspace);

        var rooms = records
            .Where(record => record.Repository == KnowledgeRoomIdentity.Repository && !record.Teammate && SessionEventText.IsId(record.Id))
            .Where(record => circle is null || string.Equals(record.Workspace, circle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var facts = _facts.Read([.. rooms.Select(record => record.Id)]);
        HelpKept Read(string id) => facts.GetValueOrDefault(id) ?? HelpKept.None;
        var help = rooms.Where(record => about is null || HelpFoci.About(about, record.Workspace, Read(record.Id))).ToList();
        var openings = events.Openings(help.Select(record => record.Id));

        // Fold continuations before ordering, searching and paging. A fresh start handed a transcript remains independent.
        // Only links within this machine's same workspace count; a damaged cycle is bounded by the ids already read.
        var byId = help.ToDictionary(record => record.Id, StringComparer.Ordinal);
        var keptById = help.ToDictionary(record => record.Id, record => Read(record.Id), StringComparer.Ordinal);
        string? Previous(string id) => keptById[id] is { Handed: Brief or NotOffered, From: { } from }
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
            var foci = HelpFoci.Of(each.Kept, each.Record.Workspace);
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
                Focus = foci.Focus,
                FocusNext = foci.FocusNext,
                Marks = each.Kept.Marks,
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
        // With no space to cut at, never half a surrogate pair.
        if (from > start && char.IsLowSurrogate(text[from])) from++;
        if (to < end && char.IsHighSurrogate(text[to - 1])) to--;
        return $"{(from > start ? "…" : "")}{text[from..to]}{(to < end ? "…" : "")}";
    }

}
