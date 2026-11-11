using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A packet's reader held packet-only: on a harness that takes Daoris's rules, every tool denied and nothing allowed or asked, the
/// person's tier and its handed file alone, no server; on one that takes none, <c>not-enforced</c>, no file written, and the walk
/// passing that choice rather than starting it holding its tools.
/// </summary>
public sealed class PacketOnlyPostureTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "packet-only-posture", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    private string Folder => Path.Combine(_root, "reader");

    public PacketOnlyPostureTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Folder);
    }

    public void Dispose() => GitSeed.Discard(_root);

    [Theory]
    [InlineData("claude-code", OpinionPosture.PacketOnly)]
    [InlineData("claude-code-acp", OpinionPosture.PacketOnly)]
    [InlineData("codex-acp", OpinionPosture.NotEnforced)]
    public void The_posture_is_packet_only_where_the_harness_takes_Daoris_s_rules_and_not_enforced_elsewhere(string door, string posture)
    {
        var adapters = ProviderRegistry.Built();

        Assert.Equal(posture, PacketOnlyRules.Posture(adapters, adapters.Door(door)));
    }

    [Fact]
    public void The_shape_denies_every_tool_and_every_server_and_allows_and_asks_nothing()
    {
        var composed = new RuleLists(["Read", "Bash(npm test)", "mcp__fs__read_file"], ["Bash(git push:*)"], ["Bash(rm:*)"]);

        var shaped = PacketOnlyPermissions.Shape(composed);

        Assert.Empty(shaped.Allow);
        Assert.Empty(shaped.Ask);
        Assert.Contains("Bash(rm:*)", shaped.Deny);
        foreach (var tool in new[] { "Bash", "PowerShell", "Read", "Glob", "Grep", "Edit", "Write", "WebFetch", "WebSearch", "Task", "Agent", "Skill" })
        {
            Assert.Contains(tool, shaped.Deny);
        }

        Assert.Contains("mcp__acp", shaped.Deny);
        Assert.Contains($"mcp__{KnowledgeConnector.ServerName}", shaped.Deny);
    }

    [Theory]
    [InlineData("claude-code")]
    [InlineData("claude-code-acp")]
    public void On_claude_code_the_reader_is_handed_the_denying_file_its_tier_alone_and_no_server(string door)
    {
        var adapters = ProviderRegistry.Built();
        var adapter = adapters.Door(door);
        var info = new ProcessStartInfo("claude");

        var (file, meta, handed) = PacketOnlyRules.Hand(Home, adapters, adapter, info, "s-read1", Folder)!.Value;

        var permissions = JsonNode.Parse(File.ReadAllText(file!))!["permissions"]!;
        Assert.Empty(permissions["allow"]?.AsArray() ?? []);
        Assert.Empty(permissions["ask"]?.AsArray() ?? []);
        var denied = permissions["deny"]!.AsArray().Select(each => each!.GetValue<string>()).ToList();
        Assert.All(PacketOnlyPermissions.Tools, tool => Assert.Contains(tool, denied));
        Assert.True(handed.Shown > 0);
        if (adapter.Can.RulesOnWire)
        {
            var options = JsonNode.Parse(JsonSerializer.Serialize(meta))!["claudeCode"]!["options"]!;
            Assert.Equal(file, options["settings"]!.GetValue<string>());
            Assert.Equal("user", options["settingSources"]![0]!.GetValue<string>());
            Assert.True(options["strictMcpConfig"]!.GetValue<bool>());
        }
        else
        {
            var args = info.ArgumentList.ToList();
            Assert.Equal(file, args[args.IndexOf("--settings") + 1]);
            Assert.Equal("user", args[args.IndexOf("--setting-sources") + 1]);
            Assert.Contains("--strict-mcp-config", args);
            Assert.DoesNotContain("--mcp-config", args);
        }
    }

    [Fact]
    public void On_codex_nothing_is_handed_and_no_file_is_written()
    {
        var adapters = ProviderRegistry.Built();
        var info = new ProcessStartInfo("codex-acp");

        Assert.Null(PacketOnlyRules.Hand(Home, adapters, adapters.Door("codex-acp"), info, "s-read2", Folder));
        Assert.Empty(info.ArgumentList);
    }

    [Fact]
    public void The_walk_passes_a_choice_whose_harness_cannot_hold_its_tools_off_and_keeps_the_order_of_the_rest()
    {
        var codex = new ActorEffective("codex-acp", null, null, "1");
        var opus = new ActorEffective("claude-code", "opus", "high", "2");
        var unknown = new ActorEffective("no-such-agent", null, null, "3");

        var (held, passed) = PacketOnlyRules.Readers(ProviderRegistry.Built(), [codex, opus, unknown]);

        Assert.Equal([opus], held);
        Assert.Equal(["codex-acp", "no-such-agent"], passed);
    }
}
