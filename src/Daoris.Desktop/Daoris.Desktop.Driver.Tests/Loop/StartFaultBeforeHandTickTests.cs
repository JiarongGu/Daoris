using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A start that is cancelled or throws after its record is opened and before the hand arms its own endings (the lock of its tree, the
/// files stage) ends that record like any other failure of the start, and the tree it held is let go.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class StartFaultBeforeHandTickTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-before-hand-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public StartFaultBeforeHandTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "before@example.com");
        Git("config", "user.name", "DRVREVIEW11");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public async Task A_close_between_the_open_and_the_hand_ends_the_opened_record_and_frees_the_tree()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        using var closing = new CancellationTokenSource(Poll.Patience);
        var closes = new ClosesAfterTheOpen(closing);
        using var client = new ServiceClient(service.Url, null, new HttpClient(closes, disposeHandler: false));
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Actors = StaffedRoles.On("acp-stub"),
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", "-e", "process.exit(0)"] },
        };
        var driver = new Daoris.Driver.Driver(client, config, ProviderRegistry.Built(), _home, processes: new SessionProcesses());

        try { await driver.RunOnceAsync(closing.Token).WaitAsync(Poll.Patience); }
        catch (OperationCanceledException) { }

        Assert.True(closes.Closed, "the driver was never closed after the open");
        var opened = service.Session("s1");
        Assert.Equal("stopped", opened["state"]!.GetValue<string>());
        Assert.Null(SessionLocks.HolderOf(opened["tree"]!.GetValue<string>()));
    }

    /// <summary>Closes the driver at the first call after the open of the record is answered, so the step that makes it throws.</summary>
    private sealed class ClosesAfterTheOpen(CancellationTokenSource closing) : DelegatingHandler(new HttpClientHandler())
    {
        private bool _opened;

        public bool Closed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_opened && !Closed)
            {
                Closed = true;
                await closing.CancelAsync().ConfigureAwait(false);
            }

            var response = await base.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions") _opened = true;
            return response;
        }
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
