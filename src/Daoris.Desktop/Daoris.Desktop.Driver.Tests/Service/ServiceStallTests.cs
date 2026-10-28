using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 SERVICESTALL1: on 2026-10-28 a terminal's ask exited 2 with <i>no DAORIS_SERVICE_URL … nothing answered as its service</i> a
/// minute after a landing, on a machine at full load, and the service answered 200 in 62 ms ten seconds later. A request the
/// service does not answer in its bound is said as what was seen: the address, the bound, and that a read was asked once more
/// after a short wait, while a press that changes state is never asked again blindly and says what to look at instead. A
/// stand-in host answers late or never, through the client's own bound.
/// </summary>
public sealed class ServiceStallTests
{
    private const string Url = "http://localhost:5181";

    /// <summary>A host that answers each request as the test says, by its number, and counts what it was asked; a delay honours the client's bound.</summary>
    private sealed class StandIn(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Asked.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return answer(Asked.Count, request, ct);
        }
    }

    private static HttpResponseMessage Ok(string body) => new(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };

    /// <summary>Held until the client's bound cancels it, as a service that never answers is.</summary>
    private static async Task<HttpResponseMessage> Never(CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct);
        return Ok("");
    }

    private static ServiceHttp Doors(StandIn host, int boundMilliseconds = 100) =>
        new(Url, key: null, new HttpClient(host, disposeHandler: false) { Timeout = TimeSpan.FromMilliseconds(boundMilliseconds) })
        {
            RetryAfter = TimeSpan.Zero,
        };

    [Fact]
    public async Task A_read_the_service_answers_late_is_asked_once_more_and_succeeds()
    {
        var host = new StandIn((nth, _, ct) => nth == 1 ? Never(ct) : Task.FromResult(Ok("[1]")));
        using var doors = Doors(host);

        Assert.Equal("[1]", await doors.GetAsync("/api/sessions", CancellationToken.None));
        Assert.Equal(["GET /api/sessions", "GET /api/sessions"], host.Asked);
    }

    [Fact]
    public async Task A_read_that_is_a_reply_door_is_asked_once_more_too()
    {
        var host = new StandIn((nth, _, ct) => nth == 1 ? Never(ct) : Task.FromResult(Ok("{}")));
        using var doors = Doors(host);

        var reply = await doors.SendAsync(ServiceVerb.Get, "/api/asks/1", null, CancellationToken.None);

        Assert.Equal(200, reply.Status);
        Assert.Equal(2, host.Asked.Count);
    }

    [Fact]
    public async Task A_service_that_never_answers_a_read_is_said_as_what_was_seen()
    {
        var host = new StandIn((_, _, ct) => Never(ct));
        using var doors = Doors(host);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => doors.GetAsync("/api/sessions", CancellationToken.None));

        Assert.StartsWith($"the service at {Url} did not answer within 0.1 seconds to /api/sessions, and not again after a second try 0 seconds later.", error.Message);
        Assert.Contains($"{ServiceClient.UrlVariable} is not what is missing: the service may be busy", error.Message);
        Assert.Equal(["GET /api/sessions", "GET /api/sessions"], host.Asked);
    }

    [Fact]
    public async Task A_press_the_service_does_not_answer_is_never_asked_again_and_says_what_to_look_at()
    {
        var host = new StandIn((_, _, ct) => Never(ct));
        using var doors = Doors(host);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => doors.PostAsync("/api/asks", "{}", CancellationToken.None));

        Assert.StartsWith($"the service at {Url} did not answer within 0.1 seconds to /api/asks. This asked for a change, so it was not asked again", error.Message);
        Assert.Contains("look at what it changes", error.Message);
        Assert.Equal(["POST /api/asks"], host.Asked);

        var sent = await Assert.ThrowsAsync<HttpRequestException>(() => doors.SendAsync(ServiceVerb.Post, "/api/asks", "{}", CancellationToken.None));
        Assert.Contains("was not asked again", sent.Message);
        Assert.Equal(["POST /api/asks", "POST /api/asks"], host.Asked);
    }

    /// <summary>A refusal is an answer: the service's own sentence comes back, and nothing is asked twice.</summary>
    [Fact]
    public async Task A_refusal_is_an_answer_and_is_not_asked_again()
    {
        var host = new StandIn((_, _, _) => Task.FromResult(
            new HttpResponseMessage(System.Net.HttpStatusCode.Conflict) { Content = new StringContent("{\"error\":\"taken\"}") }));
        using var doors = Doors(host);

        var error = await Assert.ThrowsAsync<DriverException>(() => doors.GetAsync("/api/sessions", CancellationToken.None));

        Assert.Contains("taken", error.Message);
        Assert.Single(host.Asked);
    }

    /// <summary>The client a caller hands in carries the caller's deadline: its timeout ends a read once, as the cancellation HttpClient made it (SERVICESTALL1b).</summary>
    private static ServiceHttp Callers(StandIn host, int boundMilliseconds = 100) =>
        new(Url, key: null, new HttpClient(host, disposeHandler: false) { Timeout = TimeSpan.FromMilliseconds(boundMilliseconds) });

    /// <summary>🔴 SERVICESTALL1b: a read that met the timeout of the client its caller handed in is the caller's deadline, not a stalled service.</summary>
    [Fact]
    public async Task A_read_that_meets_the_callers_own_deadline_stays_a_cancellation_and_is_asked_once()
    {
        var host = new StandIn((_, _, ct) => Never(ct));
        using var doors = Callers(host);

        var read = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doors.GetAsync("/api/quests", CancellationToken.None));
        var reply = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doors.SendAsync(ServiceVerb.Get, "/api/quests/q1/claim", null, CancellationToken.None));

        Assert.IsType<TimeoutException>(read.InnerException);
        Assert.IsType<TimeoutException>(reply.InnerException);
        Assert.Equal(["GET /api/quests", "GET /api/quests/q1/claim"], host.Asked);
    }

    /// <summary>🔴 SERVICESTALL1b: a press against a host that never answers is sent once, and says nothing of the service's stall where the deadline was the caller's.</summary>
    [Fact]
    public async Task A_press_the_caller_s_deadline_cut_is_sent_once()
    {
        var host = new StandIn((_, _, ct) => Never(ct));
        using var doors = Callers(host);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doors.PostAsync("/api/asks", "{}", CancellationToken.None));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doors.SendAsync(ServiceVerb.Delete, "/api/asks/1", null, CancellationToken.None));

        Assert.Equal(["POST /api/asks", "DELETE /api/asks/1"], host.Asked);
    }

    /// <summary>🔴 SERVICESTALL1b: a caller's deadline that falls while the read waits to be asked once more ends it as that cancellation, and the second try is never sent.</summary>
    [Fact]
    public async Task A_caller_deadline_that_falls_between_the_two_tries_stays_a_cancellation()
    {
        var host = new StandIn((_, _, ct) => Never(ct));
        using var doors = new ServiceHttp(Url, key: null, new HttpClient(host, disposeHandler: false) { Timeout = TimeSpan.FromMilliseconds(50) })
        {
            RetryAfter = TimeSpan.FromSeconds(30),
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doors.GetAsync("/api/sessions", deadline.Token));

        Assert.Single(host.Asked);
    }

    /// <summary>A service that could not be reached is not one that stalled: its failure comes back as it was, asked once, so a caller that counts its asks counts one.</summary>
    [Fact]
    public async Task A_service_that_cannot_be_reached_is_not_asked_again()
    {
        var host = new StandIn((_, _, _) => throw new HttpRequestException("refused"));
        using var doors = Doors(host);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => doors.GetAsync("/api/sessions", CancellationToken.None));

        Assert.Equal("refused", error.Message);
        Assert.Single(host.Asked);
    }

    /// <summary>A caller that gave up is not a service that did not answer: its cancellation is its own.</summary>
    [Fact]
    public async Task A_caller_that_gave_up_is_not_said_as_a_stall()
    {
        var host = new StandIn((_, _, ct) => Never(ct));
        using var doors = Doors(host, boundMilliseconds: 30_000);
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doors.GetAsync("/api/sessions", cancelled.Token));

        Assert.Single(host.Asked);
    }
}
