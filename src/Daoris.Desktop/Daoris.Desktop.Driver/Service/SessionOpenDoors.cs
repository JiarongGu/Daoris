using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>The destination's open result. A held answer admits no delivery or adoption.</summary>
public sealed record DestinationOpen(string? SessionId, string Message, string? Opens = null,
    IReadOnlyList<JsonElement>? Transfer = null, bool Held = false, bool Gone = false,
    IReadOnlyList<string>? Candidates = null)
{
    /// <summary>
    /// That the host was asked to move the words and echoed the key (FOUNDATION1d-ii part 3; its <c>opens</c> mark 2): its open moved what
    /// <see cref="Moved"/> names. False where it was not asked, a host whose mark is below 2 or that keeps none, so the old record's words are
    /// still the caller's to move as today.
    /// </summary>
    public bool Moves { get; init; }

    /// <summary>
    /// Every word the open moved, in order, as its transfer answers them: delivered from the destination by <see cref="MovedWord.Id"/>, never
    /// by the old record's ids. Read as moved whatever <see cref="Moves"/> says: a host whose mark is below 2 replaying a newer host's receipt
    /// answers what that host moved.
    /// </summary>
    public IReadOnlyList<MovedWord> Moved { get; init; } = [];

    /// <summary>
    /// The opened record's adapter, repository and workspace as the host recorded them (FOUNDATION1d-ii part 5): what an open's watchers are
    /// told, as the unkeyed door tells them, a driven record naming its quest's receiver. Null where the answer names none.
    /// </summary>
    public (string? Adapter, string? Repository, string? Workspace) Recorded { get; init; }
}

/// <summary>
/// What a keyed open is asked to move (FOUNDATION1d-ii part 3, the design §3.4 rule 3): <paramref name="From"/>, the record the words move from,
/// the open's own <c>from</c> or a start's source where it names none; <paramref name="Words"/>, the ids the course named; <paramref name="Read"/>,
/// the ids it saw waiting there when it read, so the person's words kept since move too.
/// </summary>
public sealed record MoveWords(string From, IReadOnlyList<string> Words, IReadOnlyList<string> Read)
{
    /// <summary>
    /// The key a chat's keyed open is sent under (FOUNDATION1f-ii part a): the move record's <c>opens</c>, written before the open, so a retried
    /// open whose answer was lost reuses it; null for a key of the open's own (<see cref="ChatKey"/>).
    /// </summary>
    public string? Opens { get; init; }

    /// <summary>A new key for a chat's keyed open.</summary>
    public static string ChatKey() => "chat-move/" + Guid.NewGuid().ToString("N");

    /// <summary>A new key for a driven record's keyed open (FOUNDATION1f-iv part a): the carry-on's move record's <c>opens</c>, written before the open.</summary>
    public static string DrivenKey() => "drive-move/" + Guid.NewGuid().ToString("N");
}

/// <summary>
/// One word a keyed open moved: its id on the record it left (<paramref name="Word"/>) and on the destination (<paramref name="Id"/>, which a
/// moved word keeps), its producer key or none, its transfer id and its place.
/// </summary>
public sealed record MovedWord(string Word, string Id, string? Key, string Transfer, int Order);

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
    /// Whether the host's marks keep the moving open and the head (FOUNDATION1o, the design §3.4 rule 4, §19.11): <c>opens</c> 2 or more and
    /// <c>head</c> 1 or more, so a word for a record that winds up or moves is kept on its <c>said</c>, which the open moves and the say door
    /// follows past, rather than held by the shell. False for a host without both, or one that does not answer the protocol; never throws for it.
    /// </summary>
    public async Task<bool> KeepsAsync(CancellationToken ct = default)
    {
        if (await HeadAsync(ct).ConfigureAwait(false) < 1) return false;
        lock (_marks) return _opens >= 2;
    }

    /// <summary>
    /// Opens a driven or chat destination. The body's fields are the existing open request, without an opens member.
    /// A caller recovering a lost answer passes retry; each retry re-reads protocol before it sends anything. The moves that hand a part words
    /// open through it (FOUNDATION1d-ii parts 4 and 5: <see cref="ServiceClient.OpenChatMovingAsync"/>, <see cref="ServiceClient.OpenSessionMovingAsync"/>).
    /// </summary>
    /// <param name="moves">
    /// The words the open is asked to move (FOUNDATION1d-ii part 3), sent as the request's <c>moves</c> only to a host whose <c>opens</c> mark
    /// is 2 or more and only beside the key; any <c>moves</c> the body carries is never sent. Against an older host the answer's
    /// <see cref="DestinationOpen.Moves"/> is false, and the caller moves the words itself as today.
    /// </param>
    public async Task<DestinationOpen> OpenAsync(
        string path, string body, string key, string left, bool retry = false, CancellationToken ct = default, MoveWords? moves = null)
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
            var moving = keyed && _opens >= 2 && moves is not null;
            var request = ServiceClient.WriteJson(writer =>
            {
                using var json = JsonDocument.Parse(body);
                writer.WriteStartObject();
                foreach (var field in json.RootElement.EnumerateObject())
                    if (field.Name is not ("opens" or "moves")) field.WriteTo(writer);
                if (keyed) writer.WriteString("opens", key);
                if (moving) WriteMoves(writer, moves!);
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
                var moved = transfer.Select(MovedOf).ToArray();
                _lost.Remove(key);
                return new(id, message, echo, transfer)
                {
                    Moves = moving && echo == key, Moved = moved,
                    Recorded = (Text(record, "adapter"), Text(record, "repository"), Text(record, "workspace")),
                };
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

    private static void WriteMoves(Utf8JsonWriter writer, MoveWords moves)
    {
        writer.WriteStartObject("moves");
        writer.WriteString("from", moves.From);
        writer.WriteStartArray("words");
        foreach (var word in moves.Words) writer.WriteStringValue(word);
        writer.WriteEndArray();
        writer.WriteStartArray("read");
        foreach (var word in moves.Read) writer.WriteStringValue(word);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// One element of a transfer as the host answers it. An element naming no <c>id</c>, as a host whose mark is below 2 replays a newer
    /// host's receipt, reads as its word's own id, which a moved word keeps; one naming no word, no transfer id or no whole order does not read.
    /// </summary>
    private static MovedWord MovedOf(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "word") is not { Length: > 0 } word
            || Text(element, "transfer") is not { Length: > 0 } transfer
            || !element.TryGetProperty("order", out var order) || order.ValueKind != JsonValueKind.Number || !order.TryGetInt32(out var place))
            throw new JsonException("A word of the keyed open's transfer does not read.");
        return new(word, Text(element, "id") is { Length: > 0 } id ? id : word, Text(element, "key") is { Length: > 0 } producer ? producer : null,
            transfer, place);
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
