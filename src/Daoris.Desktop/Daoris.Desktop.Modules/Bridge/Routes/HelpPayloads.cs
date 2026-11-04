namespace Daoris.Desktop;

// The payloads of HelpRoutes' presses, each named for its route.

/// <summary>A blank workspace is the default's.</summary>
public sealed record StartHelp(string? Workspace);

/// <summary>No revision is a press with no card to stand for, which the driver refuses; <c>Taken</c> and <c>Left</c> are the draft editor's changes.</summary>
public sealed record HelpApply(string Id, string? Revision, IReadOnlyList<string>? Taken, IReadOnlyList<string>? Left);

public sealed record HelpDismiss(string Id);

/// <summary>No name clears it.</summary>
public sealed record HelpRename(string Id, string? Name, bool Said = false);

public sealed record HelpPin(string Id, bool Pinned, bool Said = false);

/// <summary>A blank workspace is the default's.</summary>
public sealed record HelpStartFrom(string Id, string? Workspace);

/// <summary>Told by event: the page's <c>held</c> rides beside it, which the module reads off any request.</summary>
public sealed record HelpProposalsRead(string Session);

public sealed record HelpPublishedRead(string Session);

public sealed record HelpWiredRead(string Session);

/// <summary>A page of conversations: <c>Offset</c> and <c>Limit</c> count them, <c>Q</c> searches, a blank workspace is every workspace's; the terminal's flags (<c>Said</c>, <c>All</c>, <c>Json</c>) ask for its words.</summary>
public sealed record HelpConversationsRead(
    string? Q, long? Offset, long? Limit, string? Workspace, bool Said = false, bool All = false, bool Json = false);
