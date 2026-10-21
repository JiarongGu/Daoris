using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a held wait on the service's confirmations answered (NOTIFY2j): the cursor to ask from next, and each confirmation waiting
/// for the person, whole, oldest first, as the door answers it.
/// </summary>
public sealed record ConfirmationsWaiting(string Since, IReadOnlyList<JsonElement> Waiting);

/// <summary>
/// The local host's confirmation doors, as the desktop's host reads them (NOTIFY2j, D196 point 4; <c>GET /api/confirmations</c> and
/// <c>/waiting</c>): a class over the service's client, reached as its <see cref="ServiceClient.Confirmations"/> (PART8). Reads only:
/// the person answers a confirmation in the window, with the key, at the service's own door.
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
}
