using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Contracts.Confirmations;

// A local host's alone: a shared host has no door that is the person's alone. An ask waits with no clock; the window lists it and the
// person answers with the key, and at the Confirm the host makes the request as asked. The asker polls its one, and withdraws it with
// the token its ask is answered with. An id an earlier start held reads <c>restarted</c>.

/// <summary>What waits, oldest first.</summary>
public sealed record ListConfirmations : IRoute<ConfirmationResponse[]>
{
    public static string Method => "GET";
    public static string Path => "/api/confirmations";
}

/// <summary>
/// What waits after <c>Since</c> (<c>&lt;epoch&gt;.&lt;n&gt;</c>), answered as soon as it moves, one past its time at its time, or at
/// <c>Wait</c> seconds (30 at most) as it is; any other cursor at once. The host's stop answers a held wait at once.
/// </summary>
public sealed record WaitConfirmations(string? Since = null, int? Wait = null) : IRoute<ConfirmationsWaitingResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/confirmations/waiting";
}

public sealed record ReadConfirmation(string Id) : IRoute<ConfirmationResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/confirmations/{id}";
}

/// <summary>
/// The ask: the request to be made, as sent (<c>Path</c> with its query, <c>Body</c> as text, <c>Session</c> the asker's record, absent for
/// a terminal's). The handler reads the body itself, no further than its limit.
/// </summary>
[OwnBody]
public sealed record AskConfirmation(string? Method, string? Path, string? Body, string? Session) : IRoute<ConfirmationAskedResponse>
{
    static string IRoute<ConfirmationAskedResponse>.Method => "POST";
    static string IRoute<ConfirmationAskedResponse>.Path => "/api/confirmations";
}

/// <summary>The person's yes, behind the key: the host makes the request, and answers how its door took it.</summary>
public sealed record ConfirmConfirmation(string Id) : IRoute<ConfirmationActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/confirmations/{id}/confirm";
}

public sealed record RefuseConfirmation(string Id) : IRoute<ConfirmationActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/confirmations/{id}/refuse";
}

/// <summary>An asker's withdrawal of its own ask, by the token its ask is answered with.</summary>
[OptionalBody]
public sealed record WithdrawConfirmation(string Id, string? Token = null) : IRoute<ConfirmationActionResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/confirmations/{id}/withdraw";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ListConfirmations))]
[JsonSerializable(typeof(WaitConfirmations))]
[JsonSerializable(typeof(ReadConfirmation))]
[JsonSerializable(typeof(AskConfirmation))]
[JsonSerializable(typeof(ConfirmConfirmation))]
[JsonSerializable(typeof(RefuseConfirmation))]
[JsonSerializable(typeof(WithdrawConfirmation))]
[JsonSerializable(typeof(ConfirmationResponse))]
[JsonSerializable(typeof(ConfirmationResponse[]))]
[JsonSerializable(typeof(ConfirmationsWaitingResponse))]
[JsonSerializable(typeof(ConfirmationActionResponse))]
[JsonSerializable(typeof(ConfirmationAskedResponse))]
internal sealed partial class ConfirmationJson : JsonSerializerContext;
