namespace Daoris.Desktop;

// The payloads of TreesRoutes' reads, each named for its route.

/// <summary>The <c>Read</c> is the token the call waits under, which the module reads off any request so a later <c>READ_LEAVE</c> can stop it.</summary>
public sealed record SessionDiff(string Id, string? Read);

public sealed record ReadLeave(string Read);

public sealed record SessionProcess(string Id);

public sealed record SessionFiles(string Id);

public sealed record SessionFile(string Id, string Path);
