namespace Daoris.Knowledge;

/// <summary>Who made an operation, from the caller's record at the call (§1), never from a tool's arguments; and the call's instant.</summary>
public sealed record MemoryAuthor(
    string Session, string Workspace, string? Quest, string Door, string? DoorVersion, string? Role, string? RoleRevision, DateTimeOffset At);
