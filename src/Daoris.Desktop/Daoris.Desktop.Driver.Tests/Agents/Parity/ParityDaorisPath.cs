using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The Daoris path over a scenario program (the agent-lyntai design §6.3, 1b-i): a native door Daoris runs on <see cref="TransportRun"/>,
/// its spawn the program's command with the prompt on its input, its wire read by <see cref="ScenarioDialect"/>, its failure read at the
/// conclusion from the door's tables, each thing it did observed for the oracle.
/// </summary>
internal sealed class ParityDaorisPath(string home) : IDisposable
{
    public const string Door = "parity-daoris";

    private readonly ServiceClient _service = new("http://127.0.0.1:9", null);

    public void Dispose() => _service.Dispose();

    /// <summary>One turn of <paramref name="command"/> with <paramref name="prompt"/>, observed.</summary>
    public async Task<ParityObservation> RunAsync(IReadOnlyList<string> command, string prompt, DateTimeOffset start, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.Combine(home, "engine"));
        var processes = new SessionProcesses(DaorisHome.Entry(home, HomeEntry.Sessions));
        var driver = new Daoris.Driver.Driver(_service, DriverConfig.Empty, ProviderRegistry.Built(), home, processes);
        var registry = TestDoors.Registry(TestDoors.Door(Door, resumes: true, spawn: new Spawn(command), dialect: new ScenarioDialect()));
        var adapter = registry.Door(Door);
        var target = new SessionTarget("q-1", "title", "body", "asker", "engine", Path.Combine(home, "engine"), "http://127.0.0.1:9") { Home = home };
        var transcript = Path.Combine(home, "daoris.log");
        var seen = new ParityObservation("daoris", start);

        await using (var run = new TransportRun(
            new DriverTransportHost(driver), registry, adapter,
            new TransportStart(registry.Start(adapter, target, null), StartClass.Background)
            {
                Target = target, Request = SpawnRequests.Of(target, null), Selection = new HarnessSelection(null), Prompt = prompt,
            }))
        {
            run.HandStart(run.Start with { Session = "s1", Transcript = transcript, Own = new HarnessWords() });
            run.HandSink(ParityObserver.Sink(seen));
            await run.OpenAsync(ct);
            var end = await run.EndAsync(ct);
            ParityObserver.Ended(seen, end, stopped: false);
            if (end.Ending.Failure is { } words)
            {
                var error = AgentErrors.Read(registry, adapter, DoorFailures.OfResult(words), start, TimeZoneInfo.Utc);
                ParityObserver.Failed(seen, error, ParityObserver.Heard(seen));
            }
        }

        ParityObserver.Transcript(seen, transcript);
        return seen;
    }

    private sealed class Spawn(IReadOnlyList<string> command) : ISpawn
    {
        public ProcessRecipe Start(SpawnRequest request) => new ProcessRecipe(command[0], [.. command.Skip(1)]) with { Input = true };

        public ProcessRecipe Chat(SpawnRequest request) => Start(request);
    }
}
