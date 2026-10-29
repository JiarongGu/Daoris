using System.Net;
using System.Text;
using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A service standing in: the records as given, the state door, and a say door that keeps one word per producer's key on a record
/// and answers a repeated key with that word, refusing <c>running</c> for a record <see cref="Winding"/> names. Every say is heard.
/// </summary>
internal sealed class KeyedSayService : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly List<(string Session, string Body, bool Refused)> _said = [];
    private readonly Dictionary<string, List<(string? Key, string Id, string Text)>> _kept = [];
    private int _words;

    public string Records { get; set; } = "[]";

    public Func<string, bool> Winding { get; init; } = _ => false;

    private readonly Dictionary<string, string> _settled = [];

    /// <summary>
    /// A producer's key whose word left <paramref name="record"/> (FOUNDATION1 row p, the design §3.4 rule 5): a say that sends <c>settled</c>
    /// under it, to any record where no word waits under it, is answered settled, naming <paramref name="record"/>, before any refusal and
    /// with nothing kept, as the say door answers.
    /// </summary>
    public KeyedSayService Settle(string record, string key)
    {
        lock (_gate) _settled[key] = record;
        return this;
    }

    private readonly Dictionary<string, string> _heads = [];

    /// <summary>
    /// The head of <paramref name="record"/>'s line where it is no longer the record itself (FOUNDATION1d-ii, the design §3.4 rule 2): a say to it is
    /// refused <c>continued</c>, naming <paramref name="head"/>, after the settled answer and before any other refusal, <c>running</c> among them, as a
    /// host with the <c>head</c> mark answers, and nothing is kept. A <paramref name="head"/> of null makes the record its line's head again.
    /// </summary>
    public KeyedSayService Continue(string record, string? head)
    {
        lock (_gate)
        {
            if (head is null) _heads.Remove(record);
            else _heads[record] = head;
        }

        return this;
    }

    /// <summary>Whether <c>GET /api/protocol</c> answers the <c>head</c> mark, as a host from row d-ii does; false answers none, as a host before.</summary>
    public bool HeadMark { get; init; }

    /// <summary>Every say to a record, in order, and whether it was refused as <c>running</c>.</summary>
    public IReadOnlyList<(string Body, bool Refused)> Said(string session)
    {
        lock (_gate) return [.. _said.Where(post => post.Session == session).Select(post => (post.Body, post.Refused))];
    }

    /// <summary>The words the record kept: one per key, and every keyless one.</summary>
    public int Kept(string session)
    {
        lock (_gate) return _kept.TryGetValue(session, out var words) ? words.Count : 0;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var (status, answer) = (request.Method.Method, parts) switch
        {
            ("GET", ["api", "sessions"]) => (HttpStatusCode.OK, Records),
            ("GET", ["api", "protocol"]) when HeadMark => (HttpStatusCode.OK, """{"run":"keyed","marks":{"settled":1,"opens":1,"head":1}}"""),
            ("POST", ["api", "sessions", var id, "state"]) => (HttpStatusCode.OK,
                $$"""{"session":{"id":"{{Uri.UnescapeDataString(id)}}","state":"{{JsonDocument.Parse(body).RootElement.GetProperty("state").GetString()}}"},"message":"Moved."}"""),
            ("POST", ["api", "sessions", var id, "say"]) => Say(Uri.UnescapeDataString(id), body),
            ("POST", ["api", "sessions", var id, "added"]) => Added(Uri.UnescapeDataString(id)),
            ("POST", _) => (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}"""),
            _ => (HttpStatusCode.NotFound, ""),
        };
        return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
    }

    private readonly List<string> _added = [];

    /// <summary>The sessions whose person's words were kept on their ask (<c>POST /api/sessions/{id}/added</c>), in order: what <c>WordsOnAsk</c> asks.</summary>
    public IReadOnlyList<string> Added()
    {
        lock (_gate) return [.. _added];
    }

    private (HttpStatusCode, string) Added(string session)
    {
        lock (_gate) _added.Add(session);
        return (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}""");
    }

    private (HttpStatusCode, string) Say(string session, string body)
    {
        using var said = JsonDocument.Parse(body);
        var key = said.RootElement.TryGetProperty("key", out var named) ? named.GetString() : null;
        var text = said.RootElement.GetProperty("text").GetString()!;
        var running = said.RootElement.TryGetProperty("running", out var flag) && flag.GetBoolean();
        lock (_gate)
        {
            var asksSettled = said.RootElement.TryGetProperty("settled", out var settledFlag) && settledFlag.ValueKind == JsonValueKind.True;
            if (asksSettled && key is not null && _settled.TryGetValue(key, out var left)
                && !(_kept.TryGetValue(session, out var waiting) && waiting.Any(each => each.Key == key)))
            {
                _said.Add((session, body, false));
                return (HttpStatusCode.OK, $$"""{"message":"Already settled.","settled":"{{left}}"}""");
            }

            if (_heads.TryGetValue(session, out var head))
            {
                _said.Add((session, body, false));
                return (HttpStatusCode.Conflict,
                    $$"""{"error":"Session `{{session}}` went on in `{{head}}`, its line's head: say it to that one.","refusal":"continued","head":"{{head}}"}""");
            }

            var refused = Winding(session) && !running;
            _said.Add((session, body, refused));
            if (refused) return (HttpStatusCode.Conflict, $$"""{"error":"Session `{{session}}` is working.","refusal":"running"}""");

            if (!_kept.TryGetValue(session, out var words)) _kept[session] = words = [];
            var word = key is not null ? words.FirstOrDefault(each => each.Key == key) : default;
            if (word.Id is null)
            {
                word = (key, $"w{++_words}", text);
                words.Add(word);
            }

            return (HttpStatusCode.OK, $$"""
                {"session":{"id":"{{session}}","state":"completed"},"message":"Kept for session `{{session}}` to go on with.",
                 "said":{"id":"{{word.Id}}","text":{{JsonSerializer.Serialize(word.Text)}},"at":"2026-10-03T09:00:00+00:00","files":[],"reopens":true}
                }
                """);
        }
    }
}
