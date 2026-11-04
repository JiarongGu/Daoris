namespace Daoris.Desktop;

// The payloads of ConsoleRoutes' reads and press, TraceRoutes' and RegistryRoutes', each named for its route.

/// <summary>No <c>After</c> is everything held: a page opening a drawer has seen nothing.</summary>
public sealed record TailSession(string Id, long? After);

public sealed record SessionStreamsRead(string Id);

/// <summary>The <c>Key</c> of one of the session's tasks, as <c>SESSION_STREAMS</c> names it.</summary>
public sealed record StopTask(string Id, string Key);

/// <summary>A commit, a session or a quest by its <c>Kind</c>, and its <c>Id</c>.</summary>
public sealed record TraceRead(string Kind, string Id);

public sealed record RegistryRefresh(string Repository);
