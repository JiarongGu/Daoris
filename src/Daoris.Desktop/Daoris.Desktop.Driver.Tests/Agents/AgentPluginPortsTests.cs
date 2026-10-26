using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

public sealed class AgentPluginPortsTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "agent-plugin-ports", Guid.NewGuid().ToString("N")[..8]);
    public AgentPluginPortsTests() => Directory.CreateDirectory(_home);
    public void Dispose() => GitSeed.Discard(_home);

    private AgentsWorld World() => new() { Instance = new InstanceWorld { Home = _home } };

    [Fact]
    public void The_installed_catalogue_reaches_both_discovery_paths_and_keeps_order_provenance_and_skill_bytes()
    {
        Assert.IsType<InstalledAgentPluginReads>(AgentPluginReads.Current);
        Plugin("acme.z", "z-agent", "z-owner");
        Plugin("acme.a", "a-agent", "a-owner");
        Plugin("acme.off", "off-agent", "off-owner");
        PluginState.Disable(_home, "acme.off");
        var built = AdapterSet.Built();
        var catalog = AgentPluginReads.Current.Load(_home, built.Names);
        Assert.Equal(["a-agent", "z-agent"], catalog.Adapters.Select(adapter => adapter.Name));
        Assert.Equal(["a-agent", "a-owner", "off-agent", "off-owner", "z-agent", "z-owner"], catalog.DeclaredAgents);
        foreach (var set in new[] { built.WithPlugins(catalog), World().Adapters(_home) })
        {
            Assert.Equal("acme.a", set.DeclaredBy("a-agent"));
            Assert.Null(set.DeclaredBy("stub"));
            Assert.DoesNotContain("off-agent", set.Names);
            Assert.Contains("off-owner", set.Agents);
            Assert.Equal("stand-command", set.Resolve("a-agent").Prepare(Target(), null).FileName);
        }
        Assert.Equal(built.WithPlugins(catalog).Agents, HarnessSettings.AgentsKnown(_home));
        var (folder, plugins) = SpawnSkills.Write(_home, "s1", catalog, "sessions");
        Assert.Equal(["acme.a", "acme.z"], plugins.Select(Path.GetFileName));
        foreach (var plugin in plugins)
        {
            Assert.Equal(Skill, File.ReadAllText(Path.Combine(plugin, "skills", "read", "SKILL.md")));
            var manifest = File.ReadAllText(Path.Combine(plugin, ".claude-plugin", "plugin.json"));
            using var parsed = JsonDocument.Parse(manifest);
            Assert.Equal(["name", "version", "description"], parsed.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal("1.0.0", parsed.RootElement.GetProperty("version").GetString());
        }
        SpawnSkills.Remove(folder);
        Assert.False(Directory.Exists(folder));
        Assert.Equal(Skill, File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.a"), "skills", "read", "SKILL.md")));
    }

    [Fact]
    public void Independent_catalogue_and_adapter_contracts_work_without_the_installed_reader()
    {
        var actual = AgentPluginReads.Current;
        var catalog = new Catalog();
        var read = new Reads(catalog);
        try
        {
            AgentPluginReads.Current = read;
            Assert.Contains("kept-off-owner", HarnessSettings.AgentsKnown(_home));
            Assert.Equal("stand.plugin", World().Adapters(_home).DeclaredBy("stand-agent"));
            Assert.Equal(2, read.Homes.Count);
            Assert.All(read.Homes, home => Assert.Equal(_home, home));
            Assert.All(read.Reserved, names => Assert.Equal(AdapterSet.Built().Names, names));
            AgentPluginReads.Current = null!;
            Assert.Throws<InvalidOperationException>(() => HarnessSettings.AgentsKnown(_home));
            Assert.Throws<InvalidOperationException>(() => World().Adapters(_home));
            var built = AdapterSet.Built();
            var direct = built.WithPlugins(catalog);
            Assert.Same(built.Resolve("stub"), direct.Resolve("stub"));
            Assert.Null(direct.DeclaredBy("stub"));
            Assert.Equal("stand.plugin", direct.DeclaredBy("stand-agent"));
            var info = new ProcessStartInfo();
            var (folder, plugins) = SpawnSkills.Hand(new Adapter("stand-agent"), info, _home, "s2", catalog, "help");
            Assert.Equal("stand-folder", folder);
            Assert.Equal(["second", "first"], plugins);
            Assert.Equal(plugins, info.ArgumentList);
            Assert.Equal([(_home, "s2", "help")], catalog.Writes);
        }
        finally { AgentPluginReads.Current = actual; }
        Assert.Same(actual, AgentPluginReads.Current);
    }

    [Fact]
    public void Refusal_and_cancellation_are_propagated_without_handing_any_plugins()
    {
        var actual = AgentPluginReads.Current;
        var catalog = new Catalog();
        var refused = new IOException("catalogue refused");
        try
        {
            AgentPluginReads.Current = new Reads(catalog) { Failure = refused };
            Assert.Same(refused, Assert.Throws<IOException>(() => HarnessSettings.AgentsKnown(_home)));
            Assert.Same(refused, Assert.Throws<IOException>(() => World().Adapters(_home)));
            var cancelled = new OperationCanceledException(new CancellationToken(canceled: true));
            catalog.Failure = cancelled;
            var info = new ProcessStartInfo();
            Assert.Same(cancelled, Assert.Throws<OperationCanceledException>(() => SpawnSkills.Hand(
                new Adapter("stand-agent"), info, _home, "s3", catalog, "sessions")));
            Assert.Empty(info.ArgumentList);
            catalog.Failure = refused;
            Assert.Same(refused, Assert.Throws<IOException>(() => SpawnSkills.Write(_home, "s4", catalog, "help")));
        }
        finally { AgentPluginReads.Current = actual; }
    }

    private const string Skill = "---\nname: read\ndescription: Read the record.\n---\nRead it.\n";
    private void Plugin(string id, string harness, string owner)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(Path.Combine(folder, "skills", "read"));
        AtomicFile.WriteText(Path.Combine(folder, "plugin.json"), $$"""
            { "id": "{{id}}", "version": "1.0.0", "harnesses": [ { "name": "{{harness}}", "accountOf": "{{owner}}", "command": ["stand-command"] } ], "skills": [ { "name": "read" } ] }
            """);
        AtomicFile.WriteText(Path.Combine(folder, "skills", "read", "SKILL.md"), Skill);
    }

    private SessionTarget Target() => new("q1", "Title", "Body", "person", "engine", _home, "http://localhost");

    private sealed class Adapter(string name) : IPluginAdapter
    {
        public string Name => name;
        public string Plugin => "stand.plugin";
        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("stand-command");
        public void HandPlugins(ProcessStartInfo info, IReadOnlyList<string> plugins)
        {
            foreach (var plugin in plugins) info.ArgumentList.Add(plugin);
        }
    }

    private sealed class Catalog : IAgentPlugins
    {
        public IEnumerable<ISessionAdapter> Adapters => [new Adapter("stand-agent"), new Adapter("stub")];
        public IEnumerable<string> DeclaredAgents => ["stand-agent", "kept-off-owner"];
        public List<(string, string, string)> Writes { get; } = [];
        public Exception? Failure { get; set; }
        public (string? Folder, IReadOnlyList<string> Plugins) WriteSkills(string home, string sessionId, string to)
        {
            if (Failure is { } failure) throw failure;
            Writes.Add((home, sessionId, to));
            return ("stand-folder", ["second", "first"]);
        }
    }

    private sealed class Reads(IAgentPlugins catalog) : IAgentPluginReads
    {
        public List<string> Homes { get; } = [];
        public List<IReadOnlyList<string>> Reserved { get; } = [];
        public Exception? Failure { get; init; }
        public IAgentPlugins Load(string home, IEnumerable<string> reservedHarnesses)
        {
            if (Failure is { } failure) throw failure;
            Homes.Add(home);
            Reserved.Add(reservedHarnesses.ToList());
            return catalog;
        }
    }
}
