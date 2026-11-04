using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>
/// One trace, as data (TRACE1b, D143, D50): the reading both doors word. The terminal prints it in the driver's English
/// (<see cref="TraceWords"/>), and the screen words its codes in the reader's language. Each link names the store it was read
/// from, and a link nothing keeps carries why it is missing.
/// </summary>
/// <remarks>
/// <b>Nothing here is a machine path on the wire</b> (D47 §4): a session's tree is its folder's name, a file under the home is
/// named by its store, and what a failed read said (an exception's message, which may name a file) is the terminal's alone,
/// held by <see cref="JsonIgnoreAttribute"/>.
/// </remarks>
/// <param name="Kind">The kind the id named, one of <see cref="TraceEntry"/>.</param>
public sealed record TraceChain(string Kind, string Id)
{
    /// <summary>For a commit: each evidence line and each landing that names it.</summary>
    public IReadOnlyList<TraceFoundBy> Found { get; init; } = [];

    /// <summary>Each store that did not answer: its links below say they could not be read.</summary>
    public IReadOnlyList<TraceUnread> Unread { get; init; } = [];

    /// <summary>The chain, in the order it is read: each quest's ask, the quest, its sessions; then the sessions on no quest.</summary>
    public IReadOnlyList<TraceLink> Links { get; init; } = [];
}

/// <summary>How a commit was found: in a session's evidence, or as a landing's tip or the commit a plugin pushed of it.</summary>
/// <param name="How"><c>evidence</c>, <c>tip</c> or <c>pushed</c>.</param>
public sealed record TraceFoundBy(string How, string Session)
{
    public const string Evidence = "evidence";
    public const string Tip = "tip";
    public const string Pushed = "pushed";

    /// <summary>The evidence's line that names it.</summary>
    public string? Line { get; init; }

    /// <summary>The branch whose landing names it.</summary>
    public string? Branch { get; init; }
}

/// <summary>A store that did not answer, one of <see cref="TraceStores"/>.</summary>
public sealed record TraceUnread(string Store)
{
    /// <summary>What the read said, the terminal's: it may name a file under the home.</summary>
    [JsonIgnore]
    public string? Problem { get; init; }
}

/// <summary>One link: an ask, a quest, a session, or a session only a landing names.</summary>
/// <param name="Kind"><c>ask</c>, <c>quest</c>, <c>session</c> or <c>unrecorded</c>; the one link of that kind is set.</param>
public sealed record TraceLink(string Kind)
{
    public const string AskKind = "ask";
    public const string QuestKind = "quest";
    public const string SessionKind = "session";
    public const string UnrecordedKind = "unrecorded";

    public TraceAskLink? Ask { get; init; }
    public TraceQuestLink? Quest { get; init; }
    public TraceSessionLink? Session { get; init; }
    public TraceUnrecordedLink? Unrecorded { get; init; }
}

/// <summary>What names a link the chain reached: a quest's sender, an intake's record, a session's record, a landing, or the trace.</summary>
/// <param name="Kind"><c>quest</c>, <c>intake</c>, <c>session</c>, <c>landing</c> or <c>trace</c>.</param>
public sealed record TraceNamedBy(string Kind, string? Id = null);

/// <summary>What a read of an id came to: its chain, or why nothing here names it.</summary>
/// <param name="Problem">The terminal's sentence where nothing, or more than one kind, names the id.</param>
public sealed record TraceRead(TraceChain? Chain, string? Problem, IReadOnlyList<TraceUnread> Unread);
