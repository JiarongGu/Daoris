using System.Net;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-x, the Workplace-names-Plugins cut: the seams the cut adds. Workplace declares the plugin wire's five operations, the
/// catalogue's two facets, the operations that make the wire and load the catalogue, and the lower patience and door words; Plugins
/// implements and installs them. The cases that need no new seam are <see cref="WorkplacePluginsBoundaryTests"/>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class WorkplacePluginsPortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workplace-plugins-ports", Guid.NewGuid().ToString("N")[..8]);
    private string ChatTree => DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s1");

    public WorkplacePluginsPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private void Install(string id, string manifest)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
    }

    // ——— The lower vocabulary

    [Fact]
    public void The_lower_patience_and_door_words_are_the_values_the_higher_names_alias()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), LandingPatience.Plugin);
        Assert.Equal(LandingPatience.Plugin, LandingPlugins.DefaultPatience);
        Assert.Equal(LandingPatience.Plugin + TimeSpan.FromMinutes(3), LandingHolds.Wait);
        Assert.Equal(("screen", "terminal"), (WorkplaceDoors.Screen, WorkplaceDoors.Terminal));
        Assert.Equal((WorkplaceDoors.Screen, WorkplaceDoors.Terminal), (PluginEvents.Screen, PluginEvents.Terminal));
    }

    // ——— The catalogue's facets

    [Fact]
    public void The_catalogue_and_its_entries_answer_the_facets_the_workplace_reads()
    {
        Install("acme.ready", """{ "id": "acme.ready", "version": "1.0.0", "hooks": { "command": ["node", "h.mjs"], "points": ["work/land", "work/state"] } }""");
        Install("acme.off", """{ "id": "acme.off", "version": "1.0.0", "hooks": { "command": ["node", "h.mjs"], "points": ["work/land"] } }""");
        PluginState.Disable(_home, "acme.off");
        Install("acme.broken", "{ not json");
        Install("acme.mute", """{ "id": "acme.mute", "version": "1.0.0" }""");
        var catalog = PluginCatalog.Load(_home);
        IWorkplacePluginCatalog facet = catalog;

        var ready = facet.Find("ACME.Ready")!;
        Assert.Equal(("acme.ready", true, null), (ready.Id, ready.Enabled, ready.Problem));
        Assert.Equal(["work/land", "work/state"], ready.Points);
        var off = facet.Find("acme.off")!;
        Assert.Equal(("acme.off", false, null), (off.Id, off.Enabled, off.Problem));
        var broken = facet.Find("acme.broken")!;
        Assert.NotNull(broken.Problem);
        Assert.Equal(catalog.Plugins.Single(entry => entry.Manifest.Id == "acme.broken").Problem, broken.Problem);
        Assert.Empty(facet.Find("acme.mute")!.Points);
        Assert.Null(facet.Find("acme.absent"));
        Assert.Null(facet.Find(""));

        // The ones the host drives, as the catalogue lists them: the same list, never a copy that could drift.
        Assert.Same(catalog.Contributing, facet.Contributing);
        Assert.Equal(["acme.mute", "acme.ready"], facet.Contributing.Select(entry => entry.Id));
        Assert.Empty(((IWorkplacePluginCatalog)PluginCatalog.None).Contributing);
        Assert.Null(((IWorkplacePluginCatalog)PluginCatalog.None).Find("acme.ready"));
    }

    [Fact]
    public void A_workflow_reads_the_plugins_of_any_catalogue_that_answers_the_facet()
    {
        var plugins = WorkflowCurrent.PluginsOf(new Standing(
            new Entry("b.one", ["work/land"]), new Entry("a.two", [])));

        Assert.Equal(["b.one", "a.two"], plugins.Select(plugin => plugin.Id));
        Assert.Equal(["work/land"], plugins[0].Points);
        Assert.Empty(plugins[1].Points);
    }

    // ——— The installed operations

    [Fact]
    public void The_installed_operations_are_plugins_and_make_the_original_wire_and_load_the_original_catalogue()
    {
        Assert.IsType<WorkplacePluginOperations>(WorkplacePlugins.Current);
        var wire = WorkplacePlugins.Current.Wire(_home);
        Assert.IsType<LandingPlugins>(wire);
        Assert.NotSame(wire, WorkplacePlugins.Current.Wire(_home));
        Assert.StartsWith("the landing rule names plugin `acme.lands`, which is not installed", wire.Problem("acme.lands"));
        Assert.Equal(DateTimeOffset.UtcNow, wire.Now, TimeSpan.FromMinutes(1));

        // The catalogue is read as the driver reads it: a harness name this build carries refuses the plugin that declares it.
        var reserved = AdapterSet.Built().Names.First();
        Install("acme.clash", JsonSerializer.Serialize(new { id = "acme.clash", version = "1.0.0", harnesses = new[] { new { name = reserved, command = new[] { "node", "a.mjs" } } } }));
        var loaded = Assert.IsType<PluginCatalog>(WorkplacePlugins.Current.Load(_home));
        Assert.NotNull(Assert.Single(loaded.Plugins).Problem);
        Assert.Equal(Assert.Single(PluginCatalog.Load(_home, AdapterSet.Built().Names).Plugins).Problem, loaded.Plugins[0].Problem);
        Assert.NotSame(loaded, WorkplacePlugins.Current.Load(_home));
    }

    [Fact]
    public void The_trees_make_the_default_wire_through_the_installed_operations_over_their_home_and_only_where_none_is_handed()
    {
        var actual = WorkplacePlugins.Current;
        var made = new Wire();
        var operations = new Operations(made);
        try
        {
            WorkplacePlugins.Current = operations;
            Assert.Same(made, new SessionTrees(_home).Context.Plugins);
            Assert.Equal([$"wire:{_home}"], operations.Calls);

            // A wire the caller hands is the one the trees use, and the operations are not asked.
            var handed = new Wire();
            Assert.Same(handed, new SessionTrees(_home, handed).Context.Plugins);
            Assert.Same(handed, new TreeContext(_home, handed).Plugins);
            Assert.Equal([$"wire:{_home}"], operations.Calls);
        }
        finally { WorkplacePlugins.Current = actual; }
    }

    [Fact]
    public async Task The_trees_speak_the_wire_they_hold_through_its_five_operations_alone()
    {
        var wire = new Wire();
        var trees = new SessionTrees(_home, wire);
        var frame = new LandingFrame("engine", "default", "D:/fam/engine", "feature/q1-fix", "main", "Fix", "q1", "s1", []);

        Assert.Equal(At, trees.Context.Plugins.Now);
        Assert.Equal("said:acme.lands", trees.Context.Plugins.Problem("acme.lands"));
        Assert.Equal("land:acme.lands:feature/q1-fix", (await trees.Context.Plugins.LandAsync("acme.lands", frame)).Message);
        Assert.Equal("hand:acme.lands:feature/q1-fix", (await trees.Context.Plugins.HandAsync("acme.lands", frame)).Message);
        // An occasion that has nothing due reads the moment once and asks the wire for no entry, as it always did.
        Assert.Empty(await new PullRequestAsks(trees.Context).AskStatesAsync([], landedMayGo: false, CancellationToken.None));

        Assert.Equal(
            ["now", "problem:acme.lands", "land:acme.lands", "hand:acme.lands", "now", "ask:0"],
            wire.Calls);
    }

    [Fact]
    public async Task A_missing_operation_is_required_only_where_it_is_reached_and_its_failures_propagate_unchanged()
    {
        var actual = WorkplacePlugins.Current;
        using var client = Client();
        var handed = new Wire();
        try
        {
            WorkplacePlugins.Current = null!;
            // A wire handed in needs no operations; the default wire and the catalogue's load do.
            Assert.Same(handed, new SessionTrees(_home, handed).Context.Plugins);
            var wire = Assert.Throws<InvalidOperationException>(() => new SessionTrees(_home));
            Assert.Contains("Plugins installs them", wire.Message);
            var load = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ChatLandCommand.RunAsync(World(client, handed), "s1", ChatTree, "chat-s1", Ended("s1"), null, null));
            Assert.Contains("Plugins installs them", load.Message);

            var refused = new IOException("refused");
            WorkplacePlugins.Current = new Operations(handed) { LoadFails = refused };
            Assert.Same(refused, await Assert.ThrowsAsync<IOException>(() =>
                ChatLandCommand.RunAsync(World(client, handed), "s1", ChatTree, "chat-s1", Ended("s1"), null, null)));
        }
        finally { WorkplacePlugins.Current = actual; }
    }

    [Fact]
    public async Task The_chat_land_loads_the_home_s_catalogue_once_through_the_supplied_operation_and_reads_its_plugins_for_the_binding()
    {
        var actual = WorkplacePlugins.Current;
        var operations = new Operations(new Wire());
        using var client = Client();
        try
        {
            WorkplacePlugins.Current = operations;
            Assert.Equal(1, await ChatLandCommand.RunAsync(World(client, new Wire()), "s1", ChatTree, "chat-s1", Ended("s1"), null, null));
        }
        finally { WorkplacePlugins.Current = actual; }

        // The press reads the catalogue once; other doors it passes through may make wires of their own.
        Assert.Equal([$"load:{_home}"], operations.Calls.Where(call => call.StartsWith("load:", StringComparison.Ordinal)));
    }

    private ServiceClient Client() => new("http://ledger.test", null,
        new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") })), disposeHandler: false));

    private ChatLandWorld World(ServiceClient service, IWorkplacePluginWire wire) =>
        new(service, _home, new SessionTrees(_home, wire), null, new StringWriter(), new StringWriter());

    private string Ended(string id) => JsonSerializer.Serialize(new[] { new { id, state = "completed", kind = "chat", repository = "engine", tree = ChatTree } });

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => answer(request);
    }

    private sealed record Entry(string Id, IReadOnlyList<string> Points) : IWorkplacePluginEntry
    {
        public bool Enabled => true;
        public string? Problem => null;
    }

    private sealed class Standing(params Entry[] entries) : IWorkplacePluginCatalog
    {
        public IWorkplacePluginEntry? Find(string plugin) => entries.FirstOrDefault(entry => entry.Id == plugin);
        public IReadOnlyList<IWorkplacePluginEntry> Contributing => entries;
    }

    private sealed class Wire : IWorkplacePluginWire
    {
        public List<string> Calls { get; } = [];

        public DateTimeOffset Now
        {
            get
            {
                Calls.Add("now");
                return At;
            }
        }

        public string? Problem(string plugin)
        {
            Calls.Add($"problem:{plugin}");
            return $"said:{plugin}";
        }

        public Task<PluginLanding> LandAsync(string plugin, LandingFrame frame, CancellationToken ct = default)
        {
            Calls.Add($"land:{plugin}");
            return Task.FromResult(new PluginLanding(plugin, true, null, $"land:{plugin}:{frame.Branch}"));
        }

        public Task<PluginLanding> HandAsync(string plugin, LandingFrame frame, CancellationToken ct = default)
        {
            Calls.Add($"hand:{plugin}");
            return Task.FromResult(new PluginLanding(plugin, true, null, $"hand:{plugin}:{frame.Branch}"));
        }

        public Task<IReadOnlyList<StateAnswer>> AskStatesAsync(IReadOnlyList<StateAsk> asks, CancellationToken ct = default)
        {
            Calls.Add($"ask:{asks.Count}");
            return Task.FromResult<IReadOnlyList<StateAnswer>>([]);
        }
    }

    private sealed class Operations(IWorkplacePluginWire made) : IWorkplacePluginOperations
    {
        public List<string> Calls { get; } = [];

        public Exception? LoadFails { get; init; }

        public IWorkplacePluginWire Wire(string home)
        {
            Calls.Add($"wire:{home}");
            return made;
        }

        public IWorkplacePluginCatalog Load(string home)
        {
            Calls.Add($"load:{home}");
            return LoadFails is { } failure ? throw failure : PluginCatalog.None;
        }
    }
}
