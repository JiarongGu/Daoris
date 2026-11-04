using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Brainstorms;

// The doors a terminal's `daoris brainstorm` and `daoris meeting` reach: the person's question written as the request the driver's
// look freezes into a packet, the packet and document read back, the person's send, stop and clear. A brainstorm is kept where it was
// asked and is never a quest operation, so a local host serves it to a caller on this machine alone. The passes are the driver's;
// nothing here runs one. `Kind` is `brainstorm` (none) or `meeting`; anything else is the caller's mistake.

/// <summary>A person's question for a brainstorm, or a meeting's. Every part may be left out, and the desk refuses the blank naming which.</summary>
/// <param name="Repository">The one whose knowledge the packet is frozen from; none for the question alone.</param>
/// <param name="Id">The caller's own, which makes the ask idempotent.</param>
/// <param name="Sources">The repository's paths the packet takes in; a meeting takes none.</param>
public sealed record AskBrainstorm(
    string? Question, string? Repository = null, IReadOnlyList<string?>? Agents = null, string? Kind = null, string? Id = null,
    IReadOnlyList<string?>? Sources = null) : IRoute<BrainstormActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/brainstorms";
}

public sealed record ListBrainstorms : IRoute<BrainstormResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/brainstorms";
}

public sealed record ShowBrainstorm(string Id) : IRoute<BrainstormShownResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/brainstorms/{id}";
}

/// <summary>The person's send of a brainstorm's packet, or of a meeting's round.</summary>
/// <param name="Round">The round a meeting's send is for; a brainstorm has one packet and no rounds.</param>
[OptionalBody]
public sealed record SendBrainstorm(string Id, [property: FromQuery] string? Kind = null, [property: FromQuery] int? Round = null)
    : IRoute<BrainstormActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/brainstorms/{id}/send";
}

[OptionalBody]
public sealed record StopBrainstorm(string Id, [property: FromQuery] string? Kind = null) : IRoute<BrainstormActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/brainstorms/{id}/stop";
}

public sealed record ClearBrainstorm(string Id, string? Kind = null) : IRoute<BrainstormActionResponse>
{
    public static string Method => "DELETE";
    public static string Path => "/api/brainstorms/{id}";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AskBrainstorm))]
[JsonSerializable(typeof(ListBrainstorms))]
[JsonSerializable(typeof(ShowBrainstorm))]
[JsonSerializable(typeof(SendBrainstorm))]
[JsonSerializable(typeof(StopBrainstorm))]
[JsonSerializable(typeof(ClearBrainstorm))]
[JsonSerializable(typeof(BrainstormResponse[]))]
[JsonSerializable(typeof(BrainstormShownResponse))]
[JsonSerializable(typeof(BrainstormActionResponse))]
internal sealed partial class BrainstormJson : JsonSerializerContext;
