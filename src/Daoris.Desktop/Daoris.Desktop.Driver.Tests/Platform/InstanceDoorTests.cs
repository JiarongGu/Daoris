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

    private InstanceStandIn Serve()
    {
        _standIn = new InstanceStandIn(_pipe, _id);
        return _standIn;
    }

    private InstanceWorld World(Func<string, string?>? environment = null, string? baseDirectory = null, string? statusHome = null,
                                TimeSpan? wait = null, TimeSpan? answer = null) => new()
    {
        Home = _home,
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
        Assert.Single(standIn.Presses);
        Assert.Equal($"trees: archived s1a2b3c4.{Environment.NewLine}", _out.ToString());
        Assert.Equal($"trees: waiting for you to confirm this in Daoris's window…{Environment.NewLine}", _error.ToString());
    }

    /// <summary>RPC1c: the person's refusal, and two minutes with no answer, are the verb's exit 1 with the instance's sentence; nothing is done alone.</summary>
    [Theory]
    [InlineData("refused")]
    [InlineData("expired")]
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
        Assert.False(Directory.Exists(Path.Combine(_home, "pipe", "sent")));
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
