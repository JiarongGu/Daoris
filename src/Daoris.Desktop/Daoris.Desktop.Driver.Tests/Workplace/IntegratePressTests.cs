using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>INTEGRATE1h: <c>daoris-driver integrate</c>'s acts pressed over the pipe, against a stand-in of it.</summary>
public sealed class IntegratePressTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-integrate-press-tests", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private string _home = "";
    private string _app = "";
    private string _id = "";
    private string _pipe = "";
    private InstanceStandIn? _standIn;

    public Task InitializeAsync()
    {
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

    private InstanceWorld World() => new()
    {
        Home = _home,
        Environment = _ => null,
        BaseDirectory = _app,
        Status = _ => new ServiceStatus("lexical", _id),
        Out = _out,
        Error = _error,
        Verb = "integrate",
        Answer = TimeSpan.FromMinutes(2),
        Wait = TimeSpan.FromMinutes(2),
    };

    [Fact]
    public async Task A_request_is_pressed_as_the_integrate_route_and_the_answer_is_said()
    {
        if (!OperatingSystem.IsWindows()) return;
        _standIn = new InstanceStandIn(_pipe, _id) { Confirming = true, Data = new { done = true, message = "Request r1 is admitted." } };
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await IntegrateCommand.PressAsync(new IntegrateAsk("engine") { Act = "request", Target = "feature/a" }, World());

        Assert.Equal(0, exit);
        var sent = Assert.Single(_standIn.Presses);
        Assert.Equal("INTEGRATE", sent.GetProperty("type").GetString());
        var payload = sent.GetProperty("payload");
        Assert.Equal("engine", payload.GetProperty("repository").GetString());
        Assert.Equal("request", payload.GetProperty("act").GetString());
        Assert.Equal("feature/a", payload.GetProperty("branch").GetString());
        Assert.Contains("integrate: Request r1 is admitted.", _out.ToString());
    }

    [Fact]
    public async Task A_refusal_the_route_answers_as_data_is_said_and_exits_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        _standIn = new InstanceStandIn(_pipe, _id) { Data = new { done = false, why = "gone", message = "Request r9 is not open." } };
        using var running = RunningAddress.Write(_home, "http://localhost:5177", _pipe);

        var exit = await IntegrateCommand.PressAsync(new IntegrateAsk("engine") { Act = "drop", Target = "r9" }, World());

        Assert.Equal(1, exit);
        Assert.Contains("Request r9 is not open.", _out.ToString());
    }

    [Fact]
    public async Task With_no_daoris_running_nothing_is_done_and_the_window_is_named()
    {
        var exit = await IntegrateCommand.PressAsync(new IntegrateAsk("engine") { Act = "recheck" }, World());

        Assert.Equal(1, exit);
        Assert.Contains("a card in Daoris's window", _error.ToString());
    }

    [Fact]
    public async Task Decide_and_order_are_refused_here_until_their_steps_are_built()
    {
        var exit = await IntegrateCommand.PressAsync(new IntegrateAsk("engine") { Act = "decide", Target = "c1 land" }, World());

        Assert.Equal(1, exit);
        Assert.Contains("is not built yet; nothing was changed", _error.ToString());
    }

    [Fact]
    public void The_route_is_the_persons_and_the_census_classes_it_so()
    {
        Assert.Equal(RouteClass.Person, IntegrateCommand.Route.Class);
        Assert.Equal("INTEGRATE", IntegrateCommand.Route.Route);
    }
}
