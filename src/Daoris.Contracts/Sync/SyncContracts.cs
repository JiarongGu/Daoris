using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Sync;

// A local host's sync doors: one pass for one circle, where it stands, a machine's retirement at the remote, and where this machine's
// claim on a quest stands. A shared host has none: it is what they sync with.

/// <summary>One pass for one circle: the quests' fetch, rebase and push, then the session records, the channel and the code maps, both ways.</summary>
public sealed record SyncCircle([property: FromQuery] string? Workspace = null) : IRoute<SyncResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sync";
}

/// <summary>Where a circle stands, read from the store without reaching the remote.</summary>
public sealed record ReadSyncStanding(string? Workspace = null) : IRoute<SyncStandingResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/sync";
}

/// <summary>The person's retirement of a machine the circle's remote still counts, asked at the remote.</summary>
public sealed record RetireSyncMachine(string Machine, [property: FromQuery] string? Workspace = null) : IRoute<MachineRetiredResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/sync/machines/{machine}/retire";
}

/// <summary>Where this machine's claim on one quest stands.</summary>
public sealed record ReadClaim(string Id) : IRoute<QuestClaimResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/quests/{id}/claim";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SyncCircle))]
[JsonSerializable(typeof(ReadSyncStanding))]
[JsonSerializable(typeof(RetireSyncMachine))]
[JsonSerializable(typeof(ReadClaim))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(SyncStandingResponse))]
[JsonSerializable(typeof(MachineRetiredResponse))]
[JsonSerializable(typeof(QuestClaimResponse))]
internal sealed partial class SyncJson : JsonSerializerContext;
