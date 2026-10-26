using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>The destination's open result. A held answer admits no delivery or adoption.</summary>
public sealed record DestinationOpen(string? SessionId, string Message, string? Opens = null,
    IReadOnlyList<JsonElement>? Transfer = null, bool Held = false, bool Gone = false,
    IReadOnlyList<string>? Candidates = null);

/// <summary>Keyed opens and protocol negotiation, over one client's host runs (D212 §19.5, §19.12).</summary>
public sealed class SessionOpenDoors
{
    private readonly ServiceClient _service;
    private string? _run;
    private bool _known;
    private int _opens;
    private readonly HashSet<string> _lost = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _step = new(1, 1);

    internal SessionOpenDoors(ServiceClient service) => _service = service;

    internal void Observe(ServiceReply answer)
    {
        if (answer.Run != _run || answer.Run is null) _known = false;
    }

    private async Task ReadProtocolAsync(CancellationToken ct)
    {
        var reply = await _service.SendAsync(ServiceVerb.Get, "/api/protocol", null, ct).ConfigureAwait(false);
        _run = null;
        _opens = 0;
        if (reply.Ok)
        {
            using var json = JsonDocument.Parse(reply.Payload);
            _run = Text(json.RootElement, "run");
            if (_run is not null && json.RootElement.TryGetProperty("marks", out var marks)
                && marks.TryGetProperty("opens", out var mark) && mark.TryGetInt32(out var version)) _opens = version;
        }
        _known = true;
    }

    /// <summary>
    /// Opens a driven or chat destination. The body's fields are the existing open request, without an opens member.
    /// A caller recovering a lost answer passes retry; each retry re-reads protocol before it sends anything.
    /// No production move uses this door until its adoption row.
    /// </summary>
    public async Task<DestinationOpen> OpenAsync(string path, string body, string key, string left, bool retry = false, CancellationToken ct = default)
    {
        if (path is not ("/api/sessions" or "/api/sessions/chat")) throw new ArgumentException("Not a destination open door.", nameof(path));
        await _step.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            retry |= _lost.Contains(key);
            if (retry) _lost.Add(key);
            if (retry || !_known) await ReadProtocolAsync(ct).ConfigureAwait(false);
            if (retry && _opens == 0) return await HoldAsync(left, ct).ConfigureAwait(false);
            var keyed = _opens >= 1;
            var request = ServiceClient.WriteJson(writer =>
            {
                using var json = JsonDocument.Parse(body);
                writer.WriteStartObject();
                foreach (var field in json.RootElement.EnumerateObject())
                    if (field.Name != "opens") field.WriteTo(writer);
                if (keyed) writer.WriteString("opens", key);
                writer.WriteEndObject();
            });
            ServiceReply reply;
            try
            {
                reply = await _service.SendAsync(ServiceVerb.Post, path, request, ct).ConfigureAwait(false);
                using var answer = JsonDocument.Parse(reply.Payload);
                var root = answer.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new JsonException("The open's answer is not an object.");
                var echo = Text(root, "opens");
                if (retry && echo != key) return await HoldAsync(left, ct).ConfigureAwait(false);
                var message = Text(root, "message") ?? Text(root, "error") ?? reply.Payload;
                if (!reply.Ok)
                {
                    _lost.Remove(key);
                    return new(null, message, echo, Gone: Text(root, "code") == "gone");
                }
                var id = root.TryGetProperty("session", out var record) ? Text(record, "id") : null;
                if (string.IsNullOrWhiteSpace(id)) throw new JsonException("The open's answer names no destination.");
                IReadOnlyList<JsonElement> transfer = [];
                if (echo == key)
                {
                    if (!root.TryGetProperty("transfer", out var words) || words.ValueKind != JsonValueKind.Array)
                        throw new JsonException("The keyed open's answer has no transfer list.");
                    transfer = words.EnumerateArray().Select(word => word.Clone()).ToArray();
                }
                _lost.Remove(key);
                return new(id, message, echo, transfer);
            }
            catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException)
            {
                _lost.Add(key);
                _known = false;
                return await HoldAsync(left, ct).ConfigureAwait(false);
            }
        }
        finally { _step.Release(); }
    }

    private async Task<DestinationOpen> HoldAsync(string left, CancellationToken ct)
    {
        // Candidates are evidence for the person, never a heuristic for adopting one.
        var candidates = new List<string>();
        try
        {
            var reply = await _service.SendAsync(ServiceVerb.Get, "/api/sessions?includeClosed=true", null, ct).ConfigureAwait(false);
            if (reply.Ok)
            {
                using var json = JsonDocument.Parse(reply.Payload);
                if (json.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var record in json.RootElement.EnumerateArray())
                        if (Text(record, "from") == left && Text(record, "id") is { } id) candidates.Add(id);
            }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException) { }
        return new(null, $"The open's answer was lost. Held for you; records from `{left}`: {string.Join(", ", candidates)}.",
            Held: true, Candidates: candidates);
    }
}
