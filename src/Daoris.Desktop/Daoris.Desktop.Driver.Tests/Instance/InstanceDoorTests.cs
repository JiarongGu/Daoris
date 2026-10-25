using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A verb's door to the running Daoris (RPC1b, the terminal RPC design §7.2 and §7.4), row by row of the switch, against a stand-in
/// of the pipe: alone where no instance runs, sent where one answers, refused where one lives and does not answer or refuses the
/// hello, and never alone once a request was sent and its answer lost.
/// </summary>
/// <remarks>Windows only, as the pipe is: each test serves its own stand-in in process, on its own home's name.</remarks>
public sealed class InstanceDoorTests : IAsyncLifetime
{
    /// <summary>A press the census opens, as a verb's would be: the trees verbs' are all the person's (D171's RPC1a note).</summary>
    private static readonly InstanceRoute Open = new("TEST.PRESS", "MOVE", RouteClass.Open);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-instance-door-tests", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private string _home = "";
    private string _app = "";
    private string _id = "";
    private string _pipe = "";
    private int _alone;
    private InstanceStandIn? _standIn;

    public Task InitializeAsync()
    {
        // An install, as FINDDAORIS1 knows one: its marker beside `app/`, the driver host under it, its home in `data/`.
        var install = Path.Combine(_root, "install");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "");
        _app = Path.Combine(install, DaorisHome.InstallApp, "daoris-driver");
        Directory.CreateDirectory(_app);
        _home = Path.Combine(install, DaorisHome.InstallData);
        Directory.CreateDirectory(_home);
        _id = HomeIdentity.Ensure(_home)!;
        _pipe = PipeName.For(_id, Random.Shared.Next(100_000, 999_999));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_standIn is not null) await _standIn.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private InstanceStandIn Serve(bool refusing = false)
    {
        _standIn = new InstanceStandIn(_pipe, _id, refusing);
        return _standIn;
    }

    private InstanceWorld World(Func<string, string?>? environment = null, string? baseDirectory = null, string? statusHome = null,
                                TimeSpan? wait = null, TimeSpan? answer = null, IReadOnlySet<string>? owners = null) => new()
    {
        Home = _home,
        Owners = owners,
        // 🔴 Never the account's: a test reads no variable it was not handed (D171's FINDDAORIS1 note).
        Environment = environment ?? (_ => null),
        BaseDirectory = baseDirectory ?? _app,
        Status = _ => new ServiceStatus("lexical", statusHome ?? _id),
        Out = _out,
        Error = _error,
        Verb = "trees",
        // TESTWAIT7: a stand-in that answers is waited for for minutes, as a loaded machine needs; the bound only elapses where a
        // case serves nothing (`answer:` short there, as no load can make a pipe nobody serves answer).
        Answer = answer ?? TimeSpan.FromMinutes(2),
        Wait = wait ?? TimeSpan.FromMinutes(2),
    };

    private Task<int> Alone()
    {
        Interlocked.Increment(ref _alone);
        _out.WriteLine("trees: done alone.");
        return Task.FromResult(0);
    }

    /// <summary>The answer in the verb's words: its message, exit 0 where done and 1 where refused as data (§6).</summary>
    private int Words(JsonElement answer)
    {
        _out.WriteLine($"trees: {answer.GetProperty("message").GetString()}");
        return answer.GetProperty("done").GetBoolean() ? 0 : 1;
    }

    private Task<int> PressAsync(InstanceWorld world, InstanceRoute? route = null) =>
        InstanceDoor.PressAsync(route ?? Open, new { id = "s1a2b3c4", force = true }, Alone, Words, world);

    [Fact]
    public async Task No_running_file_acts_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();

        var exit = await PressAsync(World());

        Assert.Equal(0, exit);
        Assert.Equal(1, _alone);
        Assert.Equal(0, standIn.Connections);
    }

    [Fact]
    public async Task A_workspace_build_reads_no_address_file()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);
        var workspace = Path.Combine(_root, "workspace", "bin");
        Directory.CreateDirectory(workspace);

        var exit = await PressAsync(World(baseDirectory: workspace));

        Assert.Equal(0, exit);
        Assert.Equal(1, _alone);
        Assert.Equal(0, standIn.Connections);
    }

    [Fact]
    public async Task A_status_answering_another_home_acts_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var another = Path.Combine(_root, "another");
        Directory.CreateDirectory(another);
        var exit = await PressAsync(World(statusHome: HomeIdentity.Ensure(another)));

        Assert.Equal(0, exit);
        Assert.Equal(1, _alone);
        Assert.Equal(0, standIn.Connections);
    }

    [Fact]
    public async Task A_headless_loop_holding_the_home_acts_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        Assert.NotNull(lease);

        var exit = await PressAsync(World());

        Assert.Equal(0, exit);
        Assert.Equal(1, _alone);
        Assert.Equal(0, standIn.Connections);
    }

    /// <summary>
    /// RPC1c: a person's press goes to the instance that answers, which shows it in the window; the terminal says it waits there,
    /// then says the answer the confirmed press gave, and nothing is done alone.
    /// </summary>
    [Fact]
    public async Task A_person_route_is_sent_and_the_terminal_says_it_waits_for_the_window_then_says_its_answer()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Confirming = true;
        standIn.Data = new { done = true, message = "archived s1a2b3c4." };
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World(), Open with { Class = RouteClass.Person });

        Assert.Equal(0, exit);
        Assert.Equal(0, _alone);
        var id = Assert.Single(standIn.Presses).GetProperty("id").GetString();
        Assert.Equal($"trees: archived s1a2b3c4.{Environment.NewLine}", _out.ToString());
        // CONFIRMWAIT1b (the confirm-wait design §4.1): the id, and the two verbs that read and take it back, said at once.
        Assert.Equal(
            $"trees: waiting for you to confirm this in Daoris's window (request {id}); `daoris-driver result {id}` says how it ended, "
            + $"and `daoris-driver withdraw {id}` takes it back…{Environment.NewLine}",
            _error.ToString());
    }

    /// <summary>
    /// CONFIRMWAIT1b (design §4.1): with <c>--no-wait</c> (a wait of zero) the verb returns once the instance says <c>CONFIRMING</c>,
    /// having said the id and its two verbs, and asks <c>RESULT</c>: a press <c>waiting</c> is exit 1 and kept past the day.
    /// </summary>
    [Fact]
    public async Task No_wait_returns_once_the_instance_says_it_waits_naming_its_id_and_its_two_verbs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Hold;
        standIn.Result = "waiting";
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World(wait: TimeSpan.Zero), Open with { Class = RouteClass.Person });

        Assert.Equal(1, exit);
        Assert.Equal(0, _alone);
        var id = Assert.Single(standIn.Presses).GetProperty("id").GetString();
        Assert.Single(standIn.Results);
        var lines = _error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains($"(request {id}); `daoris-driver result {id}` says how it ended, and `daoris-driver withdraw {id}` takes it back…", lines[0]);
        Assert.Equal(InstanceDoor.StillWaiting("trees", id!), lines[1]);
        var kept = new SentPresses(_home).Find(id!, DateTimeOffset.UtcNow.AddDays(2));
        Assert.NotNull(kept);
        Assert.NotNull(kept.WaitingAt);
    }

    /// <summary>
    /// CONFIRMWAIT1b (design §4.1): the verb's own wait over with the press still waiting is not lost and not an error: <c>RESULT</c>'s
    /// <c>waiting</c> is said with the id and its two verbs, exit 1; today's instance, which answers <c>running</c>, is said as today, exit 2.
    /// </summary>
    [Theory]
    [InlineData("waiting", 1)]
    [InlineData("running", 2)]
    public async Task A_press_at_its_waits_end_is_said_by_results_state(string state, int code)
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Hold;
        standIn.Result = state;
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World(wait: TimeSpan.FromMilliseconds(500)), Open with { Class = RouteClass.Person });

        Assert.Equal(code, exit);
        var id = Assert.Single(standIn.Presses).GetProperty("id").GetString()!;
        if (state == "waiting") Assert.Contains(InstanceDoor.StillWaiting("trees", id), _error.ToString());
        else Assert.Contains($"Daoris was still working on it (request {id})", _error.ToString());
        // CONFIRMWAIT1 (§15.2 item 6): nothing found it ended, so its key is kept past the day either way.
        Assert.NotNull(new SentPresses(_home).Find(id, DateTimeOffset.UtcNow.AddDays(2)));
    }

    /// <summary>
    /// CONFIRMWAIT1 (the confirm-wait design §15.2 item 6): the verb marks its record waiting as soon as the instance says the id, before
    /// any <c>RESULT</c>, so a verb whose harness is killed after the id, or whose <c>RESULT</c> does not read, leaves the key kept.
    /// </summary>
    [Fact]
    public async Task The_record_is_marked_waiting_as_the_id_is_said()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Hold;
        standIn.Result = "id-taken";
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        await PressAsync(World(wait: TimeSpan.Zero), Open with { Class = RouteClass.Person });

        var id = Assert.Single(standIn.Presses).GetProperty("id").GetString()!;
        Assert.NotNull(new SentPresses(_home).Find(id, DateTimeOffset.UtcNow.AddDays(2))?.WaitingAt);
    }

    /// <summary>
    /// CONFIRMWAIT1a-ii (design §4.1, §7.2): the close answers a line waiting on a press <c>waiting</c>, <c>why: close</c>, the press kept
    /// in its record across the restart: said as the verb's own wait's end, with the id and its two verbs, exit 1, and kept past the day.
    /// </summary>
    [Fact]
    public async Task A_close_answering_waiting_is_the_verbs_own_waits_end()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = "waiting";
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World(), Open with { Class = RouteClass.Person });

        Assert.Equal(1, exit);
        Assert.Equal(0, _alone);
        var id = Assert.Single(standIn.Presses).GetProperty("id").GetString()!;
        Assert.Contains(InstanceDoor.StillWaiting("trees", id), _error.ToString());
        Assert.NotNull(new SentPresses(_home).Find(id, DateTimeOffset.UtcNow.AddDays(2))?.WaitingAt);
    }

    /// <summary>CONFIRMWAIT1b (design §4.1, §5.1): <c>already</c> is exit 0, its sentence on the error stream, nothing done alone.</summary>
    [Fact]
    public async Task Already_set_is_exit_0_with_its_sentence()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Press = StandInPress.Refuse;
        standIn.Code = "already";
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World(), Open with { Class = RouteClass.Person });

        Assert.Equal(0, exit);
        Assert.Equal(0, _alone);
        Assert.Contains("trees: refused already. (already)", _error.ToString());
    }

    /// <summary>
    /// RPC1c: the person's refusal, and two minutes with no answer, are the verb's exit 1 with the instance's sentence; nothing is done
    /// alone. CONFIRMWAIT1b (design §2): a ground that changed, a withdrawal and a loss are the person's or the asker's word too.
    /// </summary>
    [Theory]
    [InlineData("refused")]
    [InlineData("expired")]
    [InlineData("changed")]
    [InlineData("withdrawn")]
    [InlineData("lost")]
    public async Task A_person_route_the_window_refuses_or_lets_expire_is_exit_1_and_never_done_alone(string code)
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = code;
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World(), Open with { Class = RouteClass.Person });

        Assert.Equal(1, exit);
        Assert.Equal(0, _alone);
        Assert.Contains($"trees: refused {code}. ({code})", _error.ToString());
        Assert.Equal("", _out.ToString());
    }

    /// <summary>A person's press with no instance running acts alone, as it always did (§7.2); a window's route is never sent.</summary>
    [Fact]
    public async Task A_person_route_with_no_instance_acts_alone_and_a_window_route_is_never_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();

        Assert.Equal(0, await PressAsync(World(), Open with { Class = RouteClass.Person }));
        using (RunningAddress.Write(_home, "http://localhost:5177", _pipe))
        {
            Assert.Equal(0, await PressAsync(World(), Open with { Class = RouteClass.Window }));
        }

        Assert.Equal(2, _alone);
        Assert.Equal(0, standIn.Connections);
    }

    [Fact]
    public async Task An_instance_that_answers_is_sent_the_press_once_and_its_answer_is_said_in_the_verbs_words()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Data = new { done = true, message = "landed on `main`." };
        standIn.Progress = "half way";
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World());

        Assert.Equal(0, exit);
        Assert.Equal(0, _alone);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("TEST.PRESS", press.GetProperty("module").GetString());
        Assert.Equal("MOVE", press.GetProperty("type").GetString());
        Assert.Equal("""{"id":"s1a2b3c4","force":true}""", press.GetProperty("payload").GetRawText());
        Assert.True(Guid.TryParse(press.GetProperty("id").GetString(), out _));
        Assert.InRange(press.GetProperty("key").GetString()!.Length, 16, 128);
        // The hello first, naming this home's id and the wire's version.
        var hello = standIn.Sent[0];
        Assert.Equal("HELLO", hello.GetProperty("type").GetString());
        Assert.Equal(_id, hello.GetProperty("payload").GetProperty("home").GetString());
        Assert.Equal(1, hello.GetProperty("payload").GetProperty("protocol").GetInt32());
        Assert.Equal($"  half way{Environment.NewLine}trees: landed on `main`.{Environment.NewLine}", _out.ToString());
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task An_answer_refused_as_data_is_the_verbs_exit_1()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Data = new { done = false, message = "kept: its tree has uncommitted work." };
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World());

        Assert.Equal(1, exit);
        Assert.Equal(0, _alone);
        Assert.Contains("trees: kept: its tree has uncommitted work.", _out.ToString());
    }

    [Fact]
    public async Task A_press_the_instance_refuses_is_exit_2_with_its_sentence_and_never_done_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Press = StandInPress.Refuse;
        standIn.Code = "confirm-in-window";
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World());

        Assert.Equal(2, exit);
        Assert.Equal(0, _alone);
        Assert.Contains("refused confirm-in-window.", _error.ToString());
        Assert.Contains("confirm-in-window", _error.ToString());
    }

    [Fact]
    public async Task An_instance_whose_pipe_does_not_answer_refuses_the_press_and_does_nothing_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        // The file names this home's pipe, and nothing serves it.
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World(answer: TimeSpan.FromSeconds(1)));

        Assert.Equal(2, exit);
        Assert.Equal(0, _alone);
        Assert.StartsWith(
            $"trees: Daoris is running on this home (`Daoris.exe`, pid {Environment.ProcessId}) and did not answer; nothing was done.",
            _error.ToString());
    }

    [Fact]
    public async Task An_instance_naming_no_pipe_refuses_the_press_and_does_nothing_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        using var running = RunningAddress.Write(_home, "http://localhost:5177");

        var exit = await PressAsync(World());

        Assert.Equal(2, exit);
        Assert.Equal(0, _alone);
        Assert.Equal(0, standIn.Connections);
        Assert.StartsWith(
            $"trees: Daoris is running on this home (`Daoris.exe`, pid {Environment.ProcessId}) and did not answer; nothing was done.",
            _error.ToString());
        Assert.Contains("names no pipe", _error.ToString());
    }

    /// <summary>
    /// A pipe this terminal does not take as its user's (PIPEOWNER1) refuses the press saying so, at once and never as a pipe that
    /// did not answer, exit 2, nothing sent and nothing done alone: one another account serves, whose ACL refuses the connect, or
    /// one whose owner the terminal does not take, as a terminal that is not elevated did an elevated Daoris's before the pipe was
    /// its user's. The owners taken are the world's here, so no second elevation is needed.
    /// </summary>
    [Theory]
    [InlineData("acl")]
    [InlineData("owner")]
    [InlineData("variable")]
    public async Task A_pipe_another_account_or_elevation_holds_refuses_the_press_saying_so_and_nothing_is_done_alone(string how)
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve(refusing: how != "owner");
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);
        // The Guests group's: an owner no pipe served here has.
        var owners = how == "owner" ? new HashSet<string> { "S-1-5-32-546" } : null;
        var named = how == "variable" ? _pipe : null;

        var exit = await PressAsync(World(environment: name => name == InstanceDoor.Variable ? named : null, owners: owners));

        Assert.Equal(2, exit);
        Assert.Equal(0, _alone);
        Assert.Empty(standIn.Sent);
        var who = how == "variable"
            ? $"the Daoris {InstanceDoor.Variable} names ({_pipe})"
            : $"Daoris is running on this home (`Daoris.exe`, pid {Environment.ProcessId}) and";
        Assert.Equal(
            $"trees: {who} refused this terminal; nothing was done. Its pipe belongs to another Windows account or elevation.",
            _error.ToString().TrimEnd());
    }

    [Theory]
    [InlineData("protocol", "close", "another version of the terminal's pipe")]
    [InlineData("home", "close", "another home")]
    [InlineData("stopping", "close", "Daoris is closing; run this again once it is back.")]
    [InlineData("stopping", "update", "Daoris is updating; run this again once it is back.")]
    public async Task A_hello_refused_refuses_the_press_saying_which(string code, string why, string said)
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.RefuseHello = code;
        standIn.StoppingWhy = why;
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World());

        Assert.Equal(2, exit);
        Assert.Equal(0, _alone);
        Assert.Empty(standIn.Presses);
        Assert.Contains(said, _error.ToString());
    }

    [Fact]
    public async Task A_lost_answer_is_asked_for_by_its_key_and_said_never_sent_again_nor_done_alone()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Press = StandInPress.Drop;
        standIn.Data = new { done = true, message = "landed on `main`." };
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World());

        Assert.Equal(0, exit);
        Assert.Equal(0, _alone);
        var press = Assert.Single(standIn.Presses);
        var result = Assert.Single(standIn.Results);
        Assert.Equal(press.GetProperty("id").GetString(), result.GetProperty("payload").GetProperty("id").GetString());
        Assert.Equal(press.GetProperty("key").GetString(), result.GetProperty("payload").GetProperty("key").GetString());
        Assert.Contains("trees: landed on `main`.", _out.ToString());
    }

    [Theory]
    [InlineData(StandInPress.Drop, "running", "Daoris was still working on it (request ")]
    [InlineData(StandInPress.Drop, "unknown", "Daoris does not know how it ended (request ")]
    [InlineData(StandInPress.Hold, "running", "Daoris was still working on it (request ")]
    public async Task A_lost_answer_not_yet_known_is_said_with_its_request_and_never_done_alone(StandInPress press, string state, string said)
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Press = press;
        standIn.Result = state;
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await PressAsync(World(wait: press == StandInPress.Hold ? TimeSpan.FromMilliseconds(500) : null));

        Assert.Equal(2, exit);
        Assert.Equal(0, _alone);
        var sent = Assert.Single(standIn.Presses);
        Assert.Single(standIn.Results);
        Assert.Contains(said + sent.GetProperty("id").GetString() + ")", _error.ToString());
    }

    [Fact]
    public async Task The_variable_names_the_instance_a_workspace_build_reaches()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        var workspace = Path.Combine(_root, "workspace", "bin");
        Directory.CreateDirectory(workspace);

        var exit = await PressAsync(World(environment: name => name == InstanceDoor.Variable ? _pipe : null, baseDirectory: workspace));

        Assert.Equal(0, exit);
        Assert.Equal(0, _alone);
        Assert.Single(standIn.Presses);
    }

    [Fact]
    public async Task The_variable_naming_an_instance_that_does_not_answer_refuses_the_press()
    {
        if (!OperatingSystem.IsWindows()) return;

        var exit = await PressAsync(World(environment: name => name == InstanceDoor.Variable ? _pipe : null, answer: TimeSpan.FromSeconds(1)));

        Assert.Equal(2, exit);
        Assert.Equal(0, _alone);
        Assert.Contains(InstanceDoor.Variable, _error.ToString());
    }

    private Task<int> ReadAsync(InstanceWorld world, InstanceRoute? route = null) =>
        InstanceDoor.ReadAsync(route ?? Open, new { id = "s1a2b3c4" }, Alone, answer => Words(answer), world);

    [Fact]
    public async Task A_read_is_sent_to_an_instance_that_answers_and_said_in_the_verbs_words()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Data = new { done = true, message = "the plan." };
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await ReadAsync(World());

        Assert.Equal(0, exit);
        Assert.Equal(0, _alone);
        Assert.Single(standIn.Presses);
        Assert.Contains("trees: the plan.", _out.ToString());
        // A read keeps no record to ask again: nothing it did needs finding out.
        Assert.False(Directory.Exists(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Pipe), "sent")));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("silent")]
    [InlineData("refused")]
    [InlineData("answer refused")]
    [InlineData("answer lost")]
    [InlineData("person")]
    public async Task A_read_acts_alone_wherever_the_instance_does_not_answer_it_and_says_nothing_of_it(string case_)
    {
        if (!OperatingSystem.IsWindows()) return;
        var route = Open;
        switch (case_)
        {
            case "silent":
                using (RunningAddress.Write(_home, "http://localhost:5177", _pipe))
                {
                    Assert.Equal(0, await ReadAsync(World(answer: TimeSpan.FromSeconds(1))));
                }

                break;
            case "refused":
                Serve().RefuseHello = "stopping";
                using (RunningAddress.Write(_home, "http://localhost:5177", _pipe))
                {
                    Assert.Equal(0, await ReadAsync(World()));
                }

                break;
            case "answer refused":
                var refusing = Serve();
                refusing.Press = StandInPress.Refuse;
                using (RunningAddress.Write(_home, "http://localhost:5177", _pipe))
                {
                    Assert.Equal(0, await ReadAsync(World()));
                }

                break;
            case "answer lost":
                Serve().Press = StandInPress.Drop;
                using (RunningAddress.Write(_home, "http://localhost:5177", _pipe))
                {
                    Assert.Equal(0, await ReadAsync(World()));
                }

                break;
            case "person":
                route = Open with { Class = RouteClass.Person };
                var standIn = Serve();
                using (RunningAddress.Write(_home, "http://localhost:5177", _pipe))
                {
                    Assert.Equal(0, await ReadAsync(World(), route));
                    Assert.Equal(0, standIn.Connections);
                }

                break;
            default:
                Assert.Equal(0, await ReadAsync(World()));
                break;
        }

        Assert.Equal(1, _alone);
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task A_press_keeps_its_id_and_key_under_the_home_for_a_day_and_the_record_is_read_back()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        await PressAsync(World());

        var press = Assert.Single(standIn.Presses);
        var id = press.GetProperty("id").GetString()!;
        var kept = new SentPresses(_home).Find(id, DateTimeOffset.UtcNow);
        Assert.NotNull(kept);
        Assert.Equal(press.GetProperty("key").GetString(), kept.Key);
        Assert.Equal("TEST.PRESS.MOVE", kept.Route);
        // A day on, it is gone, and so is its file.
        Assert.Null(new SentPresses(_home).Find(id, DateTimeOffset.UtcNow.AddHours(25)));
        Assert.Null(new SentPresses(_home).Find("not-an-id", DateTimeOffset.UtcNow));
        Assert.Null(new SentPresses(_home).Find("../escape", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Result_asks_again_for_a_press_this_terminal_sent_and_says_its_answer_in_the_routes_words()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Press = StandInPress.Drop;
        standIn.Result = "running";
        standIn.Data = new { said = new { @out = new[] { "trees: removed 1 of 1." }, error = Array.Empty<string>(), code = 0 } };
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);
        Assert.Equal(2, await PressAsync(World()));
        var id = Assert.Single(standIn.Presses).GetProperty("id").GetString()!;
        _out.GetStringBuilder().Clear();
        _error.GetStringBuilder().Clear();

        // Still under way: said so, with the id, and nothing is sent again.
        Assert.Equal(2, await InstanceDoor.ResultAsync(id, World()));
        Assert.Contains($"Daoris is still working on it (request {id})", _error.ToString());

        standIn.Result = "answered";
        _error.GetStringBuilder().Clear();
        Assert.Equal(0, await InstanceDoor.ResultAsync(id, World()));
        Assert.Contains("trees: removed 1 of 1.", _out.ToString());
        Assert.Single(standIn.Presses);
        Assert.Equal(0, _alone);
    }

    /// <summary>
    /// CONFIRMWAIT1b (design §4.2): <c>result</c> says a press still <c>waiting</c> as the verb's own end does, exit 1, and keeps it past
    /// the day; its ending's refusals are exit 1 where they are the person's or the asker's word, aligned with the press, else 2; and
    /// <c>unknown</c> says what is so.
    /// </summary>
    [Theory]
    [InlineData("waiting", null, 1)]
    [InlineData("answered", "refused", 1)]
    [InlineData("answered", "changed", 1)]
    [InlineData("answered", "withdrawn", 1)]
    [InlineData("answered", "lost", 1)]
    [InlineData("answered", "expired", 1)]
    [InlineData("answered", "already", 0)]
    [InlineData("answered", "stopping", 2)]
    [InlineData("unknown", null, 2)]
    public async Task Result_says_a_waiting_press_and_each_ending_with_the_press_exit_codes(string state, string? refused, int code)
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Press = StandInPress.Drop;
        standIn.Result = "running";
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);
        await PressAsync(World());
        var id = Assert.Single(standIn.Presses).GetProperty("id").GetString()!;
        _error.GetStringBuilder().Clear();
        standIn.Result = state;
        standIn.ResultRefused = refused;

        var exit = await InstanceDoor.ResultAsync(id, World());

        Assert.Equal(code, exit);
        var said = _error.ToString();
        switch (state)
        {
            case "waiting":
                Assert.Contains(InstanceDoor.StillWaiting("trees", id), said);
                Assert.NotNull(new SentPresses(_home).Find(id, DateTimeOffset.UtcNow.AddDays(2)));
                break;
            case "unknown":
                Assert.Contains($"Daoris has no ending for request {id}: it keeps an ending a day, and a press it never began is not in it.", said);
                Assert.DoesNotContain("it stopped while this was under way", said);
                break;
            default:
                Assert.Contains($"refused {refused}. ({refused})", said);
                break;
        }
    }

    /// <summary>
    /// CONFIRMWAIT1b (design §4.3): <c>withdraw &lt;id&gt;</c> sends the pipe's own <c>WITHDRAW { id, key }</c> with the kept key on a
    /// new connection, never the kit's <c>CANCEL</c>, and says each answer: taken back exit 0; confirmed meanwhile, nothing cancelled, exit
    /// 1; already ended, with its ending, exit 1; another key's exit 2; and today's instance, which has no such route, exit 2, saying so.
    /// </summary>
    [Theory]
    [InlineData("withdrawn", 0, "took back request {id}: it was not done, and its card is gone from Daoris's window.")]
    [InlineData("running", 1, "request {id} was confirmed meanwhile and is under way, so nothing was taken back")]
    [InlineData("ended", 1, "request {id} had already ended: it was done; `daoris-driver result {id}` says its answer.")]
    [InlineData("id-taken", 2, "refused id-taken. (id-taken)")]
    [InlineData("no-route", 2, "the Daoris running here cannot take a press back (request {id}): it is from before `withdraw`")]
    public async Task Withdraw_sends_the_pipes_own_withdraw_with_the_kept_key_and_says_each_answer(string answer, int code, string said)
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Drop;
        standIn.Result = "running";
        standIn.Withdraw = answer;
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);
        await PressAsync(World(), Open with { Class = RouteClass.Person });
        var press = Assert.Single(standIn.Presses);
        var id = press.GetProperty("id").GetString()!;
        _error.GetStringBuilder().Clear();

        var exit = await InstanceDoor.WithdrawAsync(id, World());

        Assert.Equal(code, exit);
        Assert.Contains(said.Replace("{id}", id), _error.ToString() + _out.ToString());
        var withdraw = Assert.Single(standIn.Withdraws);
        Assert.Equal("DAORIS.PIPE", withdraw.GetProperty("module").GetString());
        Assert.Equal(id, withdraw.GetProperty("payload").GetProperty("id").GetString());
        Assert.Equal(press.GetProperty("key").GetString(), withdraw.GetProperty("payload").GetProperty("key").GetString());
        Assert.DoesNotContain(standIn.Sent, each => each.GetProperty("type").GetString() == "CANCEL");
    }

    /// <summary>CONFIRMWAIT1b: an id this terminal never sent is withdrawn from nothing, and the instance is not asked.</summary>
    [Fact]
    public async Task Withdraw_of_an_id_it_never_sent_asks_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await InstanceDoor.WithdrawAsync("a-request-nobody-sent", World());

        Assert.Equal(2, exit);
        Assert.Equal(0, standIn.Connections);
        Assert.Contains("this terminal sent no request a-request-nobody-sent", _error.ToString());
    }

    /// <summary>
    /// CONFIRMWAIT1b (design §4.1): <c>--wait &lt;minutes&gt;</c>, 0 to 30, and <c>--no-wait</c>, which is <c>--wait 0</c>, are taken
    /// from the words in the host's entry before any verb reads them; no flag leaves the wait unset, and a wait out of bounds, without
    /// its minutes, or said twice is a problem.
    /// </summary>
    [Theory]
    [InlineData("agent rules allow Bash(git:*) --wait 5", "agent rules allow Bash(git:*)", 5)]
    [InlineData("--no-wait tool use git system", "tool use git system", 0)]
    [InlineData("setup Lyntai --wait 0", "setup Lyntai", 0)]
    [InlineData("setup Lyntai --wait 30", "setup Lyntai", 30)]
    [InlineData("trees list", "trees list", null)]
    public void The_wait_is_taken_from_the_words_before_the_verb_reads_them(string words, string rest, int? minutes)
    {
        Assert.True(InstanceDoor.TakeWait(words.Split(' '), out var left, out var wait, out var problem));
        Assert.Null(problem);
        Assert.Equal(rest.Split(' '), left);
        Assert.Equal(minutes is { } m ? TimeSpan.FromMinutes(m) : null, wait);
    }

    [Theory]
    [InlineData("setup Lyntai --wait 31")]
    [InlineData("setup Lyntai --wait -1")]
    [InlineData("setup Lyntai --wait soon")]
    [InlineData("setup Lyntai --wait")]
    [InlineData("setup Lyntai --wait 5 --no-wait")]
    [InlineData("setup Lyntai --no-wait --no-wait")]
    public void A_wait_out_of_bounds_or_said_twice_is_a_problem(string words)
    {
        Assert.False(InstanceDoor.TakeWait(words.Split(' '), out _, out _, out var problem));
        Assert.Contains("--wait", problem);
    }

    /// <summary>
    /// CONFIRMWAIT1b (design §4.1, §11): Ctrl+C still withdraws nothing, and the families' one sentence says both verbs, read by each of
    /// the four consoles; no client of this machine writes the kit's <c>CANCEL</c>.
    /// </summary>
    [Fact]
    public void The_four_ctrl_c_sentences_name_result_and_withdraw_and_withdraw_nothing()
    {
        Assert.Contains("may still be confirmed there", InstanceDoor.StoppedAtCtrlC);
        Assert.Contains("`daoris-driver result <id>` asks how it ended", InstanceDoor.StoppedAtCtrlC);
        Assert.Contains("`daoris-driver withdraw <id>` takes it back", InstanceDoor.StoppedAtCtrlC);
        var host = Path.Combine(SourceRoot(), "Daoris.Desktop.Driver.Host");
        foreach (var console in new[] { "AgentsConsole.cs", "DriverChoicesConsole.cs", "PluginConsole.cs", "ToolsConsole.cs" })
        {
            var source = File.ReadAllText(Path.Combine(host, console));
            Assert.Contains("InstanceDoor.StoppedAtCtrlC", source);
            Assert.DoesNotContain("may still be confirmed there", source);
        }

        Assert.DoesNotContain("\"CANCEL\"", File.ReadAllText(SourceFiles.Driver("InstanceClient.cs")));
    }

    /// <summary>The desktop's source folder, found up from the test's output.</summary>
    private static string SourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Daoris.Desktop.Driver.Host"))) return dir.FullName;
        }

        throw new InvalidOperationException("the desktop's source folder was not found above the test's output");
    }

    [Fact]
    public async Task Result_says_an_id_it_never_sent_is_unknown_without_asking_the_instance()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await InstanceDoor.ResultAsync("a-request-nobody-sent", World());

        Assert.Equal(2, exit);
        Assert.Equal(0, standIn.Connections);
        Assert.Contains("this terminal sent no request a-request-nobody-sent", _error.ToString());
    }

    [Fact]
    public async Task Result_says_an_id_the_instance_holds_for_another_key_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var standIn = Serve();
        standIn.Press = StandInPress.Drop;
        standIn.Result = "id-taken";
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);
        await PressAsync(World());
        var id = Assert.Single(standIn.Presses).GetProperty("id").GetString()!;
        _error.GetStringBuilder().Clear();

        var exit = await InstanceDoor.ResultAsync(id, World());

        Assert.Equal(2, exit);
        Assert.Contains("id-taken", _error.ToString());
    }

    [Fact]
    public async Task Result_with_no_instance_running_says_so_and_does_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        Serve();
        using (var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe))
        {
            await PressAsync(World());
        }

        var id = Assert.Single(_standIn!.Presses).GetProperty("id").GetString()!;
        _error.GetStringBuilder().Clear();

        var exit = await InstanceDoor.ResultAsync(id, World());

        Assert.Equal(2, exit);
        Assert.Contains("Daoris is not running on this home", _error.ToString());
    }

    [Fact]
    public async Task What_it_finds_where_the_instance_answers_is_its_client_and_says_so()
    {
        if (!OperatingSystem.IsWindows()) return;
        Serve();
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var look = await InstanceDoor.FindAsync(World());
        await using var client = look.Client;

        Assert.Equal(InstanceFound.Answers, look.Found);
        Assert.NotNull(client);
        Assert.Equal(_pipe, client.Pipe);
        Assert.Equal(Environment.ProcessId, client.Server);
    }
}
