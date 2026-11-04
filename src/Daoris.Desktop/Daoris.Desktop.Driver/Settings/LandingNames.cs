namespace Daoris.Driver;

/// <summary>
/// What a pattern is expanded from: the session's quest (or the session itself), its title's words, the repository, and the
/// ticket its rule's key found (PROCESS1b-i), the session standing in where it found none.
/// </summary>
public sealed record LandingNames(string Quest, string Slug, string Repository, string Session, string? Ticket = null);
