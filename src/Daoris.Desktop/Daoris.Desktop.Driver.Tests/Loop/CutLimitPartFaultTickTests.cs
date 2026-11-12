using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A carry-on of a limit's part that a restart cut off before it ran reads the part the limit ended after its record is opened and its
/// tree held. Where that read throws, the opened record is ended like any other failure of the start, and nothing it held stays held.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class CutLimitPartFaultTickTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-cut-limit-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public CutLimitPartFaultTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "cut@example.com");
        Git("config", "user.name", "DRVREVIEW3");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public async Task A_read_of_the_part_the_limit_ended_that_throws_ends_the_opened_record_failed_and_frees_the_tree()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var then = DateTimeOffset.UtcNow.AddHours(-1);
        // A limit ended s1; a restart cut its carry-on s2 off before it ran, so the next start carries on s2.
        service.Keep(new JsonObject
        {
            ["id"] = "s1", ["quest"] = "q1", ["repository"] = "engine", ["state"] = "failed", ["kind"] = "drive", ["limit"] = true,
            ["created"] = then.ToString("O"),
        });
        service.Keep(new JsonObject
        {
            ["id"] = "s2", ["quest"] = "q1", ["repository"] = "engine", ["state"] = "failed", ["kind"] = "drive",
            ["from"] = "s1", ["fromWhy"] = "limit", ["fromBy"] = "daoris", ["created"] = then.AddMinutes(1).ToString("O"),
        });
        service.Close("q1", "Taken", "");
        var failing = new FailsTheReadOfThePartTheLimitEnded();
        var adapters = ProviderRegistry.Built();
        using var client = new ServiceClient(service.Url, null, new HttpClient(failing, disposeHandler: false));
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],            Actors = StaffedRoles.On("acp-stub"),
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", "-e", "process.exit(0)"] },
        };
        var driver = new Daoris.Driver.Driver(client, config, adapters, _home, processes: new SessionProcesses());

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.True(failing.Failed, "the part the limit ended was never read");
        var opened = service.Session("s3");
        Assert.Equal("failed", opened["state"]!.GetValue<string>());
        Assert.Contains("the service is down for this call", opened["note"]?.GetValue<string>());
        Assert.Null(SessionLocks.HolderOf(opened["tree"]!.GetValue<string>()));
    }

    /// <summary>
    /// Refuses the second poll of the kept records after a record is opened, which is the read of the part the limit ended: the first is
    /// the open's own, and the stages between the open and the hand read nothing of the records.
    /// </summary>
    private sealed class FailsTheReadOfThePartTheLimitEnded : DelegatingHandler
    {
        private bool _opened;
        private int _polls;

        public FailsTheReadOfThePartTheLimitEnded() : base(new HttpClientHandler())
        {
        }

        public bool Failed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path == "/api/sessions") _opened = true;
            else if (_opened && !Failed && request.Method == HttpMethod.Get && path == "/api/sessions/changes" && ++_polls == 2)
            {
                Failed = true;
                throw new InvalidOperationException("the service is down for this call");
            }

            return base.SendAsync(request, ct);
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
