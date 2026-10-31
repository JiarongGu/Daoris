using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KNOWCENTRAL2d (the memory coordinator design §1): the driver's half of the commit points. A session's end hands the service its usage
/// history's turn lines, its own and those of the sessions it continues, so each row it staged names the model of the turn its instant falls
/// in; a move that ends nothing hands none. Its connector names the role and revision its start took, read at each call.
/// </summary>
public sealed class MemoryCommitPointTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "memory-turns", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<(string Path, string Body)> _posted = [];

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private void Turned(string session, long seq, DateTimeOffset started, string model) =>
        UsageHistory.Turned(
            _home, new TurnBinding("fake", "a1", "fake-door", UsageHistory.PerTurn),
            new TurnSeen(session, seq, started, started.AddMinutes(1), null, new TurnModels(model, null, null, null), false), started.AddMinutes(1));

    private ServiceClient Client()
    {
        var sessions = """
            [{"id":"3f9c2a71","repository":"Owner","state":"working","from":"5e6d7c81"},
             {"id":"5e6d7c81","repository":"Owner","state":"stopped"},
             {"id":"9a8b7c61","repository":"Owner","state":"working"}]
            """;
        var standIn = new StandIn(request =>
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/sessions?", StringComparison.Ordinal)) return (HttpStatusCode.OK, sessions);
            if (request.Method != HttpMethod.Post) return null;
            _posted.Add((path, request.Content!.ReadAsStringAsync().Result));
            return (HttpStatusCode.OK, """{"session":{"id":"3f9c2a71","repository":"Owner","state":"completed"},"message":"ok"}""");
        });
        return new ServiceClient("http://stand-in", null, new HttpClient(standIn)) { TurnLines = sessions => ServiceClient.LinesOf(UsageHistory.TurnLinesOf(_home, sessions), turn => new ServiceClient.TurnLine(turn.Seq, turn.Started, turn.Ended, turn.Model)) };
    }

    /// <summary>🔴 A completion hands the turn lines of the session and of the session it continues, and no other session's.</summary>
    [Fact]
    public async Task A_sessions_end_hands_its_turn_lines_and_those_of_the_session_it_continues()
    {
        Turned("3f9c2a71", 4, At, "claude-opus-5");
        Turned("5e6d7c81", 2, At.AddHours(-1), "gpt-5");
        Turned("9a8b7c61", 1, At, "other-model");
        using var service = Client();

        await service.AdvanceAsync("3f9c2a71", "completed");

        var (path, body) = Assert.Single(_posted);
        Assert.Equal("/api/sessions/3f9c2a71/state", path);
        var turns = JsonDocument.Parse(body).RootElement.GetProperty("turns");
        Assert.Equal(["3f9c2a71", "5e6d7c81"], turns.EnumerateObject().Select(each => each.Name).Order(StringComparer.Ordinal));
        var line = Assert.Single(turns.GetProperty("3f9c2a71").GetProperty("turns").EnumerateArray());
        Assert.Equal((4, "claude-opus-5"), (line.GetProperty("seq").GetInt64(), line.GetProperty("model").GetString()));
        Assert.Equal(At, line.GetProperty("started").GetDateTimeOffset());
        Assert.Equal("gpt-5", Assert.Single(turns.GetProperty("5e6d7c81").GetProperty("turns").EnumerateArray()).GetProperty("model").GetString());
    }

    /// <summary>🔴 A move that ends nothing hands no turn lines; a stop and a failure, which a person's Keep may release later, do.</summary>
    [Fact]
    public async Task Only_a_move_that_ends_the_session_hands_turn_lines()
    {
        Turned("3f9c2a71", 4, At, "claude-opus-5");
        using var service = Client();

        await service.AdvanceAsync("3f9c2a71", "working");
        await service.AdvanceAsync("3f9c2a71", "stopped");
        await service.AdvanceAsync("3f9c2a71", "failed");

        Assert.Equal([false, true, true], _posted.Select(each => JsonDocument.Parse(each.Body).RootElement.TryGetProperty("turns", out _)));
    }

    /// <summary>🔴 The connector a session is offered names the role and revision its start took; one that took none names none.</summary>
    [Fact]
    public void The_connector_names_the_role_its_start_took()
    {
        var named = KnowledgeConnector.SessionScope("C:/somewhere/data", "s1a2b3c4", scope: null, role: ("builder", 3));
        var none = KnowledgeConnector.SessionScope("C:/somewhere/data", "s1a2b3c4", scope: null);

        Assert.Equal(("builder", "3"), (named[KnowledgeConnector.RoleVariable], named[KnowledgeConnector.RoleRevisionVariable]));
        Assert.False(none.ContainsKey(KnowledgeConnector.RoleVariable));
    }

    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
