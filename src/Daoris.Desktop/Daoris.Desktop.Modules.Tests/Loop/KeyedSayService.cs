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
            ("POST", ["api", "sessions", var id, "state"]) => (HttpStatusCode.OK,
                $$"""{"session":{"id":"{{Uri.UnescapeDataString(id)}}","state":"{{JsonDocument.Parse(body).RootElement.GetProperty("state").GetString()}}"},"message":"Moved."}"""),
            ("POST", ["api", "sessions", var id, "say"]) => Say(Uri.UnescapeDataString(id), body),
            ("POST", _) => (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}"""),
            _ => (HttpStatusCode.NotFound, ""),
        };
        return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
    }

    private (HttpStatusCode, string) Say(string session, string body)
    {
        using var said = JsonDocument.Parse(body);
        var key = said.RootElement.TryGetProperty("key", out var named) ? named.GetString() : null;
        var text = said.RootElement.GetProperty("text").GetString()!;
        var running = said.RootElement.TryGetProperty("running", out var flag) && flag.GetBoolean();
        lock (_gate)
        {
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
