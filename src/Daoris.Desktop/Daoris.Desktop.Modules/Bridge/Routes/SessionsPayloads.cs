namespace Daoris.Desktop;

// The payloads of SessionsRoutes' presses and reads, each named for its route. A flag the terminal's verbs send (`Said`, `Help`)
// is read as the page's flags are: true only where it says true.

public sealed record StopSession(string Id, bool Said = false);

public sealed record ResolveSession(string Id, string State, string? Note, bool Said = false);

/// <summary>
/// <c>After</c> names the ask an <i>Ask again…</i> follows, and <c>ToPerson</c> keeps its answer for the person; both absent
/// is a first ask whose answer goes to the session.
/// </summary>
public sealed record SessionHelperAsk(string Id, string? Question, string? Agent, bool Said = false, string? After = null, bool ToPerson = false);

/// <summary><i>Pass to the lead</i> on an answer kept for the person: the ask, and the session the page names.</summary>
public sealed record SessionHelperPass(string Ask, string Session, bool Said = false);

public sealed record SessionHelpers(string Id);

public sealed record SessionPartnerAsk(string Id, string? Piece, string? Agent, IReadOnlyList<string>? Paths, bool Said = false);

public sealed record SessionPartners(string Id);

/// <summary>A piece's later act (<c>discard</c> or <c>hand-over</c>) on the session that asked it, for the driver's next look.</summary>
public sealed record SessionPartnerAct(string Id, string Piece, string Act);

public sealed record SessionHandOverAsk(string Id);

public sealed record SessionQuestion(string Id);

public sealed record SessionHandOverApprove(string Session, string? Ask, bool Said = false);

public sealed record SessionHandOverDecline(string Session, string? Ask, bool Said = false);

public sealed record SessionQuietLeave(string Id);

public sealed record SessionQuietCarryOn(string Id);

/// <summary>No option refuses the held request.</summary>
public sealed record SessionPermissionAnswer(string Session, string Request, string? Option);

public sealed record SessionAnswerAlone(string Session, bool GoOn);

public sealed record SessionSupervisor(string Id);

public sealed record SupervisorOverrule(string Id, string Decision);

/// <summary>An <c>op</c> is approved or not by <c>Approve</c>; none keeps or drops the ended session's staged changes by <c>Keep</c>.</summary>
public sealed record SessionMemoryDecide(string Session, string? Op, bool Approve = false, bool Keep = false);

public sealed record SessionDelete(string Id, bool Said = false, bool Help = false);

public sealed record SessionOpenFolder(string Id);

/// <summary>The terminal's verb sends the <c>Ask</c> alone with <c>Said</c>; the page sends the <c>Session</c> too, which the press refuses without.</summary>
public sealed record SessionHelperApprove(string Ask, string? Session, bool Said = false);

/// <summary>As <see cref="SessionHelperApprove"/>.</summary>
public sealed record SessionHelperDecline(string Ask, string? Session, bool Said = false);

// The reads, named for their route with a Read where another type holds the name. A read told by event also carries the page's
// `held`, which the module reads off any request.

public sealed record SessionOpeningsRead(IReadOnlyList<string>? Ids);

public sealed record SessionRelatedRead(string Session);

public sealed record SessionSpentRead(IReadOnlyList<string>? Ids);

public sealed record SessionRecordRead(string Id);

/// <summary>The sessions a list shows, each with the tree its record names; a row naming no id or no tree is left out.</summary>
public sealed record SessionWhereRead(IReadOnlyList<SessionTreeNamed>? Sessions);

public sealed record SessionTreeNamed(string? Id, string? Tree);

public sealed record SessionMemoryRead(string Session);

/// <summary>No ids is every session; the terminal's <c>session list</c> sends its <c>Group</c>, <c>Repository</c> and <c>Json</c> with <c>Said</c>.</summary>
public sealed record SessionGroupsRead(IReadOnlyList<string>? Ids, string? Group, string? Repository, bool Said = false, bool Json = false);

/// <summary>Archive these sessions, or bring them back by <c>Archived</c> false.</summary>
public sealed record SessionArchiveMark(bool Archived, IReadOnlyList<string>? Ids, bool Said = false);

/// <summary>A <c>Session</c> searches its own words; none, a page of sessions by <c>Offset</c> and <c>Limit</c>.</summary>
public sealed record SessionSearchRead(string Q, string? Session, long? Offset, long? Limit);
