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

    /// <summary>The sets the readers are held over: the built one, and one with a plugin's door of the stand-in's kind.</summary>
    public static TheoryData<string> Sets => new() { "built", "plugin" };

    private static ProviderRegistry Set(string which) => which switch
    {
        "plugin" => ProviderRegistry.Built().WithPlugins(new SingleDoor(PluginDoors.Declare(new PluginHarness("acme-door", ["acme"]), "acme-plugin"))),
        _ => ProviderRegistry.Built(),
    };

    /// <summary>The roster's listing as the code spelled it: skip a door that names no binary and that the home configures no command for.</summary>
    private static List<string> OldListing(ProviderRegistry set, IReadOnlyDictionary<string, IReadOnlyList<string>> commands) => [.. set.Names.Where(name =>
        !(set.AgentNamed(name) is { Binary.Count: 0 } && commands.GetValueOrDefault(name) is not { Count: > 0 }))];

    /// <summary>The verbs' operands as the code spelled them: each door that names a real program and each agent a door runs as.</summary>
    private static List<string> OldOperands(ProviderRegistry set)
    {
        TestAgent? Of(string each) => set.AgentNamed(each);
        // A plugin's door that names a program is an operand as a built one is, so the set's names are read, in the roster's ordinal order.
        return [.. set.Reserved.Concat(set.Names).Distinct(StringComparer.Ordinal).Where(each => Of(each) is { Binary.Count: > 0 }).Order(StringComparer.Ordinal)];
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void The_roster_lists_what_it_listed_with_and_without_a_stand_in_s_command_configured(string which)
    {
        var set = Set(which);
        var registry = set;
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

        var operands = set.Operands();

        Assert.Equal(OldOperands(set), operands);
        Assert.DoesNotContain("stub", operands);
        Assert.DoesNotContain("acp-stub", operands);
    }

    [Fact]
    public void The_stand_ins_name_no_program_so_the_two_rules_differ_only_by_the_command()
    {
        var registry = ProviderRegistry.Built();

        Assert.False(registry.Listed(new DoorId("stub"), configured: false));
        Assert.True(registry.Listed(new DoorId("stub"), configured: true));
        Assert.True(registry.Listed(new DoorId("claude-code"), configured: false));
        Assert.Contains("claude-code", registry.Operands());
        Assert.Contains("codex", registry.Operands());
    }

    /// <summary>The brainstorm's provenance reads the flag from the registration: a stand-in's doors are stand-ins.</summary>
    [Fact]
    public void The_brainstorm_reads_the_stand_in_flag_from_the_registration()
    {
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
        var set = ProviderRegistry.Built().WithPlugins(new SingleDoor(PluginDoors.Declare(harness, "acme-plugin")));
        var registry = set;

        Assert.Contains(registry.Providers, provider => provider.Id.Value == "acme" && provider.Identity.DeclaredBy.Count > 0);
        Assert.DoesNotContain("acme", registry.Reserved);
        Assert.DoesNotContain("acme-agent", registry.Reserved);
        Assert.DoesNotContain("acme", PluginCatalog.Reserved);
        Assert.Contains("stub", registry.Reserved);
        Assert.Contains("acp-stub", registry.Reserved);
    }

    private sealed class SingleDoor(DeclaredDoor door) : IAgentPlugins
    {
        public IEnumerable<DeclaredDoor> Doors { get; } = [door];

        public IEnumerable<string> DeclaredAgents { get; } = [door.Name];

        public IReadOnlyList<SkillSource> Skills(string to) => [];
    }
}
