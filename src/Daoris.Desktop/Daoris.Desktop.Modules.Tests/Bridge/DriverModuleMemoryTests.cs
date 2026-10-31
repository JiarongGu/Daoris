using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// KNOWCENTRAL2e (the memory coordinator design §1, §4): the session page's door onto its memory. <c>SESSION_MEMORY</c> passes the
/// service's rows of a session through; <c>SESSION_MEMORY_DECIDE</c> is the person's decision on them, an awaiting end approved or
/// declined by its row, or an ended session's staged changes kept or dropped, a refusal answered as data in the service's words.
/// </summary>
public sealed class DriverModuleMemoryTests : DriverModuleBridge
{
    private const string Rows = """
        {"ops":[{"id":"o1","repository":"Owner","state":"awaiting","change":{"kind":"end","text":"The map is drawn in vectors now.","target":"m1","revision":1},
         "by":{"session":"s1","adapter":"claude-code"},"at":"2026-10-31T09:00:00Z","models":"opus"}]}
        """;

    private async Task<(DriverModule Module, StandIn Service)> UpAsync(StandIn standIn)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(standIn)));
        return (new DriverModule(Bus, loop), standIn);
    }

    /// <summary>The rows pass through as the service answers them, and a host with no door answers none.</summary>
    [Fact]
    public async Task SESSION_MEMORY_passes_the_sessions_rows_through()
    {
        var (module, service) = await UpAsync(new StandIn(request => request.RequestUri!.AbsolutePath == "/api/sessions/s1/memory" ? (HttpStatusCode.OK, Rows) : null));

        var read = await AnswerAsync(module, "SESSION_MEMORY", new { session = "s1" });
        var none = await AnswerAsync(module, "SESSION_MEMORY", new { session = "s2" });

        Assert.Contains("GET /api/sessions/s1/memory", service.Requests);
        var op = Assert.Single(read.GetProperty("ops").EnumerateArray());
        Assert.Equal(("o1", "awaiting", "end"), (op.GetProperty("id").GetString(), op.GetProperty("state").GetString(), op.GetProperty("change").GetProperty("kind").GetString()));
        Assert.Empty(none.GetProperty("ops").EnumerateArray());
    }

    /// <summary>An end is decided through its row and staged changes kept or dropped through the session's; a refusal is data, never thrown.</summary>
    [Fact]
    public async Task SESSION_MEMORY_DECIDE_decides_an_end_and_keeps_or_drops_staged_changes()
    {
        var (module, service) = await UpAsync(new StandIn(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/memory/o1/decide" => (HttpStatusCode.OK, """{"message":"The end `o1` is approved and applies next.","op":null,"moved":0}"""),
            "/api/sessions/s1/memory" => (HttpStatusCode.OK, """{"message":"2 staged change(s) of session `s1` dropped.","op":null,"moved":2}"""),
            "/api/sessions/s2/memory" => (HttpStatusCode.Conflict, """{"error":"Refused: session `s2` is running or unknown here."}"""),
            _ => null,
        }));

        var approved = await AnswerAsync(module, "SESSION_MEMORY_DECIDE", new { session = "s1", op = "o1", approve = true });
        var dropped = await AnswerAsync(module, "SESSION_MEMORY_DECIDE", new { session = "s1", keep = false });
        var refused = await AnswerAsync(module, "SESSION_MEMORY_DECIDE", new { session = "s2", keep = true });

        Assert.True(approved.GetProperty("decided").GetBoolean());
        Assert.Contains("approved", approved.GetProperty("message").GetString());
        Assert.True(dropped.GetProperty("decided").GetBoolean());
        Assert.False(refused.GetProperty("decided").GetBoolean());
        Assert.Equal("refused", refused.GetProperty("why").GetString());
        Assert.Contains("running", refused.GetProperty("message").GetString());
        Assert.Equal(
            [
                """POST /api/memory/o1/decide {"approve":true}""", """POST /api/sessions/s1/memory {"keep":false}""",
                """POST /api/sessions/s2/memory {"keep":true}""",
            ],
            service.Sent.Where(sent => sent.Contains("memory", StringComparison.Ordinal)));
    }

    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var sent = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            lock (Requests)
            {
                Requests.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
                Sent.Add($"{request.Method} {request.RequestUri!.PathAndQuery} {sent}".TrimEnd());
            }

            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, """{"error":"none"}""");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
