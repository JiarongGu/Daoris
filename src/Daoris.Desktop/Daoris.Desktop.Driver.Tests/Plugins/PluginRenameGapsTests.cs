using System.Text.Json.Nodes;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A rename reaches what runs as well as what is kept: a loop's plugins, the desktop's and the headless host's alike, start after the
/// renames have moved (<see cref="PluginsAtStart"/>); an attempt still in flight under the old id is carried to the new one, its step
/// kind with it; and a saved graph's node of the old kind is drawn as the new id's step, with no save.
/// </summary>
public sealed class PluginRenameGapsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-rename-gaps-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Plugin(string id, string declared = "")
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, PluginCatalog.ManifestName),
            $$"""{ "id": "{{id}}", "version": "1.0.0", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] }{{declared}} }""");
    }

    [Fact]
    public async Task A_loop_s_plugins_start_after_the_renames_have_moved()
    {
        Plugin("acme-tickets");
        Plugin("acme-desk", """, "formerly": ["acme-tickets"]""");

        await using (PluginsAtStart.Set(_home, say: null, output: null, log: null))
        {
            Assert.Equal(["acme-desk"], PluginCatalog.Load(_home).Plugins.Select(each => each.Manifest.Id));
        }
    }

    private static NodeAttempt Attempt(int number, string plugin, string state) =>
        new("open", number, plugin, new WorkPerformCall("unit-1", $"{plugin}/pull-request", 1, $"i{number}", $"k{number}", "r1", [], At), state)
        {
            RecordedAt = At, Generation = 4, Deliveries = 1, Outcome = state == AttemptStates.Settled ? WorkStates.Failed : null,
        };

    [Fact]
    public void An_attempt_in_flight_under_the_old_id_is_carried_to_the_new_one()
    {
        Plugin("acme-desk", """, "formerly": ["acme-tickets"]""");
        using var held = HomeLease.TryAcquire(_home, LeaseKind.Verb, out _, "daoris plugin update");
        var records = new NodeRecordFiles(_home);
        records.Write(new NodeRecord("unit-1")
        {
            Statuses = new Dictionary<string, NodeStatus> { ["open"] = new("unit-1", "open", NodeStates.Active) { Attempt = 2, Tries = 2, Unreconciled = 2 } },
            Attempts = [Attempt(1, "acme-tickets", AttemptStates.Settled), Attempt(2, "acme-tickets", AttemptStates.Sent)],
        });

        Assert.Null(Assert.Single(PluginCatalog.Load(_home).Plugins).Problem);
        Assert.Equal(2, records.Read("unit-1")!.Attempts.Count);
        Assert.True(HomeLease.HeldHere(_home));
        var rename = Assert.Single(PluginRenames.Move(_home));
        Assert.Contains(PluginRenames.Attempts, rename.Moved);

        var attempts = records.Read("unit-1")!.Attempts;
        var (settled, sent) = (attempts.Single(each => each.Attempt == 1), attempts.Single(each => each.Attempt == 2));
        Assert.Equal(("acme-tickets", "acme-tickets/pull-request"), (settled.Plugin, settled.Call.Step));
        Assert.Equal(("acme-desk", "acme-desk/pull-request", 4), (sent.Plugin, sent.Call.Step, sent.Generation));
        Assert.Empty(PluginRenames.Move(_home));
    }

    [Fact]
    public void A_saved_graph_s_old_kind_is_drawn_as_the_new_id_s_step()
    {
        var raw = JsonNode.Parse("""{ "nodes": [{ "id": "open", "kind": "acme-tickets/pull-request" }, { "id": "end", "kind": "end" }], "wires": [] }""")!;
        var formerly = new Dictionary<string, string> { ["acme-tickets"] = "acme-desk" };
        NodeDeclaration Node(string id, string kind) => new(id, PluginFormerKinds.KindOf(kind, formerly), 1, "repository");
        var read = new NodeGraph([Node("open", "acme-tickets/pull-request"), Node("end", "end")], [], ["open"], "end");

        var drawn = PluginFormerKinds.Drawn(raw, read);

        Assert.Equal("acme-desk/pull-request", (string?)drawn["nodes"]![0]!["kind"]);
        Assert.Equal("end", (string?)drawn["nodes"]![1]!["kind"]);
        Assert.Equal("acme-tickets/pull-request", (string?)raw["nodes"]![0]!["kind"]);
    }
}
