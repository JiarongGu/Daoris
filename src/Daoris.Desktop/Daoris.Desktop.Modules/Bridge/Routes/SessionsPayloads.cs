namespace Daoris.Desktop;

// The payloads of SessionsRoutes' presses and reads, each named for its route. A flag the terminal's verbs send (`Said`, `Help`)
// is read as the page's flags are: true only where it says true.

public sealed record StopSession(string Id, bool Said = false);

public sealed record ResolveSession(string Id, string State, string? Note, bool Said = false);

public sealed record SessionGoOnNew(string Id, bool Said = false);

public sealed record SessionHelperAsk(string Id, string? Question, string? Agent, bool Said = false);

public sealed record SessionHelpers(string Id);

public sealed record SessionPartnerAsk(string Id, string? Piece, string? Agent, IReadOnlyList<string>? Paths, bool Said = false);

public sealed record SessionPartners(string Id);

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
