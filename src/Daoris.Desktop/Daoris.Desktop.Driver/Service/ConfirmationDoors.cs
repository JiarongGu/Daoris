using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a held wait on the service's confirmations answered (NOTIFY2j): the cursor to ask from next, and each confirmation waiting
/// for the person, whole, oldest first, as the door answers it.
/// </summary>
public sealed record ConfirmationsWaiting(string Since, IReadOnlyList<JsonElement> Waiting);

/// <summary>
/// How the person's answer at the service's door went (ONEWAY1n-i): <see cref="Done"/> where the door took it, a Confirm's request
/// made; otherwise the host's <see cref="Code"/> where it gave one (<c>not-made</c>, <c>restarted</c>) and its sentence.
/// </summary>
public sealed record ConfirmationAnswered(bool Done, string? Code, string Message);

/// <summary>
/// The local host's confirmation doors, as the desktop's host reads and answers them (NOTIFY2j, D196 point 4; ONEWAY1n-i;
/// <c>GET /api/confirmations</c>, <c>/waiting</c>, and <c>POST /api/confirmations/{id}/confirm</c> and <c>/refuse</c>): a class over the
/// service's client, reached as its <see cref="ServiceClient.Confirmations"/> (PART8). The window answers through the bridge's
/// <c>PERSON_CONFIRM</c>, which the person's Confirm reaches here.
/// </summary>
public sealed class ConfirmationDoors
{
    /// <summary>What waits, held until it moves.</summary>
    public const string Waiting = "/api/confirmations/waiting";

    /// <summary>What waits, as it is.</summary>
    public const string List = "/api/confirmations";

    private readonly ServiceClient _service;

    internal ConfirmationDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// What waits after <paramref name="since"/>, held up to <paramref name="waitSeconds"/> until it moves; null where the host has no
    /// such door (one from before NOTIFY2j), whose list <see cref="ListAsync"/> reads. Another refusal throws, carrying its status.
    /// </summary>
    public async Task<ConfirmationsWaiting?> WaitAsync(string? since, int waitSeconds, CancellationToken ct = default)
    {
        var path = $"{Waiting}?wait={waitSeconds}" + (since is null ? "" : $"&since={Uri.EscapeDataString(since)}");
        var reply = await _service.SendAsync(ServiceVerb.Get, path, null, ct).ConfigureAwait(false);
        if (reply.Status == 404) return null;
        if (!reply.Ok) throw new DriverException($"the service at {_service.BaseUrl} refused `{Waiting}` ({reply.Status}).");

        using var answer = JsonDocument.Parse(reply.Payload);
        var root = answer.RootElement;
        var next = root.TryGetProperty("since", out var cursor) && cursor.ValueKind == JsonValueKind.String ? cursor.GetString() : null;
        if (next is null || !root.TryGetProperty("waiting", out var waiting) || waiting.ValueKind != JsonValueKind.Array)
        {
            throw new DriverException($"the service at {_service.BaseUrl} answered `{Waiting}` with no cursor or no list.");
        }

        return new ConfirmationsWaiting(next, [.. waiting.EnumerateArray().Select(each => each.Clone())]);
    }

    /// <summary>What waits now, as <c>GET /api/confirmations</c> lists it. A refusal throws, carrying its status.</summary>
    public async Task<IReadOnlyList<JsonElement>> ListAsync(CancellationToken ct = default)
    {
        var reply = await _service.SendAsync(ServiceVerb.Get, List, null, ct).ConfigureAwait(false);
        if (!reply.Ok) throw new DriverException($"the service at {_service.BaseUrl} refused `{List}` ({reply.Status}).");

        using var answer = JsonDocument.Parse(reply.Payload);
        return answer.RootElement.ValueKind == JsonValueKind.Array
            ? [.. answer.RootElement.EnumerateArray().Select(each => each.Clone())]
            : throw new DriverException($"the service at {_service.BaseUrl} answered `{List}` with no list.");
    }

    /// <summary>
    /// The person's Confirm or Refuse of <paramref name="id"/> at the service's door: at a Confirm the host makes the request then and
    /// there, and this answers how its door took it. A refusal is answered, never thrown; a host that does not answer throws.
    /// </summary>
    public async Task<ConfirmationAnswered> AnswerAsync(string id, bool confirm, CancellationToken ct = default)
    {
        var path = $"{List}/{Uri.EscapeDataString(id)}/{(confirm ? "confirm" : "refuse")}";
        var reply = await _service.SendAsync(ServiceVerb.Post, path, null, ct).ConfigureAwait(false);
        string? code = null, said = null;
        try
        {
            using var answer = JsonDocument.Parse(reply.Payload);
            if (answer.RootElement.ValueKind == JsonValueKind.Object)
            {
                said = Text(answer.RootElement, reply.Ok ? "message" : "error");
                code = reply.Ok ? null : Text(answer.RootElement, "code");
            }
        }
        catch (JsonException)
        {
        }

        return new ConfirmationAnswered(reply.Ok, code, said ?? $"the service at {_service.BaseUrl} answered `{path}` with {reply.Status}.");
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
