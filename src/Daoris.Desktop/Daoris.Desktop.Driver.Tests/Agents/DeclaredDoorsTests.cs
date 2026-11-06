using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row q (the agent-provider design §4.8): a plugin's manifest is converted to a <see cref="DeclaredDoor"/> and Agents' factory
/// makes it a contract door on the protocol transport, its identity and its home its own, so the family table, the joined door's reads and
/// the manifest's ignored fields are what they were when a class stood for it.
/// </summary>
public sealed class DeclaredDoorsTests : IDisposable
{
    private readonly string _home = ConformanceScratch.New("declared-doors");

    public void Dispose() => ConformanceScratch.Remove(_home);

    private static ProviderRegistry With(params DeclaredDoor[] doors) => ProviderRegistry.Built().WithPlugins(new Declared(doors));

    private static DeclaredDoor Door(string name, string? accountOf = null, string? maker = null, string? profile = null, string plugin = "acme") =>
        new(name, plugin, ["acme-agent", "--acp"], Posture: "edits", ProfileVariable: profile, AccountOf: accountOf, Product: "Acme", Maker: maker);

    [Fact]
    public void A_declaration_is_made_a_protocol_door_with_its_own_identity_home_and_program_and_nothing_else()
    {
        var door = DeclaredDoors.Door(Door("acme-door", maker: "Acme", profile: "ACME_HOME") with { OwnHome = true, Package = "@acme/agent" });

        Assert.Equal(Transport.Protocol, door.Transport);
        Assert.Equal(new DoorIdentity("acme", "Acme", "Acme"), door.Identity);
        Assert.Equal(new DoorHome("ACME_HOME", true, null), door.Home);
        Assert.Equal(["acme-agent"], door.Program.Required.Binary);
        Assert.True(door.Program.Required.ProbeByPresence);
        Assert.Equal("@acme/agent", door.Program.Required.Package);
        Assert.Equal("edits", door.Postures.Ordinary);
        Assert.Null(door.Postures.Full);
        Assert.True(door.Abilities.Interactive.Required);
        Assert.False(door.Abilities.Resumes.IsKnown);
        Assert.False(door.Dialect.IsKnown);
        Assert.Equal(LentPart.None, door.Lent);
        Assert.Equal(new DoorTablesPart(null, null), door.Tables);
    }

    [Fact]
    public void The_declaration_has_no_field_for_what_a_manifest_may_never_claim()
    {
        var fields = typeof(DeclaredDoor).GetProperties().Select(property => property.Name).ToList();
        foreach (var never in new[] { "Steering", "Resumes", "Limits", "Rules", "Skills", "Dialect", "Windows", "SignIn", "ErrorWords", "Usage", "StandIn", "Conversations" })
        {
            Assert.DoesNotContain(never, fields);
        }
    }

    [Fact]
    public void A_manifest_naming_fields_the_declaration_has_no_place_for_is_read_as_before_and_the_door_reads_none_of_it()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "named");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "named", "harnesses": [ { "name": "named-door", "command": ["node"], "steering": true, "resumes": true,
              "limits": [ "x" ], "rules": "claude", "dialect": "native" } ] }
            """);

        var set = ProviderRegistry.Built().WithPlugins(PluginCatalog.Load(_home, ProviderRegistry.Built().Reserved));
        var abilities = set.Door("named-door").Can;

        Assert.Equal("named", set.DeclaredBy("named-door"));
        Assert.True(abilities.Interactive);
        Assert.False(abilities.TakesSettings);
        Assert.Null(set.RuleLanguage(set.Door("named-door")));
        Assert.False(set.Door("named-door").Dialect.IsKnown);
        Assert.Null(set.AgentNamed("named-door")!.Tables.Limits);
    }

    [Fact]
    public void Two_plugin_doors_on_one_provider_each_keep_their_own_identity_and_home()
    {
        var set = With(
            Door("acme-a", accountOf: "claude-code", maker: "Acme", profile: "ACME_A_HOME"),
            Door("acme-b", accountOf: "claude-code", plugin: "other"));
        var registry = set;

        Assert.Equal("claude-code", registry.OwnerOfAgent("acme-a").Value);
        Assert.Equal("claude-code", registry.OwnerOfAgent("acme-b").Value);
        Assert.Equal(("acme", "Acme"), (registry.Door("acme-a").Identity.DeclaredBy, registry.Door("acme-a").Identity.Maker));
        Assert.Equal(("other", null), (registry.Door("acme-b").Identity.DeclaredBy, registry.Door("acme-b").Identity.Maker));
        Assert.Equal("ACME_A_HOME", registry.Door("acme-a").Home.Variable);
        Assert.Null(registry.Door("acme-b").Home.Variable);
        Assert.NotEqual("CLAUDE_CONFIG_DIR", registry.Door("acme-a").Home.Variable);
        Assert.Null(registry.Door("acme-a").Home.TrustFile);
    }

    [Fact]
    public void An_account_of_nothing_registers_makes_a_provider_of_its_own_and_one_naming_another_door_is_keyed_by_it()
    {
        var set = With(Door("acme-agent", accountOf: "acme"), Door("foo"), Door("bar", accountOf: "foo"));
        var registry = set;

        Assert.Contains(registry.Providers, provider => provider.Id.Value == "acme" && provider.Identity.DeclaredBy.Contains("acme"));
        Assert.DoesNotContain("acme", registry.Reserved);
        Assert.Equal("foo", registry.OwnerOfAgent("bar").Value);
        Assert.Equal("acme", registry.OwnerOfAgent("acme-agent").Value);
    }

    [Fact]
    public void A_disabled_plugins_names_are_kept_and_no_door_of_it_is_carried()
    {
        var set = ProviderRegistry.Built().WithPlugins(new Declared([], declaredAgents: ["off-door", "off-owner"]));

        Assert.DoesNotContain("off-door", set.Names);
        Assert.Contains("off-owner", set.Agents);
        Assert.Contains(set.Providers, provider => provider.Unavailable && provider.Id.Value == "off-door");
    }

    [Fact]
    public void A_declared_door_starts_its_command_on_an_open_stdin_and_holds_a_conversation()
    {
        var registry = With(Door("acme-door"));
        var adapter = registry.Door("acme-door");
        var target = new SessionTarget("q1", "Title", "Body", "person", "engine", _home, "http://localhost");

        var start = registry.Start(adapter, target, command: null);
        var chat = registry.Chat(adapter, SpawnRequests.Chat("engine", _home, "http://localhost", null));

        Assert.Equal("acme-agent", Path.GetFileNameWithoutExtension(start.FileName));
        Assert.True(start.RedirectStandardInput);
        Assert.Equal(Transport.Protocol, adapter.Transport);
        Assert.True(adapter.Can.Interactive);
        Assert.NotNull(chat);
    }

    private sealed class Declared(IEnumerable<DeclaredDoor> doors, IEnumerable<string>? declaredAgents = null) : IAgentPlugins
    {
        public IEnumerable<DeclaredDoor> Doors { get; } = doors;

        public IEnumerable<string> DeclaredAgents { get; } = declaredAgents ?? doors.Select(door => door.Name);

        public IReadOnlyList<SkillSource> Skills(string to) => [];
    }
}
