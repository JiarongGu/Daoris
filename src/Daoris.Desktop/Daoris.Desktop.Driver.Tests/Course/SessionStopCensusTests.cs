using System.Text.RegularExpressions;
using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every state the page counts active (<c>SESSION_ACTIVE</c>) has a person's stop at <c>session stop &lt;id&gt;</c>, the one the window's Stop runs.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class SessionStopCensusTests : NodeRunFixture
{
    private const string Graph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"release","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"release"},{"id":"r>h","kind":"after","from":"release","to":"handed"}]}
        """;

    /// <summary>The states the page's <c>SESSION_ACTIVE</c> spells, which every Stop… it offers is pressed on.</summary>
    public static TheoryData<string> Active() => [.. States()];

    private static IReadOnlyList<string> States()
    {
        var api = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Web", "src", "data", "api.ts"));
        var set = Regex.Match(api, @"SESSION_ACTIVE[^=]*=\s*new Set<SessionState>\(\[(?<states>[^\]]*)\]\)", RegexOptions.CultureInvariant).Groups["states"].Value;
        return [.. Regex.Matches(set, "'(?<state>[a-z-]+)'", RegexOptions.CultureInvariant).Select(match => match.Groups["state"].Value)];
    }

    [Fact]
    public void The_page_s_active_states_are_read_and_are_each_a_live_record_s()
    {
        var states = States();

        Assert.Equal(["queued", "starting", "working", "awaiting-person", "awaiting-step"], states);
        Assert.All(states, state => Assert.False(SessionStates.IsEnded(state)));
    }

    [Theory]
    [MemberData(nameof(Active))]
    public async Task The_person_s_stop_ends_or_moves_a_session_in_every_active_state(string state)
    {
        BoundTo(Graph);
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = new(RunUnit, "claimed", NodeStates.Reached) { Attempt = 1, Tries = 1 },
                ["release"] = new(RunUnit, "release", NodeStates.Active) { Attempt = 2, Tries = 2 },
            },
        });
        await using var service = StandInService.Start(_home);
        service.Seed("s1", state, parts: state == SessionStates.AwaitingStep ? new StepWait(RunId, "work").Note().Parts : null);
        using var client = new ServiceClient(service.Url, null);
        // The verb holds the home's lease from the top, as `session stop` alone does (VerbLeases).
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Verb, out _, "daoris session stop");
        var output = new StringWriter();

        var exit = await SessionsCommand.RunAsync(new SessionsAsk("stop") { Ids = ["s1"] }, new SessionsWorld(client, _home, DriverConfig.Empty, false, null), output);

        var ended = SessionStates.IsEnded(service.State("s1"));
        var runStopped = Files.Read(RunUnit)!.Pending.Any(word => word is { Node: "release", Act: NodeWordActs.Stop });
        Assert.True(exit == 0 && (ended || runStopped), $"{state}: exit {exit}, record {service.State("s1")}, run's stop kept {runStopped}: {output}");
    }
}
