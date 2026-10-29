using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row b, part 1 (D213's row b note): the readers that decide whether a stand-in is listed, named or told apart read the
/// registry by one answer each, and the two rules stay apart (C1-4.1): the roster lists a door that names a binary or that the home's
/// <c>commands</c> configure, and the verbs take an operand that names a binary, so no stand-in is one, configured or not. The reserved
/// names are every built provider and door id, never a made provider's (O4-F9).
/// </summary>
public sealed class StandInReadersTests
{
    private static readonly IReadOnlyList<string> Configured = ["node", "agent.mjs"];

    /// <summary>The sets the readers are held over: the built one, the classes beside the built others, and a plugin's door of the stand-in's kind.</summary>
    public static TheoryData<string> Sets => new() { "built", "classes", "plugin" };

    private static AdapterSet Set(string which) => which switch
    {
        "classes" => new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["stub"] = new StubAdapter(),
            ["acp-stub"] = new AcpStubAdapter(),
            ["claude-code"] = new ClaudeCodeAdapter(),
            ["codex-acp"] = new CodexAcpAdapter(),
        }, new Dictionary<string, HarnessToolchain>(StringComparer.OrdinalIgnoreCase) { [CodexAccounts.Agent] = CodexAccounts.Toolchain }),
        "plugin" => AdapterSet.Built().WithPlugins(new SingleDoor(new DeclaredAcpAdapter(new PluginHarness("acme-door", ["acme"]), "acme-plugin"))),
        _ => AdapterSet.Built(),
    };

    /// <summary>The roster's listing as the code spelled it: skip a door that names no binary and that the home configures no command for.</summary>
    private static List<string> OldListing(AdapterSet set, IReadOnlyDictionary<string, IReadOnlyList<string>> commands) => [.. set.Names.Where(name =>
        !(set.Resolve(name).Toolchain is { Binary.Count: 0 } && commands.GetValueOrDefault(name) is not { Count: > 0 }))];

    /// <summary>The verbs' operands as the code spelled them: each door that names a real program and each agent a door runs as.</summary>
    private static List<string> OldOperands(AdapterSet set)
    {
        bool Door(string each) => set.Names.Contains(each, StringComparer.OrdinalIgnoreCase);
        HarnessToolchain? Of(string each) => Door(each) ? set.Resolve(each).Toolchain : set.Holder(each);
        return [.. set.Reserved.Where(each => Of(each) is { Binary.Count: > 0 })];
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void The_roster_lists_what_it_listed_with_and_without_a_stand_in_s_command_configured(string which)
    {
        var set = Set(which);
        var registry = ProviderRegistry.Of(set);
        foreach (var commands in new[]
        {
            new Dictionary<string, IReadOnlyList<string>>(),
            new Dictionary<string, IReadOnlyList<string>> { ["stub"] = Configured },
            new Dictionary<string, IReadOnlyList<string>> { ["stub"] = Configured, ["acp-stub"] = Configured },
        })
        {
            var listed = registry.Doors.Where(door => registry.Listed(door, commands.GetValueOrDefault(door.Value) is { Count: > 0 })).Select(door => door.Value);

            Assert.Equal(OldListing(set, commands).Order(StringComparer.Ordinal), listed.Order(StringComparer.Ordinal));
            Assert.Equal(commands.ContainsKey("stub"), registry.Listed(new DoorId("stub"), commands.ContainsKey("stub")));
        }
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void The_verbs_operands_are_what_they_were_and_name_no_stand_in_configured_or_not(string which)
    {
        var set = Set(which);

        var operands = ProviderRegistry.Of(set).Operands();

        Assert.Equal(OldOperands(set), operands);
        Assert.DoesNotContain("stub", operands);
        Assert.DoesNotContain("acp-stub", operands);
    }

    [Fact]
    public void The_stand_ins_name_no_program_so_the_two_rules_differ_only_by_the_command()
    {
        var registry = ProviderRegistry.Of(AdapterSet.Built());

        Assert.False(registry.Listed(new DoorId("stub"), configured: false));
        Assert.True(registry.Listed(new DoorId("stub"), configured: true));
        Assert.True(registry.Listed(new DoorId("claude-code"), configured: false));
        Assert.Contains("claude-code", registry.Operands());
        Assert.Contains("codex", registry.Operands());
    }

    /// <summary>The roles' verbs and the brainstorm's provenance read the flag from the registration: a stand-in's doors are stand-ins.</summary>
    [Fact]
    public void The_roles_verbs_and_the_brainstorm_read_the_stand_in_flag_from_the_registration()
    {
        Assert.False(ActorPress.Managed("stub"));
        Assert.False(ActorPress.Managed("acp-stub"));
        Assert.True(ActorPress.Managed("claude-code"));
        Assert.True(ActorPress.Managed("codex"));
        Assert.False(ActorPress.Managed("acme"));

        Assert.True(new BrainstormBy("g1", "stub", "stub", null).Stub);
        Assert.True(new BrainstormBy("g1", "stub", "acp-stub", null).Stub);
        Assert.False(new BrainstormBy("g1", "anthropic", "claude-code", null).Stub);
        Assert.False(new BrainstormBy("g1", "anthropic", "claude-code-acp", null).Stub);
    }

    /// <summary>A provider built is reserved by registering: every provider and door id of the registration list, in every read of the reserved names.</summary>
    [Fact]
    public void Every_built_provider_and_door_id_is_reserved()
    {
        var reserved = PluginCatalog.Reserved;
        foreach (var provider in Providers.Built())
        {
            Assert.Contains(provider.Id.Value, reserved);
            Assert.All(provider.Doors, door => Assert.Contains(door.Id.Value, reserved));
        }

        Assert.Contains(reserved, name => name == "claude-code");
    }

    /// <summary>A provider made from a plugin's own harness name, or for an <c>accountOf</c> like <c>acme</c>, is reserved by nothing.</summary>
    [Fact]
    public void A_made_provider_s_id_is_never_reserved()
    {
        var harness = new PluginHarness("acme-agent", ["acme"], AccountOf: "acme");
        var set = AdapterSet.Built().WithPlugins(new SingleDoor(new DeclaredAcpAdapter(harness, "acme-plugin")));
        var registry = ProviderRegistry.Of(set);

        Assert.Contains(registry.Providers, provider => provider.Id.Value == "acme" && provider.Identity.DeclaredBy.Count > 0);
        Assert.DoesNotContain("acme", registry.Reserved);
        Assert.DoesNotContain("acme-agent", registry.Reserved);
        Assert.DoesNotContain("acme", PluginCatalog.Reserved);
        Assert.Contains("stub", registry.Reserved);
        Assert.Contains("acp-stub", registry.Reserved);
    }

    private sealed class SingleDoor(ISessionAdapter adapter) : IAgentPlugins
    {
        public IEnumerable<ISessionAdapter> Adapters { get; } = [adapter];

        public IEnumerable<string> DeclaredAgents { get; } = [adapter.Name];

        public (string? Folder, IReadOnlyList<string> Plugins) WriteSkills(string home, string sessionId, string to) => (null, []);
    }
}
