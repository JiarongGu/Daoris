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
    // FOUNDATION1d-ii (§19.12): the say door's `continued`, naming the line's head; kept beside `opens` by the same run.
    private int _head;
    private readonly object _marks = new();
    private readonly HashSet<string> _lost = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _step = new(1, 1);

    internal SessionOpenDoors(ServiceClient service) => _service = service;

    internal void Observe(ServiceReply answer)
    {
        lock (_marks)
        {
            if (answer.Run != _run || answer.Run is null) _known = false;
        }
    }

    private async Task ReadProtocolAsync(CancellationToken ct)
    {
        var reply = await _service.SendAsync(ServiceVerb.Get, "/api/protocol", null, ct).ConfigureAwait(false);
        string? run = null;
        int opens = 0, head = 0;
        if (reply.Ok)
        {
            using var json = JsonDocument.Parse(reply.Payload);
            run = json.RootElement.ValueKind == JsonValueKind.Object ? Text(json.RootElement, "run") : null;
            if (run is not null && json.RootElement.TryGetProperty("marks", out var marks) && marks.ValueKind == JsonValueKind.Object)
            {
                opens = Mark(marks, "opens");
                head = Mark(marks, "head");
            }
        }

        lock (_marks)
        {
            (_run, _opens, _head, _known) = (run, opens, head, true);
        }
    }

    /// <summary>A mark's version as the protocol answers it; 0 for none, or one that is not a whole number.</summary>
    private static int Mark(JsonElement marks, string name) =>
        marks.TryGetProperty(name, out var mark) && mark.ValueKind == JsonValueKind.Number && mark.TryGetInt32(out var version) ? version : 0;

    /// <summary>
    /// The host's <c>head</c> mark (FOUNDATION1d-ii, the design §19.12): 1 where its say door refuses a word said to a record that is not its line's
    /// head as <c>continued</c>, naming the head, so the driver's own walk (<c>WordsNever.ContinuedIn</c>) stands aside for the service's
    /// judgement; 0 for a host without it, whose say keeps the driver's walk as today. Kept by the host's run, read again before this read once an
    /// answer named another run or none. A host that does not answer the protocol, or answers it unreadably, has none; never throws for it.
    /// </summary>
    public async Task<int> HeadAsync(CancellationToken ct = default)
    {
        bool known;
        lock (_marks) known = _known;
        if (!known)
        {
            try
            {
                await ReadProtocolAsync(ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or IOException or JsonException or DriverException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return 0;
            }
        }

        lock (_marks) return _head;
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
