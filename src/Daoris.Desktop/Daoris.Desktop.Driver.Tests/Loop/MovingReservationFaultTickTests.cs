using System.Diagnostics;
using System.Net.Http;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A start refused for its account's sign-in reserves its quest for the move to the next account. Where its conclusion throws after the
/// reservation is taken, the start ends as a failure that names no move, so nothing releases the reservation unless the conclusion does.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class MovingReservationFaultTickTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-moving-fault-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public MovingReservationFaultTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "moving@example.com");
        Git("config", "user.name", "DRVREVIEW2");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"])
            .Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public async Task A_conclusion_that_throws_after_the_sign_in_refusal_reserved_the_quest_lets_it_go()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var adapters = ProviderRegistry.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var refusing = new FailsTheFirstEnd();
        using var client = new ServiceClient(service.Url, null, new HttpClient(refusing, disposeHandler: false));
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],            Actors = StaffedRoles.On("acp-stub"),
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", RefusingAgent()] },
        };
        var driver = new Daoris.Driver.Driver(client, config, adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.True(refusing.Failed, "the conclusion's end was never refused");
        Assert.Empty(driver.Context.Runs.Reserved);
    }

    /// <summary>Refuses the first move of a record to failed, as a service that is down for that one call does.</summary>
    private sealed class FailsTheFirstEnd : DelegatingHandler
    {
        public FailsTheFirstEnd() : base(new HttpClientHandler())
        {
        }

        public bool Failed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!Failed && request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/state", StringComparison.Ordinal)
                && request.Content is { } content && (await content.ReadAsStringAsync(ct)).Contains("\"failed\"", StringComparison.Ordinal))
            {
                Failed = true;
                throw new HttpRequestException("the service is down for this call");
            }

            return await base.SendAsync(request, ct);
        }
    }

    /// <summary>The protocol door's stand-in, refusing every <c>session/new</c> for its sign-in as Claude Code's adapter does.</summary>
    private string RefusingAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, error: { code: -32000, message: 'Authentication required' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
            """);
        return script;
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
