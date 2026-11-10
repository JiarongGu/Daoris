using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What holds a supervisor's turn read-only on a harness that takes Daoris's rules: the reviewer's shape with the shell denied whole and
/// no shell allow kept, the person's interpreter's among them; its connector's reads alone; the tree guard whatever the person's defaults;
/// and on Claude Code, the person's tier with the handed file and Daoris's connector alone, on either door.
/// </summary>
public sealed class SuperviseContainmentTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervise-containment", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    private string Copy => Path.Combine(_root, "copy");

    public SuperviseContainmentTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Copy);
    }

    public void Dispose() => GitSeed.Discard(_root);

    private const string Connector = $"mcp__{KnowledgeConnector.ServerName}__";

    [Fact]
    public void The_shape_denies_the_shell_whole_and_every_file_writer_keeps_no_shell_allow_and_offers_the_reads_alone()
    {
        var composed = new RuleLists(["Bash(python:*)", "Read", "mcp__fs__write_file", Connector + "quest_publish"], ["Bash(npm test)"], ["Bash(rm:*)"]);

        var shaped = SupervisePermissions.Shape(composed);

        Assert.Contains("Bash", shaped.Deny);
        Assert.Contains("PowerShell", shaped.Deny);
        foreach (var writer in new[] { "Edit", "MultiEdit", "Write", "NotebookEdit" }) Assert.Contains(writer, shaped.Deny);
        Assert.DoesNotContain(shaped.Allow, rule => rule.StartsWith("Bash", StringComparison.Ordinal));
        Assert.DoesNotContain(shaped.Ask, rule => rule.StartsWith("Bash", StringComparison.Ordinal));
        Assert.Contains("Read", shaped.Allow);
        Assert.Contains(Connector + "line_read", shaped.Allow);
        Assert.DoesNotContain(Connector + "line_read", shaped.Deny);
        foreach (var acting in new[] { "opinion_give", "helper_answer", "quest_publish", "permission_propose" }) Assert.Contains(Connector + acting, shaped.Deny);
        Assert.DoesNotContain(Connector + "quest_publish", shaped.Allow);
    }

    [Fact]
    public void Its_offered_reads_are_the_call_table_s_supervise_column()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "call-table.json")));
        var column = table.RootElement.GetProperty("columns").GetProperty("supervise").GetProperty("only").EnumerateArray().Select(each => each.GetString()!);

        Assert.Equal(column.Order(StringComparer.Ordinal), SupervisePermissions.Offered.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void On_claude_code_the_turn_takes_the_person_s_tier_and_its_handed_file_alone_and_the_tree_guard_with_it_switched_off(string name)
    {
        var rules = PermissionRules.Load(Home);
        PermissionRules.Save(Home, rules with { DefaultsOff = [.. rules.DefaultsOff, PermissionRules.TreeGuardId] });
        var adapters = ProviderRegistry.Built();
        var adapter = adapters.Door(name);
        using var service = new ServiceClient("http://127.0.0.1:9", null);
        var driver = new Daoris.Driver.Driver(service, DriverConfig.Empty, adapters, Home);
        var info = new ProcessStartInfo("claude");

        var (file, meta, _) = driver.HandRules(adapter, info, "s-sup1", null, "engine", Copy, kept: null, supervise: true);
        var (plain, plainMeta, _) = driver.HandRules(adapter, new ProcessStartInfo("claude"), "s-plain", null, "engine", Copy, kept: null);

        static int Guards(string path) => JsonNode.Parse(File.ReadAllText(path))!["hooks"]?["PreToolUse"]?.AsArray()
            .Count(entry => (string?)entry?["matcher"] == TreeGuardHook.Matcher) ?? 0;
        Assert.True(Guards(file!) > Guards(plain!), "the supervise turn is handed the tree guard whatever the defaults say");
        if (adapter.Can.RulesOnWire)
        {
            var options = JsonNode.Parse(JsonSerializer.Serialize(meta))!["claudeCode"]!["options"]!;
            Assert.Equal("user", options["settingSources"]![0]!.GetValue<string>());
            Assert.True(options["strictMcpConfig"]!.GetValue<bool>());
            Assert.Null(JsonNode.Parse(JsonSerializer.Serialize(plainMeta))!["claudeCode"]!["options"]!["strictMcpConfig"]);
        }
        else
        {
            var args = info.ArgumentList.ToList();
            Assert.Equal("user", args[args.IndexOf("--setting-sources") + 1]);
            Assert.Contains("--strict-mcp-config", args);
        }
    }
}
