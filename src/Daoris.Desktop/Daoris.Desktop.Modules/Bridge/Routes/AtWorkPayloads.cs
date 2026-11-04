namespace Daoris.Desktop;

/// <summary>An ask's view is keyed by its <c>Ask</c>, a session's by its <c>Session</c>.</summary>
public sealed record SessionAtWorkRead(string? Ask, string? Session);
