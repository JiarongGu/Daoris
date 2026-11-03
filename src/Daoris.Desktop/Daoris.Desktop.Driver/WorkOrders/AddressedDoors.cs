using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The local host's doors of the messages to one part, as the driver asks them: what waits for one part it runs, alone and on its line's
/// own channel, and that part's acknowledgment. The read is how the driver says it reads those messages: a host from before the doors
/// answers not found, so nothing is handed and they wait for the part's own read.
/// </summary>
public sealed class AddressedDoors
{
    private readonly ServiceClient _service;

    internal AddressedDoors(ServiceClient service) => _service = service;

    /// <summary>The messages waiting for <paramref name="session"/>, each with its line's acknowledgment; null where the host holds no such session or door.</summary>
    /// <exception cref="DriverException">The host answered something other than the list.</exception>
    public async Task<IReadOnlyList<ChannelItemView>?> WaitingAsync(string session, CancellationToken ct = default)
    {
        var answer = await _service.SendAsync(ServiceVerb.Get, $"/api/channel/addressed?session={Uri.EscapeDataString(session)}", null, ct).ConfigureAwait(false);
        if (answer.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!answer.Ok) throw new DriverException($"the service would not read what waits for session `{session}`: {answer.Payload}");

        using var document = JsonDocument.Parse(answer.Payload);
        return document.RootElement.ValueKind == JsonValueKind.Object
               && document.RootElement.TryGetProperty("messages", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? [.. listed.EnumerateArray().Select(ChannelView.ReadItem).OfType<ChannelItemView>()]
            : throw new DriverException($"the service answered something other than what waits for session `{session}`: {answer.Payload}");
    }

    /// <summary>One part's acknowledgment of the messages to it by their ids; the ids it changed, none from a host without the door.</summary>
    /// <exception cref="DriverException">The host refused the acknowledgment.</exception>
    public async Task<IReadOnlyList<string>> AcknowledgeAsync(string session, string state, IReadOnlyList<string> messages, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { session, state, messages });
        var (ok, status, payload, root) = await _service.PostJsonAsync("/api/channel/addressed/ack", body, ct).ConfigureAwait(false);
        if (status == (int)System.Net.HttpStatusCode.NotFound) return [];
        if (!ok) throw new DriverException($"the service would not keep session `{session}`'s {state} of the messages to it: {payload}");

        return root is { ValueKind: JsonValueKind.Object } answered
               && answered.TryGetProperty("acknowledged", out var changed) && changed.ValueKind == JsonValueKind.Array
            ? [.. changed.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!)]
            : [];
    }
}

/// <summary>The client's doors of the messages to one part, as the work orders' own member of it, holding no state of its own.</summary>
public static class AddressedDoorsOfClient
{
    extension(ServiceClient client)
    {
        public AddressedDoors Addressed => new(client);
    }
}
