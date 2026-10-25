namespace Daoris.Driver;

/// <summary>The models a conversation ran on, as the service keeps them on the record (<c>ran</c>): each turn's entry, bounded.</summary>
public sealed record ActorEntry(DateTimeOffset At, string? Acknowledged = null, string? Effort = null);
public sealed record ActorRan(IReadOnlyList<string> Models, int More, IReadOnlyList<ActorEntry> Entries, int EntriesMore);
