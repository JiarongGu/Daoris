using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the workflow readers and the migration say where a file or a catalogue they read is gone, unreadable or drops a step.</summary>
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

    /// <summary>A pushed landing's pull-request step dropped for want of a named plugin is said by the migration, naming the workflow.</summary>
    [Fact]
    public void A_dropped_pull_request_step_is_said_by_the_migration()
    {
        StoredSteps.AddGraph(WorkflowPorts.Store(_home), "pr-handed", "Handed with a pull request", "2026-11-01T09:00:00Z", JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"pr",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"},
                      {"id":"pr","kind":"pull-request","version":1,"reach":"observed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"},{"id":"h>p","kind":"after","from":"handed","to":"pr"}]}
            """)!);

        var migrated = WorkflowMigration.Convert(_home, DriverConfig.Empty, []);

        Assert.Contains(migrated.Said, line => line.Contains("`pr` opened a pull request", StringComparison.Ordinal) && line.Contains("`pr-handed`", StringComparison.Ordinal));
    }
}
