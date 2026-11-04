namespace Daoris.Knowledge;

/// <summary>A finished tree's record, as kept (the keep-size design §2): what the driver posted, when it was kept, and its sessions.</summary>
public sealed record WorkRecord(WorkRecordPost Post, DateTimeOffset At, int Schema, IReadOnlyList<WorkRecordSession> Sessions);
