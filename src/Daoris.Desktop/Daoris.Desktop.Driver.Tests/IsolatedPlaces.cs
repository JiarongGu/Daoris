using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A place whose workflow starts with the <c>isolate</c> step, for the tests that need its sessions to work apart: given to a planner's
/// snapshot directly, or to a driver's config and home as a saved workflow the config chooses.
/// </summary>
public static class IsolatedPlaces
{
    private const string Flow = "apart-flow";

    /// <summary>The snapshot with <paramref name="repositories"/> set apart by <paramref name="step"/> (a tree each, waiting on no overlap, where none is said).</summary>
    public static Snapshot Apart(this Snapshot snapshot, params string[] repositories) => snapshot.Apart(IsolateStep.Trees, repositories);

    public static Snapshot Apart(this Snapshot snapshot, IsolateStep step, params string[] repositories) => snapshot with { Isolations = Of(step, repositories) };

    public static Snapshot Apart(this Snapshot snapshot, bool apart, params string[] repositories) => apart ? snapshot.Apart(repositories) : snapshot;

    /// <summary>A snapshot's isolations: each of <paramref name="repositories"/> with a tree of its own.</summary>
    public static IReadOnlyDictionary<string, IsolateStep> Of(params string[] repositories) => Of(IsolateStep.Trees, repositories);

    public static IReadOnlyDictionary<string, IsolateStep> Of(IsolateStep step, params string[] repositories) =>
        repositories.ToDictionary(repository => repository, _ => step, StringComparer.OrdinalIgnoreCase);

    /// <summary>The config choosing, for each of <paramref name="repositories"/>, a workflow saved in <paramref name="home"/> that starts with the isolate step.</summary>
    public static DriverConfig Isolating(this DriverConfig config, string home, params string[] repositories) => config.Isolating(home, overlapWaits: false, repositories);

    public static DriverConfig Isolating(this DriverConfig config, string home, bool overlapWaits, params string[] repositories) =>
        repositories.Aggregate(config, (each, repository) => WorkflowSelection.Apply(each, new WorkflowUse(In(home, overlapWaits), repository, null, null)));

    /// <summary>A watch's <c>driver.json</c> text: the engine and tools driven on the stub, every place working apart by the workflow saved in <paramref name="home"/>.</summary>
    public static string WatchConfig(string home, int pollSeconds) =>
        $$"""{ "drivable": ["engine", "tools"], "adapter": "stub", "cap": 3, "pollSeconds": {{pollSeconds}}, "defaultWorkflow": "{{In(home)}}" }""";

    /// <summary>Saves the workflow in <paramref name="home"/> and answers its id, for a config's <c>DefaultWorkflow</c>: every place it reaches works apart.</summary>
    public static string In(string home, bool overlapWaits = false, string? checks = null)
    {
        var options = checks is null ? "" : $$""","options":{"checks":"{{checks}}"}""";
        var plain = JsonNode.Parse($$$"""
            {"id":"{{{Flow}}}","name":"Apart","versions":[{"version":1,"at":"2026-11-01T09:00:00Z","door":"screen","schema":2,"graph":{"schemaVersion":2,"entries":["work"],"end":"line",
             "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"{{{options}}} },
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}},
                      {"id":"line","kind":"line","version":1,"reach":"observed"}],
             "wires":[{"id":"a","kind":"after","from":"work","to":"landing"},{"id":"b","kind":"after","from":"landing","to":"line"}]}}]}
            """)!.AsObject();
        WorkflowPorts.Store(home).Write(Flow, IsolatedFiles.Isolated(plain, overlapWaits ? IsolateStep.Wait : "")!);
        return Flow;
    }
}
