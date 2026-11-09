using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the holder's verb is run against: an install with its home, a pipe name and an in-process stand-in for the instance, and the records
/// the print reads served by a stub service. Windows only where a pipe is served: each test serves its own stand-in, on its own home's name.
/// </summary>
public abstract class SuperviseHarness : IAsyncLifetime
{
    protected const string Key = "5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1";

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-supervise-tests", Guid.NewGuid().ToString("N")[..8]);
    protected string _install = "";
    protected string _home = "";
    protected string _app = "";
    protected string _id = "";
    protected string _pipe = "";
    private protected InstanceStandIn? _standIn;

    public Task InitializeAsync()
    {
        _install = Path.Combine(_root, "install");
        Directory.CreateDirectory(_install);
        File.WriteAllText(Path.Combine(_install, DaorisHome.InstallMarker), "");
        _app = Path.Combine(_install, DaorisHome.InstallApp, "daoris-driver");
        Directory.CreateDirectory(_app);
        _home = Path.Combine(_install, DaorisHome.InstallData);
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _id = HomeIdentity.Ensure(_home)!;
        _pipe = PipeName.For(_id, Random.Shared.Next(100_000, 999_999));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_standIn is not null) await _standIn.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>The records, the quest on its ask and the ask, as the print reads them.</summary>
    private sealed class Service : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath switch
            {
                "/api/sessions" => """[{"id":"s1","repository":"engine","state":"awaiting-person","quest":"q1","kind":"driven","created":"2026-10-22T08:00:00Z","entry":"ask:a1/engine"}]""",
                "/api/quests" => """[{"id":"q1","from":"ask #a1","to":"engine","title":"Report","body":"Write the report.","status":"taken"}]""",
                "/api/asks/a1" => """{"id":"a1","workspace":"w","sentence":"Write the report","state":"open","tier":"t","words":[],"goAheads":[{"number":1,"kind":"production","on":"engine","act":"push to origin","asked":[],"answer":{"approved":false,"at":"2026-10-24T08:00:00Z"}}]}""",
                _ => null,
            };
            return Task.FromResult(json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    protected InstanceWorld Instance(Func<int?, bool>? servedBy = null) => new()
    {
        Home = _home,
        Environment = _ => null,
        BaseDirectory = _app,
        Status = _ => new ServiceStatus("lexical", _id),
        Out = TextWriter.Null,
        Error = TextWriter.Null,
        Answer = TimeSpan.FromMinutes(2),
        Wait = TimeSpan.FromMinutes(2),
        ServedBy = servedBy,
    };

    private SessionsWorld World(ServiceClient service, InstanceWorld? instance, string? key = Key) =>
        new(service, _home, DriverConfig.Empty, false, null) { Instance = instance, HolderKey = () => key };

    protected async Task<(int Exit, string Said)> RunAsync(string[] args, InstanceWorld? instance, string? key = Key)
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Service()));
        var ask = SessionsCommand.Read(args, out var problem);
        Assert.True(problem is null, problem);
        var output = new StringWriter();
        var exit = await SessionsCommand.RunAsync(ask!, World(service, instance, key), output);
        return (exit, output.ToString().ReplaceLineEndings("\n"));
    }
}
