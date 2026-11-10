using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's half of the place's mark: every move out of a running state, the orphan sweep's conclusion of a cut-off run among them,
/// hands the ledger the uncommitted paths of its own its turn's end found in its place; a move into a running state hands none.
/// </summary>
public sealed class PlaceEndMoveTests
{
    private readonly List<(string Path, string Body)> _posted = [];
    private readonly List<string> _ended = [];

    private ServiceClient Client() => new(
        "http://stand-in", null, new HttpClient(new StandIn(request =>
        {
            if (request.Method != HttpMethod.Post) return (HttpStatusCode.OK, "[]");
            _posted.Add((request.RequestUri!.PathAndQuery, request.Content!.ReadAsStringAsync().Result));
            return (HttpStatusCode.OK, """{"session":{"id":"s-1a2b3c4d","repository":"reports","state":"stopped"},"message":"ok"}""");
        })))
    {
        PlaceEnds = (id, _) =>
        {
            _ended.Add(id);
            return Task.FromResult<IReadOnlyList<string>?>(id == "s-1a2b3c4d" ? ["draft.md"] : null);
        },
    };

    [Fact]
    public async Task A_sweep_s_conclusion_marks_the_paths_its_turn_left_and_a_move_into_working_marks_none()
    {
        using var service = Client();

        await service.AdvanceAsync("s-1a2b3c4d", "stopped", interrupted: true);
        await service.AdvanceAsync("s-1a2b3c4d", "working");
        await service.AdvanceAsync("s-9f8e7d6c", "awaiting-person");

        Assert.Equal(["s-1a2b3c4d", "s-9f8e7d6c"], _ended);
        var bodies = _posted.Select(posted => JsonDocument.Parse(posted.Body).RootElement).ToList();
        Assert.Equal(["draft.md"], bodies[0].GetProperty("holdsPlace").EnumerateArray().Select(path => path.GetString()));
        Assert.False(bodies[1].TryGetProperty("holdsPlace", out _));
        Assert.False(bodies[2].TryGetProperty("holdsPlace", out _));
    }

    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, body) = answer(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
