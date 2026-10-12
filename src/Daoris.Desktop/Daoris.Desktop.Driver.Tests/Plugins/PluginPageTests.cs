using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin's page (PLUGUI1d, D119 §4.1): <see cref="PluginPage.Read"/> answers what the manifest declares as
/// written, each point's kind, wait and whether the running process listens there, a landing plugin's rules and
/// readiness, its source, its data folder counted up to a bound, and its tests — and never an environment value.
/// </summary>
public sealed class PluginPageTests : IDisposable
{
    private const string Secret = "s3cr3t-value-never-shown";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-page-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The Daoris home, beside the checkout a plugin was added from: a source inside the home is refused.</summary>
    private string Home => Path.Combine(_root, "home");

    public PluginPageTests() => Directory.CreateDirectory(Home);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 A value may be a key (D119 §4.1): the page answers a server's environment by name, and no value leaves the reader.</summary>
    [Fact]
    public void A_servers_environment_is_read_by_name_only_and_no_value_is_read_out()
    {
        Install("acme.tools", $$"""
            { "id": "acme.tools", "name": "Tools", "version": "1.2.0", "description": "Hands sessions a ticket system.",
              "servers": [
                { "name": "tickets", "command": ["node", "${plugin}/tickets.mjs"], "env": { "TICKETS_TOKEN": "{{Secret}}", "TICKETS_URL": "https://{{Secret}}.test" } },
                { "name": "browser", "command": ["node", "${plugin}/drive.mjs"], "env": { "CDP": "${browser}" } } ] }
            """);

        var page = PluginPage.Read(Home, "acme.tools");

        Assert.Equal(["TICKETS_TOKEN", "TICKETS_URL"], page.Servers[0].Environment);
        Assert.False(page.Servers[0].DrivesBrowser);
        Assert.True(page.Servers[1].DrivesBrowser);
        // The command as written: `${plugin}`, never a path on this machine.
        Assert.Equal(["node", "${plugin}/tickets.mjs"], page.Servers[0].Command);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(page));
    }

    [Fact]
    public void The_manifest_is_read_as_written_with_each_agent_and_the_hook()
    {
        Install("acme.agent", """
            { "id": "acme.agent", "name": "Agent", "version": "0.1.0",
              "harnesses": [ { "name": "acme-agent", "command": ["node", "${plugin}/agent.mjs"], "profileVariable": "ACME_HOME",
                               "package": "@acme/agent", "install": ["npm", "i", "-g", "@acme/agent"] } ],
              "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["session/ended"] } }
            """);

        var page = PluginPage.Read(Home, "acme.agent");

        Assert.Equal(("acme.agent", "Agent", "0.1.0", true, true), (page.Id, page.Name, page.Version, page.Enabled, page.Taken));
        var agent = Assert.Single(page.Agents);
        Assert.Equal(new PageAgent("acme-agent", ["node", "${plugin}/agent.mjs"], "protocol", null, "ACME_HOME", "@acme/agent", ["npm", "i", "-g", "@acme/agent"]),
            agent with { Command = [.. agent.Command], Install = agent.Install is null ? null : [.. agent.Install] },
            new AgentComparer());
        Assert.Equal(["node", "${plugin}/hooks.mjs"], page.Hook!.Command);
        Assert.Null(page.Landing);
    }

    /// <summary>Each point's kind and wait are the kit's; whether it listens is the running process's, and with none running there is nothing to say.</summary>
    [Fact]
    public void Each_point_says_its_kind_its_wait_and_whether_the_running_process_listens()
    {
        Install("acme.gate", """
            { "id": "acme.gate", "hooks": { "command": ["node", "hooks.mjs"], "points": ["quest/consider", "session/ended"] } }
            """);

        var idle = PluginPage.Read(Home, "acme.gate", new PluginHealth());
        var health = new PluginHealth();
        new PluginLog(null, health).Started("acme.gate", ["quest/consider"], 40, PluginEvents.ByLoop);
        var up = PluginPage.Read(Home, "acme.gate", health);

        Assert.All(idle.Points, point => Assert.Null(point.Listening));
        Assert.Equal("ready", idle.Health.State);
        Assert.Equal(
            [new PagePoint("quest/consider", "decision", 10_000, true), new PagePoint("session/ended", "observation", 10_000, false)],
            up.Points);
        Assert.Equal(("running", "loop"), (up.Health.State, up.HealthFrom));
    }

    [Fact]
    public void A_terminals_page_reads_its_health_from_the_log()
    {
        Install("acme.gate", """
            { "id": "acme.gate", "hooks": { "command": ["node", "hooks.mjs"], "points": ["quest/consider"] } }
            """);
        using (var log = new MachineLog(Home, "desktop"))
        {
            new PluginLog(log).Failed("acme.gate", "quest/consider", PluginEvents.Late, null, 10_000, PluginEvents.ByLoop);
        }

        var page = PluginPage.Read(Home, "acme.gate");

        Assert.Equal(("failing", "log"), (page.Health.State, page.HealthFrom));
        Assert.Equal("desktop", page.Health.Source);
    }

    [Fact]
    public void A_landing_plugin_says_the_rules_that_name_it_and_whether_it_can_land_work_here()
    {
        Install("acme.lands", """
            { "id": "acme.lands", "hooks": { "command": ["node", "land.mjs"], "points": ["work/land"] } }
            """);
        DriverConfig.Empty
            .WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "acme.lands"))
            .WithLanding("game", new LandingRule(LandingForm.Branch, "fix/{quest}", Plugin: "acme.other"))
            .WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "work/{quest}", Plugin: "acme.lands"))
            .Save(Path.Combine(Home, "driver.json"));

        var ready = PluginPage.Read(Home, "acme.lands");
        PluginState.Disable(Home, "acme.lands");
        var off = PluginPage.Read(Home, "acme.lands");

        Assert.Equal(
            [new PageRule("repository", "engine", "feature/{quest}-{slug}"), new PageRule("workspace", "aurora", "work/{quest}")],
            ready.Landing!.Rules);
        Assert.Null(ready.Landing.Problem);
        Assert.Equal(new PagePoint("work/land", "act", 120_000, null), Assert.Single(ready.Points));
        Assert.Contains("daoris plugin enable acme.lands", off.Landing!.Problem);
        Assert.Equal("off", off.Health.State);
    }

    /// <summary>A refused plugin's manifest is shown as written and marked not taken (D119 §3.2); one that does not read shows its sentence alone.</summary>
    [Fact]
    public void A_refused_plugins_manifest_is_shown_as_written_and_not_taken()
    {
        Install("a.first", """{ "id": "a.first", "servers": [ { "name": "tickets", "command": ["node", "t.mjs"] } ] }""");
        Install("b.second", """{ "id": "b.second", "servers": [ { "name": "tickets", "command": ["node", "t2.mjs"] } ] }""");
        Install("c.broken", """{ "id": "c.broken", "hooks": { "points": ["quest/consider"] }, }""");

        var conflict = PluginPage.Read(Home, "b.second");
        var broken = PluginPage.Read(Home, "c.broken");

        Assert.False(conflict.Taken);
        Assert.Contains("already declares", conflict.Problem);
        Assert.Equal("tickets", Assert.Single(conflict.Servers).Name);
        Assert.Equal("refused", conflict.Health.State);
        Assert.False(broken.Taken);
        Assert.Null(broken.Hook);
        Assert.Empty(broken.Points);
    }

    [Fact]
    public void The_data_folders_count_stops_at_its_bound_and_says_so()
    {
        var folder = Path.Combine(Home, "data");
        Directory.CreateDirectory(Path.Combine(folder, "inner"));
        for (var i = 0; i < 4; i++) File.WriteAllText(Path.Combine(folder, $"f{i}.txt"), "abc");
        File.WriteAllText(Path.Combine(folder, "inner", "deep.txt"), "abcdef");

        var whole = PluginPage.Count(folder, bound: 100, TimeSpan.FromSeconds(2));
        var bounded = PluginPage.Count(folder, bound: 3, TimeSpan.FromSeconds(2));
        var absent = PluginPage.Count(Path.Combine(Home, "nowhere"), bound: 100, TimeSpan.FromSeconds(2));

        Assert.Equal((true, 5, 18L, false), (whole.Exists, whole.Files, whole.Bytes, whole.More));
        Assert.NotNull(whole.Changed);
        Assert.True(bounded.More);
        Assert.True(bounded.Files <= 3);
        Assert.Equal((false, 0, 0L, false), (absent.Exists, absent.Files, absent.Bytes, absent.More));
    }

    [Fact]
    public void Its_source_whether_an_update_waits_and_the_tests_its_folder_carries()
    {
        var source = Path.Combine(_root, "checkout", "acme.gate");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "plugin.json"), """
            { "id": "acme.gate", "version": "1.1.0", "hooks": { "command": ["node", "hooks.mjs"], "points": ["quest/consider"] } }
            """);
        Install("acme.gate", """
            { "id": "acme.gate", "version": "1.0.0", "hooks": { "command": ["node", "hooks.mjs"], "points": ["quest/consider"] } }
            """);
        var installed = Path.Combine(Home, "plugins", "acme.gate");
        File.WriteAllText(Path.Combine(installed, PluginSource.FileName), JsonSerializer.Serialize(new { folder = source }));
        File.WriteAllText(Path.Combine(installed, "plugin.test.mjs"), "");
        Directory.CreateDirectory(Path.Combine(installed, "test"));
        File.WriteAllText(Path.Combine(installed, "test", "wire.js"), "");
        File.WriteAllText(Path.Combine(installed, "hooks.mjs"), "");
        Directory.CreateDirectory(Path.Combine(installed, "node_modules", "dep"));
        File.WriteAllText(Path.Combine(installed, "node_modules", "dep", "index.test.js"), "");
        Install("acme.bare", """{ "id": "acme.bare", "hooks": { "command": ["node", "h.mjs"], "points": ["quest/consider"] } }""");

        var page = PluginPage.Read(Home, "acme.gate");
        var bare = PluginPage.Read(Home, "acme.bare");

        Assert.Equal(("folder", source, "waits"), (page.Source.Kind, page.Source.Folder, page.Source.Update));
        Assert.Contains(page.Source.Changes, change => change.What == "version" && change.Now == "1.1.0");
        Assert.Equal(["plugin.test.mjs", "test/wire.js"], page.Tests);
        Assert.NotNull(page.InstalledAt);
        Assert.Equal(("none", null), (bare.Source.Kind, bare.Source.Update));
        Assert.Empty(bare.Tests);
        Assert.False(bare.Data.Exists);
    }

    [Fact]
    public void A_plugin_this_machine_does_not_hold_is_refused()
    {
        var refused = Assert.Throws<DriverException>(() => PluginPage.Read(Home, "acme.nobody"));

        Assert.Equal(PluginPage.Unknown("acme.nobody"), refused.Message);
    }

    // ——— helpers

    private void Install(string id, string manifest)
    {
        var folder = Path.Combine(Home, "plugins", id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
    }

    /// <summary>An agent's fields compared by value, lists included.</summary>
    private sealed class AgentComparer : IEqualityComparer<PageAgent>
    {
        public bool Equals(PageAgent? x, PageAgent? y) =>
            x is not null && y is not null
            && (x.Name, x.WayIn, x.Posture, x.ProfileVariable, x.Package) == (y.Name, y.WayIn, y.Posture, y.ProfileVariable, y.Package)
            && x.Command.SequenceEqual(y.Command)
            && (x.Install ?? []).SequenceEqual(y.Install ?? []);

        public int GetHashCode(PageAgent obj) => obj.Name.GetHashCode(StringComparison.Ordinal);
    }
}
