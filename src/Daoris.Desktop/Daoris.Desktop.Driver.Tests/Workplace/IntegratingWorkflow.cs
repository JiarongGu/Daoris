using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A place following a saved graph whose <c>integrate</c> step names who requests and where a conflict goes: the doors outside a run read
/// both from it (<see cref="PlaceIntegrate"/>), never from the line's policy. Each requester's graph is a workflow of its own, so one case
/// can hold a place under each.
/// </summary>
internal static class IntegratingWorkflow
{
    /// <summary>The graph: the work landed for integration, then its <c>integrate</c> step and the line.</summary>
    public static string Graph(string request, string conflicts) => $$$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"line",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"integrate"}},
                  {"id":"integrate","kind":"integrate","version":1,"reach":"waited","options":{"request":"{{{request}}}","conflicts":"{{{conflicts}}}"}},
                  {"id":"line","kind":"line","version":1,"reach":"observed"}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"},{"id":"l>i","kind":"after","from":"landing","to":"integrate"},
                  {"id":"i>n","kind":"after","from":"integrate","to":"line"}]}
        """;

    /// <summary>
    /// <paramref name="config"/> with this machine's default the workflow <c>integrated-&lt;request&gt;</c>, saved under <paramref name="home"/>
    /// with its step saying <paramref name="request"/> and <paramref name="conflicts"/>.
    /// </summary>
    public static DriverConfig Follow(string home, DriverConfig config, string request, string conflicts = LandingIntegrate.Session)
    {
        var workflow = "integrated-" + request;
        RunHome.SaveGraph(home, Graph(request, conflicts), workflow);
        return config.WithDefaultWorkflow(workflow);
    }
}
