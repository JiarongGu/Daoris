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
/// half. The same pass over real processes and a real tick is <see cref="SessionRequestTickTests"/>. A terminal's words
/// (MSG1e) are a request of their own, answered beside it; the verb's whole door is <see cref="SessionsSayCommandTests"/>.
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

        Assert.True(File.Exists(DaorisHome.Entry(_home, HomeEntry.Requests, "s1.json")));
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

    /// <summary>
    /// REQTAKE1: two loops taking twenty at once, on threads of their own, honour every one exactly once. A take by renaming lost
    /// one: the other loop's rename held the file open to move it, which refused the first loop's read of the name it had just
    /// moved it to, and the first then deleted it (FIX-LOG 2026-10-16).
    /// </summary>
    [Fact]
    public async Task Two_loops_taking_twenty_at_once_honour_each_exactly_once()
    {
        var sessions = Enumerable.Range(1, 20).Select(i => $"s{i}").ToList();
        foreach (var session in sessions) Requests.Write(new SessionRequest(session, SessionMove.Stop, Now));
        var stops = new System.Collections.Concurrent.ConcurrentBag<string>();
        await using var one = new SessionRequestWatch(_home, runsHere: _ => true, stop: id => { stops.Add(id); return true; }, service: () => null, clock: () => Now);
        await using var two = new SessionRequestWatch(_home, runsHere: _ => true, stop: id => { stops.Add(id); return true; }, service: () => null, clock: () => Now);

        await Task.WhenAll(Task.Run(() => one.HonourAsync()), Task.Run(() => two.HonourAsync()));
        // A request the other loop's look held at that moment waits for the next look, and is honoured then, once.
        await one.HonourAsync();

        Assert.Equal(sessions.Order(), stops.Order());
        Assert.Empty(Requests.Pending(Now));
    }

    /// <summary>
    /// REQTAKE1, the loss itself: held as another loop's rename holds it the moment before it moves it (open, letting it be moved,
    /// refusing a reader), a take is taken either then or at the next look, never neither. On Windows the take's own rename
    /// succeeds and its read is refused, and the take that then removed what it moved lost the request unread.
    /// </summary>
    [Fact]
    public void A_take_whose_read_is_refused_leaves_the_request_for_the_next_look()
    {
        Requests.Write(new SessionRequest("s1", SessionMove.Stop, Now));

        SessionRequest? whileHeld;
        using (new FileStream(Requests.PathOf("s1"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            whileHeld = Requests.Take("s1");
        }

        var afterwards = Requests.Take("s1");

        Assert.Equal(SessionMove.Stop, Assert.Single(new[] { whileHeld, afterwards }.OfType<SessionRequest>()).Move);
        Assert.Empty(Directory.GetFiles(Requests.Folder));
    }

    /// <summary>
    /// REQTAKE2 (D126 §7.1): of a withdrawal and a take only one wins, and the withdrawal says which. On Windows a take's rename
    /// opens the file, letting it be moved, a moment before it moves it; a withdrawal's rename landing in that moment moves it
    /// aside, and the take's rename then moves it on from the withdrawn name. The withdrawal that removed what it had moved found
    /// nothing there, said withdrawn, and the loop acted on it. Held here as such a rename holds it, the rename then completes
    /// from wherever the file is: on Windows, through its handle, so the take has it and the withdrawal says it was not
    /// withdrawn; on Linux a rename is by name and whole, so the withdrawal had it and the take finds nothing.
    /// </summary>
    [Fact]
    public async Task A_withdrawal_and_a_take_begun_before_it_never_both_win()
    {
        Requests.Write(new SessionRequest("s1", SessionMove.Stop, Now));
        var path = Requests.PathOf("s1");
        var taking = path + ".a1b2c3d4.taking";

        Task<bool> withdrawing;
        var taken = false;
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            withdrawing = Task.Run(() => Requests.Withdraw("s1"));
            await Poll.Until(() => !File.Exists(path), () => "the withdrawal never moved the request aside", Poll.Patience);

            // The take's rename lands: on Windows its handle moves the file from wherever it now is, on Linux its name is gone.
            var from = OperatingSystem.IsWindows() ? Directory.GetFiles(Requests.Folder).SingleOrDefault() : path;
            try
            {
                if (from is not null)
                {
                    File.Move(from, taking);
                    taken = true;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Gone, or removed while it was held: nothing for the take.
            }
        }

        var withdrawn = await withdrawing.WaitAsync(Poll.Patience);

        Assert.False(withdrawn && taken, "the door said withdrawn while the loop took it");
        Assert.True(withdrawn || taken, "neither the withdrawal nor the take has it");
        if (OperatingSystem.IsWindows()) Assert.True(taken, "a rename begun before the withdrawal's lands after it, so the take has it");
        string[] left = taken ? [Path.GetFileName(taking)] : [];
        Assert.Equal(left, Directory.GetFiles(Requests.Folder).Select(Path.GetFileName));
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

    private static SessionRequest Say(string session, string key = "a1b2c3d4e5f6") =>
        new(session, SessionMove.Say, Now) { Text = "also the changelog; 中文 too.", Files = ["notes.md"], Key = key };

    /// <summary>
    /// MSG1e (D137 §5.2): a terminal's words are a request of their own, under the session's id and their own key, so two said
    /// at once both wait and a stop for the same session is not replaced by them; read back whole.
    /// </summary>
    [Fact]
    public void A_say_is_written_under_its_own_name_beside_a_stop_and_read_back_whole()
    {
        Requests.Write(new SessionRequest("s1", SessionMove.Stop, Now));
        Requests.Write(Say("s1"));
        Requests.Write(Say("s1", key: "0f0f0f0f0f0f"));

        Assert.True(File.Exists(Path.Combine(Requests.Folder, "s1.a1b2c3d4e5f6.json")));
        var pending = Requests.Pending(Now.AddSeconds(5));
        Assert.Equal(3, pending.Count);
        var said = Assert.Single(pending, request => request.Key == "a1b2c3d4e5f6");
        Assert.Equal(("s1", SessionMove.Say, "also the changelog; 中文 too.", RequestDoor.Terminal, Now),
            (said.Session, said.Move, said.Text, said.By, said.At));
        Assert.Equal(["notes.md"], said.Files);
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("A1B2")]
    [InlineData("")]
    public void A_say_whose_key_could_name_a_path_is_refused(string key)
    {
        Assert.Throws<DriverException>(() => Requests.Write(Say("s1", key)));
    }

    /// <summary>A say is taken by its own name, once; its answer is kept beside it for the asker, and read once.</summary>
    [Fact]
    public void A_say_is_taken_by_its_own_name_and_its_answer_read_once()
    {
        var request = Say("s1");
        Requests.Write(request);

        Assert.NotNull(Requests.Take(request));
        Assert.Null(Requests.Take(request));
        Assert.Null(Requests.AnswerOf(request));

        Requests.Answer(request, new WordsHeld(true, "resume", null) { Word = "w1" });

        Assert.Equal(new WordsHeld(true, "resume", null) { Word = "w1" }, Requests.AnswerOf(request));
        Assert.Null(Requests.AnswerOf(request));
        Assert.Empty(Requests.Pending(Now));
    }

    /// <summary>
    /// An answer something else holds open that moment (a reader that lets it be deleted and nothing else) is read at the next
    /// ask, never dropped unread: the asker would otherwise wait out its time for words that were held.
    /// </summary>
    [Fact]
    public void An_answer_held_open_is_read_at_the_next_ask_never_dropped()
    {
        var request = Say("s1");
        Requests.Answer(request, new WordsHeld(true, "turn-end", null));
        var path = Path.Combine(Requests.Folder, "s1.a1b2c3d4e5f6.answer.json");

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Delete))
        {
            Assert.Null(Requests.AnswerOf(request));
        }

        Assert.Equal(new WordsHeld(true, "turn-end", null), Requests.AnswerOf(request));
    }

    /// <summary>A say's answer is its asker's: a look never lists it as a request, and drops one nobody read for a minute.</summary>
    [Fact]
    public void An_answer_is_never_a_request_and_one_nobody_read_is_dropped()
    {
        Requests.Answer(Say("s1"), new WordsHeld(true, "turn-end", null));
        var path = Path.Combine(Requests.Folder, "s1.a1b2c3d4e5f6.answer.json");

        Assert.Empty(Requests.Pending(DateTimeOffset.UtcNow));
        Assert.True(File.Exists(path));

        Assert.Empty(Requests.Pending(DateTimeOffset.UtcNow.AddMinutes(2)));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void A_say_nobody_took_is_withdrawn_by_its_own_name()
    {
        var request = Say("s1");
        Requests.Write(request);
        Requests.Write(new SessionRequest("s1", SessionMove.Stop, Now));

        Assert.True(Requests.Withdraw(request));
        Assert.False(Requests.Withdraw(request));
        Assert.Equal(SessionMove.Stop, Assert.Single(Requests.Pending(Now)).Move);
    }

    /// <summary>A loop's half (MSG1e): a session its registry runs hears the words at its door, as the screen's box hands them.</summary>
    [Fact]
    public async Task A_say_for_a_session_this_loop_runs_is_held_at_its_door_and_answered()
    {
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.SessionPids));
        var inbox = processes.OpenInbox("s1");
        inbox.Attach(interrupt: null);
        var request = Say("s1");
        Requests.Write(request);
        await using var watch = new SessionRequestWatch(_home, processes, () => null, every: TimeSpan.FromHours(1), clock: () => Now);

        var honoured = await watch.HonourAsync();

        Assert.Equal("s1", Assert.Single(honoured).Session);
        Assert.Equal("also the changelog; 中文 too.", Assert.Single(inbox.State.Queued).Text);
        Assert.Equal(new WordsHeld(true, "turn-end", null), Requests.AnswerOf(request));
    }

    /// <summary>A session another process on this machine runs is that process's loop's to answer: the request is left.</summary>
    [Fact]
    public async Task A_say_for_a_session_another_process_runs_is_left()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        using (var self = System.Diagnostics.Process.GetCurrentProcess())
        {
            var pid = DaorisHome.Entry(_home, HomeEntry.SessionPids, "s1");
            Directory.CreateDirectory(Path.GetDirectoryName(pid)!);
            File.WriteAllText(pid, $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
        }

        var ledger = new SayingLedger();
        using var service = ledger.Client();
        Requests.Write(Say("s1"));
        await using var watch = new SessionRequestWatch(_home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.SessionPids)), () => service, every: TimeSpan.FromHours(1), clock: () => Now);

        Assert.Empty(await watch.HonourAsync());
        Assert.Single(Requests.Pending(Now));
        Assert.Equal(0, ledger.Says);
    }

    /// <summary>
    /// A session nothing on this machine runs is any loop's to keep the words for, once its service answers: kept on the
    /// record by the say door, and the word's id answered.
    /// </summary>
    [Fact]
    public async Task A_say_for_a_session_nothing_here_runs_waits_for_the_loops_service_then_is_kept()
    {
        var ledger = new SayingLedger();
        using var service = ledger.Client();
        ServiceClient? up = null;
        var request = Say("s1");
        Requests.Write(request);
        await using var watch = new SessionRequestWatch(_home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.SessionPids)), () => up, every: TimeSpan.FromHours(1), clock: () => Now);

        Assert.Empty(await watch.HonourAsync());
        Assert.Single(Requests.Pending(Now));

        up = service;
        Assert.Single(await watch.HonourAsync());
        Assert.Equal(new WordsHeld(true, "resume", null) { Word = "w1" }, Requests.AnswerOf(request));
        Assert.Equal(("also the changelog; 中文 too.", "notes.md"), (ledger.Text, ledger.File));
    }

    /// <summary>The service's say door, standing in: it keeps every word as <c>w1</c>, and remembers what it was handed.</summary>
    private sealed class SayingLedger : HttpMessageHandler
    {
        public int Says { get; private set; }

        public string? Text { get; private set; }

        public string? File { get; private set; }

        public ServiceClient Client() => new("http://ledger.test", null, new HttpClient(this, disposeHandler: false));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject();
            Says++;
            Text = body["text"]!.GetValue<string>();
            File = body["files"]?.AsArray().Select(name => (string?)name).FirstOrDefault();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    new JsonObject
                    {
                        ["session"] = new JsonObject { ["id"] = "s1", ["state"] = "completed" }, ["message"] = "Kept.",
                        ["said"] = new JsonObject { ["id"] = "w1", ["text"] = Text, ["at"] = Now.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true },
                    }.ToJsonString(),
                    System.Text.Encoding.UTF8, "application/json"),
            };
        }
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
