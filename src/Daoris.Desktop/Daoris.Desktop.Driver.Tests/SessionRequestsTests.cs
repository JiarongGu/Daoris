using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSUX1g (D126 §7.1): a stop, a finish or a decline asked from a terminal reaches a session another process on this
/// home runs through a request in <c>&lt;home&gt;/sessions/requests/</c>, which every loop on the home watches. The loop
/// whose own registry runs the session acts on it as its own route does, recording the person's move as the person's, and
/// removes it; any other leaves it; a request nobody took for a minute is dropped by whichever loop looks.
/// </summary>
/// <remarks>
/// The registry is the watch's test seam (what runs here, and how a stop is made), so no process starts: the suite's fast
/// half. The same pass over real processes and a real tick is <see cref="SessionRequestTickTests"/>.
/// </remarks>
public sealed class SessionRequestsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-requests-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionRequestsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private SessionRequests Requests => new(_home);

    [Fact]
    public void A_request_is_written_whole_where_every_loop_looks_and_read_back()
    {
        Requests.Write(new SessionRequest("s1", SessionMove.Decline, Now) { Note = "Not this way; 中文 too.", Parked = true });

        Assert.True(File.Exists(Path.Combine(_home, "sessions", "requests", "s1.json")));
        var read = Assert.Single(Requests.Pending(Now.AddSeconds(5)));
        Assert.Equal(("s1", SessionMove.Decline, "Not this way; 中文 too.", true, RequestDoor.Terminal, Now),
            (read.Session, read.Move, read.Note, read.Parked, read.By, read.At));
        Assert.DoesNotContain(Directory.GetFiles(Requests.Folder), file => !file.EndsWith(".json", StringComparison.Ordinal));
    }

    /// <summary>A session id arrives from a person's words, so one that could name a path is never written.</summary>
    [Theory]
    [InlineData("../s1")]
    [InlineData("laptop/s1")]
    [InlineData("")]
    public void An_id_that_could_name_a_path_is_refused(string id)
    {
        Assert.Throws<DriverException>(() => Requests.Write(new SessionRequest(id, SessionMove.Stop, Now)));
        Assert.False(Directory.Exists(Requests.Folder) && Directory.EnumerateFiles(Requests.Folder).Any());
    }

    [Fact]
    public async Task A_request_for_a_session_this_loop_runs_is_its_stop_and_is_removed()
    {
        Requests.Write(new SessionRequest("s1", SessionMove.Stop, Now));
        var stopped = new List<string>();
        await using var watch = new SessionRequestWatch(_home, runsHere: id => id == "s1", stop: id => { stopped.Add(id); return true; }, service: () => null, clock: () => Now);

        var honoured = await watch.HonourAsync();

        Assert.Equal(["s1"], stopped);
        Assert.Equal("s1", Assert.Single(honoured).Session);
        Assert.Empty(Requests.Pending(Now));
    }

    /// <summary>Another loop's session, or nobody's: the request is left for the loop that runs it, until it is a minute old.</summary>
    [Fact]
    public async Task A_request_for_a_session_another_loop_runs_is_left_and_one_nobody_took_for_a_minute_is_dropped()
    {
        Requests.Write(new SessionRequest("s2", SessionMove.Stop, Now));
        var stopped = new List<string>();
        await using var watch = new SessionRequestWatch(_home, runsHere: id => id == "s1", stop: id => { stopped.Add(id); return true; }, service: () => null, clock: () => Now.AddSeconds(30));

        Assert.Empty(await watch.HonourAsync());
        Assert.Empty(stopped);
        Assert.Single(Requests.Pending(Now.AddSeconds(30)));

        await using var later = new SessionRequestWatch(_home, runsHere: _ => false, stop: _ => true, service: () => null, clock: () => Now.AddMinutes(2));
        await later.HonourAsync();
        Assert.False(File.Exists(Path.Combine(Requests.Folder, "s2.json")));
    }

    /// <summary>
    /// A parked session's stop, and a finish or a decline, are its route's (<c>RESOLVE_SESSION</c>): the process goes first,
    /// then the ledger moves the record with the person's words, or the sentence that says the person did it.
    /// </summary>
    [Theory]
    [InlineData(SessionMove.Stop, true, null, "stopped", "The person stopped this at a checkpoint.")]
    [InlineData(SessionMove.Finish, true, "Merged by hand.", "completed", "Merged by hand.")]
    [InlineData(SessionMove.Finish, true, null, "completed", "The person finished this at a checkpoint.")]
    [InlineData(SessionMove.Decline, true, "Not this way.", "declined", "Not this way.")]
    public async Task A_parked_sessions_move_is_its_resolve_the_process_first_then_the_record(
        string move, bool parked, string? note, string state, string said)
    {
        var ledger = new MovingLedger("s1", "awaiting-person");
        using var service = ledger.Client();
        Requests.Write(new SessionRequest("s1", move, Now) { Note = note, Parked = parked });
        var order = new List<string>();
        ledger.Moved = (id, to) => order.Add($"record {to}");
        await using var watch = new SessionRequestWatch(
            _home, runsHere: _ => true, stop: id => { order.Add($"process {id}"); return true; }, service: () => service, clock: () => Now);

        await watch.HonourAsync();

        Assert.Equal(["process s1", $"record {state}"], order);
        Assert.Equal((state, said), (ledger.State, ledger.Note));
    }

    /// <summary>A loop whose service is not up yet takes no request it would need the ledger for: it is still there at the next look.</summary>
    [Fact]
    public async Task A_resolve_waits_for_the_loops_service()
    {
        Requests.Write(new SessionRequest("s1", SessionMove.Finish, Now) { Parked = true });
        var stopped = new List<string>();
        await using var watch = new SessionRequestWatch(_home, runsHere: _ => true, stop: id => { stopped.Add(id); return true; }, service: () => null, clock: () => Now);

        Assert.Empty(await watch.HonourAsync());
        Assert.Empty(stopped);
        Assert.Single(Requests.Pending(Now));
    }

    /// <summary>Two loops honouring at once: the request is taken by one, and acted on once.</summary>
    [Fact]
    public async Task A_request_is_taken_once()
    {
        Requests.Write(new SessionRequest("s1", SessionMove.Stop, Now));
        var stops = 0;
        await using var one = new SessionRequestWatch(_home, runsHere: _ => true, stop: _ => { Interlocked.Increment(ref stops); return true; }, service: () => null, clock: () => Now);
        await using var two = new SessionRequestWatch(_home, runsHere: _ => true, stop: _ => { Interlocked.Increment(ref stops); return true; }, service: () => null, clock: () => Now);

        await Task.WhenAll(one.HonourAsync(), two.HonourAsync());

        Assert.Equal(1, stops);
    }

    /// <summary>A request withdrawn is gone, and withdrawing one already taken says it was not there.</summary>
    [Fact]
    public void A_request_is_withdrawn_while_it_waits()
    {
        Requests.Write(new SessionRequest("s1", SessionMove.Stop, Now));

        Assert.True(Requests.Withdraw("s1"));
        Assert.False(Requests.Withdraw("s1"));
        Assert.Empty(Requests.Pending(Now));
    }

    /// <summary>The service's state door, standing in for one session: it moves the record and says how.</summary>
    private sealed class MovingLedger(string id, string state) : HttpMessageHandler
    {
        public string State { get; private set; } = state;

        public string? Note { get; private set; }

        public Action<string, string>? Moved { get; set; }

        public ServiceClient Client() => new("http://ledger.test", null, new HttpClient(this, disposeHandler: false));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject();
            State = body["state"]!.GetValue<string>();
            Note = body["note"]?.GetValue<string>();
            Moved?.Invoke(id, State);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    new JsonObject { ["session"] = new JsonObject { ["id"] = id, ["state"] = State }, ["message"] = "moved" }.ToJsonString(),
                    System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
