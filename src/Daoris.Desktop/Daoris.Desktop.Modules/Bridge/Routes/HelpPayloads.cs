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
