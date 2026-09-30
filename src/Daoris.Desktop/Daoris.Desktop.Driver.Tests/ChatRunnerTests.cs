using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation resolves its harness through the roster's LIVE set (D64): the driver's tick hands
/// the roster the build's adapters plus whatever the plugins declare, and a runner built before a
/// plugin arrived must still find the harness it declared.
/// </summary>
public sealed class ChatRunnerTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-chat-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private AdapterSet Declared()
    {
        var folder = Path.Combine(_home, "plugins", "acme.agent");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }""");
        return AdapterSet.Built().WithPlugins(PluginCatalog.Load(_home, AdapterSet.Built().Names));
    }

    /// <summary>
    /// D107: a conversation in a repository is handed what a driven session there is — the person's union, a
    /// read of its kept files, and what it may reach across — with no refusal on its own tree or files.
    /// </summary>
    [Fact]
    public void A_conversations_rules_are_a_driven_sessions_with_its_kept_files_and_what_it_may_reach_across()
    {
        var file = PermissionFile.Empty with { Machine = new RuleLists(["Bash(make:*)"], [], []) };
        var across = new AcrossReach([new("game", "/work/game")], [], [new("personal", "/srv/personal")]);

        var rules = ChatRunner.RulesFor(file, "default", "engine", "/work/engine", "/data/chats/s1", across);

        Assert.Contains("Bash(make:*)", rules.Allow);
        Assert.Contains(PermissionRules.ReadRule("/data/chats/s1"), rules.Allow);
        Assert.Contains("Read(//work/game/**)", rules.Allow);
        Assert.Contains("Edit(//work/game/**)", rules.Deny);
        Assert.Contains("Read(//srv/personal/**)", rules.Deny);
        Assert.DoesNotContain(rules.Deny, rule => rule.Contains("/work/engine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_harness_declared_after_the_runner_was_built_is_found_through_the_roster_s_live_set()
    {
        var built = AdapterSet.Built();
        var roster = new HarnessRoster(built, Path.Combine(_home, "harnesses.json"));
        using var service = new ServiceClient("http://127.0.0.1:1", null);
        var runner = new ChatRunner(service, built, _home, new SessionProcesses(), harnesses: roster);
        var config = DriverConfig.Load(Path.Combine(_home, "driver.json"));

        // Before the tick handed the roster the plugins: the driver's own refusal, naming what exists.
        var unknown = await Assert.ThrowsAsync<DriverException>(() =>
            runner.StartAsync("engine", "acme-agent", config));
        Assert.Contains("unknown adapter 'acme-agent'", unknown.Message);

        // After: the harness resolves, and the conversation gets as far as asking the service —
        // which is nowhere here, so THAT is the failure, and it is not the adapter's.
        roster.Use(Declared());
        await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
            runner.StartAsync("engine", "acme-agent", config));
    }
}
