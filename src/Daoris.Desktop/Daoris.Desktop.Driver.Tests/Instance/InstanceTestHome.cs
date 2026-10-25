using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An install's home with a stand-in of the running Daoris's pipe on it (RPCOPEN1), for the cases of a verb that goes through the
/// instance: the verb's own words from the route's answer, held to the alone path's. Windows only, as the pipe is.
/// </summary>
internal sealed class InstanceTestHome : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-open-verbs-tests", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _app;
    private RunningAddress? _running;

    public InstanceTestHome()
    {
        // An install, as FINDDAORIS1 knows one: its marker beside `app/`, the driver host under it, its home in `data/`.
        var install = Path.Combine(_root, "install");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "");
        _app = Path.Combine(install, DaorisHome.InstallApp, "daoris-driver");
        Directory.CreateDirectory(_app);
        Home = Path.Combine(install, DaorisHome.InstallData);
        Directory.CreateDirectory(Home);
        Id = HomeIdentity.Ensure(Home)!;
        Pipe = PipeName.For(Id, Random.Shared.Next(100_000, 999_999));
    }

    public string Home { get; }

    public string Id { get; }

    public string Pipe { get; }

    public StringWriter Out { get; } = new();

    public StringWriter Error { get; } = new();

    /// <summary>The stand-in, serving once <see cref="Serve"/> is called.</summary>
    public InstanceStandIn? StandIn { get; private set; }

    /// <summary>Serves the stand-in on this home and leaves the address file naming its pipe, as a running Daoris does.</summary>
    public InstanceStandIn Serve()
    {
        StandIn = new InstanceStandIn(Pipe, Id);
        _running = RunningAddress.Write(Home, "http://localhost:5177", Pipe);
        return StandIn;
    }

    /// <summary>Where a verb looks for the instance: this home, its output and its error, and a bound a loaded machine cannot pass.</summary>
    public InstanceWorld World(string verb = "daoris-driver") => new()
    {
        Home = Home,
        // 🔴 Never the account's: a test reads no variable it was not handed (D171's FINDDAORIS1 note).
        Environment = _ => null,
        BaseDirectory = _app,
        Status = _ => new ServiceStatus("lexical", Id),
        Out = Out,
        Error = Error,
        Verb = verb,
        Answer = TimeSpan.FromMinutes(2),
        Wait = TimeSpan.FromMinutes(2),
    };

    public async ValueTask DisposeAsync()
    {
        _running?.Dispose();
        if (StandIn is not null) await StandIn.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
