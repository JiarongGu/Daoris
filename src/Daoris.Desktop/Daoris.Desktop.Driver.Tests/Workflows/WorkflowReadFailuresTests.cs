using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the workflow readers say where a file or a catalogue they read is gone or unreadable.</summary>
public sealed class WorkflowReadFailuresTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-workflow-reads-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A run's binding removed between the test that it exists and the read of its size reads as none, as one never written does.</summary>
    [Fact]
    public void A_binding_removed_while_it_is_read_reads_as_none()
    {
        var run = "q7";
        RunHome.Bind(_home, run, RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"landing","nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],"wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
            """), At);
        var files = new WorkflowRunFiles(_home) { BoundTested = path => File.Delete(path) };

        Assert.Null(files.Read(run));
    }

    /// <summary>A role catalogue that cannot be read holds a conversation's bind with its reason: the press is refused, no exception reaches the caller.</summary>
    [Fact]
    public void A_chat_bind_over_an_unreadable_role_catalogue_fails_with_its_reason()
    {
        var store = WorkflowPorts.Store(_home);
        StoredSteps.AddGraph(store, "staffed", "Staffed", "2026-11-01T09:00:00Z", JsonNode.Parse("""
            {"schemaVersion":2,"entries":["work"],"end":"landing","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed","options":{"role":"frontend"}},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],"wires":[{"id":"a","kind":"after","from":"work","to":"landing"}]}
            """)!);
        var config = DriverConfig.Parse("""{"workflows":{"web-app":{"default":"staffed"}}}""");
        var database = DesktopStore.LocationIn(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        File.WriteAllText(database, "this is not a database");

        var bound = ChatRuns.Bind(config, _home, "chat-c1a2b3c4", "web-app", null, [], At);

        Assert.Null(bound.Binding);
        Assert.Contains("role catalogue cannot be read", bound.Failed);
    }
}
