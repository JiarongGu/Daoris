namespace Daoris.Knowledge;

/// <summary>A finished tree's record, as kept: what the driver posted, when it is kept, and its sessions.</summary>
public sealed record WorkRecord(WorkRecordPost Post, DateTimeOffset At, int Schema, IReadOnlyList<WorkRecordSession> Sessions);
