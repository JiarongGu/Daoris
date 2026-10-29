using System.Reflection;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row a (D213; <c>docs/2026-10-26-agent-provider-design.md</c> §2, §5, §11): the provider contract named as records
/// and the registry over today's adapters answering every question the contract asks, with no behaviour changed. Nothing reads
/// either but these tests, which hold the registry equal to every copy of "own, else owner" the code makes today.
/// </summary>
public sealed class ProviderRegistryTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-provider-registry-tests", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static readonly string[] ProviderParts =
    [
        "Id", "Identity", "Doors", "Program", "Accounts", "Tables", "Usage", "Lent", "Models", "Children", "Rules", "Skills",
        "Conversations", "Reach", "Vocabulary", "Unavailable",
    ];

    private static readonly string[] DoorParts =
    [
        "Id", "Transport", "Identity", "Program", "Home", "Lent", "Offers", "Postures", "Spawn", "Dialect", "Abilities", "Tables", "AccountOf",
    ];

    /// <summary>§2.3: every part is required, so each is a member of the record, and the records are sealed.</summary>
    [Fact]
    public void The_provider_and_the_door_are_sealed_records_holding_every_part_of_the_contract()
    {
        Assert.True(typeof(Provider).IsSealed);
        Assert.True(typeof(AgentDoor).IsSealed);
        Assert.Equal(ProviderParts.Order(), Members(typeof(Provider)).Order());
        Assert.Equal(DoorParts.Order(), Members(typeof(AgentDoor)).Order());
    }

    private static IEnumerable<string> Members(Type record) => record
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.Name)
        .Where(name => name != "EqualityContract");

    /// <summary>§2.2: a provider's id and a door's are typed, so one passed where the other is wanted does not compile.</summary>
    [Fact]
    public void The_ids_are_typed_and_convert_to_no_other_kind()
    {
        Type[] kinds = [typeof(ProviderId), typeof(DoorId), typeof(AccountKey)];
        foreach (var kind in kinds)
        {
            var conversions = kind.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name is "op_Implicit" or "op_Explicit");
            Assert.Empty(conversions);
        }

        Assert.NotEqual<object>(new ProviderId("claude-code"), new DoorId("claude-code"));
        Assert.NotEqual<object>(new DoorId("codex"), new AccountKey("codex"));
    }

    /// <summary>A name is the same name in any case, as the adapters' own table treats it, and says what it is in the one way.</summary>
    [Fact]
    public void An_id_compares_without_case_and_is_never_blank()
    {
        Assert.Equal(new ProviderId("Claude-Code"), new ProviderId("claude-code"));
        Assert.Equal(new DoorId("Claude-Code").GetHashCode(), new DoorId("claude-code").GetHashCode());
        Assert.Equal(new AccountKey("Codex"), new AccountKey("codex"));
        Assert.Equal("claude-code", new DoorId("claude-code").ToString());
        Assert.Throws<ArgumentException>(() => new ProviderId(" "));
        Assert.Throws<ArgumentException>(() => new DoorId(""));
        Assert.Throws<ArgumentException>(() => new AccountKey(null!));
    }

    /// <summary>§2.4: a fact is known with its source and where it applies, or it is stated absent; absent is never a value read.</summary>
    [Fact]
    public void A_fact_names_its_source_its_evidence_and_where_it_applies_or_is_stated_absent()
    {
        var declared = Known<bool>.Declared(true, "the door's declaration");
        Assert.True(declared.IsKnown);
        Assert.True(declared.Value);
        Assert.Equal(KnownBy.Declared, declared.By);
        Assert.Equal("the door's declaration", declared.Evidence);
        Assert.Equal(Known<bool>.Anywhere, declared.Applies);

        var negotiated = Known<string>.Negotiated("plan", "initialize answered", "claude-code-acp 0.79.0");
        Assert.Equal(KnownBy.Negotiated, negotiated.By);
        Assert.Equal("claude-code-acp 0.79.0", negotiated.Applies);
        var measured = Known<int>.Measured(3, "a probe on the install", "posture asking");
        Assert.Equal(KnownBy.Measured, measured.By);

        var absent = Known<string>.Absent;
        Assert.False(absent.IsKnown);
        Assert.False(absent.TryGet(out _));
        Assert.Throws<InvalidOperationException>(() => absent.Required);
        Assert.NotEqual(Known<bool>.Absent, Known<bool>.Declared(false, "declared false"));
    }

    [Fact]
    public void A_transport_is_the_wire_the_adapter_declares()
    {
        Assert.Equal(Transport.Native, TransportOf.Wire(SessionWire.Pipe));
        Assert.Equal(Transport.Protocol, TransportOf.Wire(SessionWire.Acp));
    }

    /// <summary>§5: a key resolves to its pool, the key and the providers holding it, and a pool that holds none is no pool.</summary>
    [Fact]
    public void An_account_pool_holds_its_key_and_every_provider_holding_it()
    {
        var pool = new AccountPool(new AccountKey("claude-code-acp"), [new ProviderId("foo"), new ProviderId("baz")], [new DoorId("foo")]);

        Assert.Equal(new AccountKey("claude-code-acp"), pool.Key);
        Assert.Equal(2, pool.Providers.Count);
        Assert.Throws<ArgumentException>(() => new AccountPool(new AccountKey("x"), [], []));
    }

    /// <summary>§5's table: each field a record keeps, with the kind of id it holds, so no reader guesses.</summary>
    [Fact]
    public void The_records_table_names_the_kind_of_id_each_field_holds()
    {
        Assert.NotEmpty(RecordIds.Fields);
        Assert.All(RecordIds.Fields, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Record));
            Assert.False(string.IsNullOrWhiteSpace(row.Field));
        });
        Assert.Equal(RecordIds.Fields.Count, RecordIds.Fields.Select(row => (row.Record, row.Field)).Distinct().Count());
        foreach (var kind in Enum.GetValues<IdKind>())
        {
            Assert.Contains(RecordIds.Fields, row => row.Kind == kind);
        }

        Assert.Equal(IdKind.Door, RecordIds.KindOf("The session record", "Adapter"));
        Assert.Equal(IdKind.AccountKey, RecordIds.KindOf("The account homes, cooling, windows", "profiles/agents/<agent>/"));
        Assert.Equal(IdKind.AccountKey, RecordIds.KindOf("SessionEnded, the usage history", "TurnBinding.Agent"));
        Assert.Equal(IdKind.Provider, RecordIds.KindOf("The lineage", "harness"));
        Assert.Throws<DriverException>(() => RecordIds.KindOf("The session record", "NoSuchField"));
    }

    // ---- The registry over today's adapters, held equal to every copy of "own, else owner" ----

    private static JsonElement Table() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "provider-owner.json"))).RootElement.Clone();

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var each in Table().GetProperty("cases").EnumerateArray()) data.Add(each.GetProperty("name").GetString()!);
        return data;
    }

    private static JsonElement Case(string name) =>
        Table().GetProperty("cases").EnumerateArray().Single(each => each.GetProperty("name").GetString() == name);

    /// <summary>Writes the case's plugins under the home, as a person's would be, and joins the enabled ones to the built set.</summary>
    private AdapterSet Build(JsonElement plugins)
    {
        foreach (var plugin in plugins.EnumerateArray())
        {
            var id = plugin.GetProperty("id").GetString()!;
            var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "plugin.json"),
                $$"""{ "id": "{{id}}", "harnesses": {{plugin.GetProperty("harnesses").GetRawText()}} }""");
            if (plugin.TryGetProperty("disabled", out var off) && off.GetBoolean()) PluginState.Disable(_home, id);
        }

        var built = AdapterSet.Built();
        return built.WithPlugins(AgentPluginReads.Current.Load(_home, built.Reserved));
    }

    /// <summary>Everything the copies answer for one door, as a value to hold equal.</summary>
    private sealed record Answer(
        string Owner, string FamilyOwner, string AccountAgent, string ReportingDoor, bool Limits, bool SignIn, bool Windows, bool Usage,
        bool Login, string? KeyVariable, string? SettingsFile, string? HomeVariable, bool OwnHome, bool StartsInOwnHome, string? TrustFile);

    /// <summary>Today's answers: <c>Toolchain.Owner</c>, <c>AgentFamily</c> and the roster's readers, each read where the code reads it.</summary>
    private static Answer Today(AdapterSet set, HarnessRoster roster, string door)
    {
        var toolchain = set.Resolve(door).Toolchain!;
        var agent = roster.AccountAgentOf(door);
        // HarnessRoster.ReportingDoor (private): the account agent where it is a door of this set, else the door itself.
        var reporting = agent is { } found && set.Names.Contains(found.Name, StringComparer.OrdinalIgnoreCase) ? found.Name : door;
        return new Answer(
            toolchain.Owner(door), AgentFamily.Of(set, door).Owner, agent!.Value.Name, reporting,
            roster.LimitsOf(door) is not null, roster.SignInOf(door) is not null, roster.WindowsOf(door) is not null,
            roster.UsageOf(door) is not null, agent.Value.Toolchain.LoginCheck is not null, agent.Value.Toolchain.KeyVariable,
            roster.AccountToolchain(door)?.SettingsFile, toolchain.ProfileVariable, toolchain.RunsInOwnHome, toolchain.StartsInOwnHome,
            toolchain.TrustFile);
    }

    private static Answer Registered(ProviderRegistry registry, string door)
    {
        var id = new DoorId(door);
        var home = registry.Door(id).Home;
        var agent = registry.AccountAgentOf(id)!;
        return new Answer(
            registry.OwnerOf(id).Value, registry.OwnerOfAgent(door).Value, agent.Name, registry.ReportingDoorOf(id).Value,
            registry.LimitsOf(id) is not null, registry.SignInOf(id) is not null, registry.WindowsOf(id) is not null,
            registry.UsageOf(id) is not null, agent.Accounts.Login is not null, agent.Accounts.KeyVariable,
            registry.SettingsFileOf(door), home.Variable, home.OwnHome, home.StartsInOwnHome, home.TrustFile);
    }

    private static Answer Written(JsonElement door)
    {
        string? Text(string name) => door.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        bool Flag(string name) => door.TryGetProperty(name, out var value) && value.GetBoolean();
        var owner = Text("owner")!;
        var variable = Text("homeVariable");
        return new Answer(
            owner, Text("familyOwner") ?? owner, Text("accountAgent")!, Text("reportingDoor")!, Flag("limits"), Flag("signIn"),
            Flag("windows"), Flag("usage"), Flag("login"), Text("keyVariable"), Text("settingsFile"), variable, Flag("ownHome"),
            Flag("ownHome") || variable is null, Text("trustFile"));
    }

    /// <summary>
    /// The page's question of whether a start may run in its own home reads the toolchain of the account owner's first door
    /// (<c>HarnessRoster.OwnersToolchain</c>, private, copied here), where the walk reads the door's own: the cells where two copies
    /// answer differently today.
    /// </summary>
    private static List<string> Disagreeing(AdapterSet set)
    {
        HarnessToolchain? Owners(string owner)
        {
            HarnessToolchain? door = null;
            foreach (var name in set.Names)
            {
                var toolchain = set.Resolve(name).Toolchain;
                if (toolchain is null || !string.Equals(toolchain.Owner(name), owner, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, owner, StringComparison.OrdinalIgnoreCase)) return toolchain;
                door ??= toolchain;
            }

            return door ?? set.Holder(owner);
        }

        return [.. set.Names.Where(name =>
            set.Resolve(name).Toolchain is { } own && own.StartsInOwnHome != Owners(own.Owner(name))!.StartsInOwnHome)];
    }

    /// <summary>
    /// Row a's proof (the design §11): over the built set and each plugin case, the registry answers for every door the account key
    /// <c>Toolchain.Owner</c> gives, and every fact the copies answer, equal to today's, to the fixture's written table, and to
    /// each other.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void The_registry_answers_every_door_as_the_copies_of_own_else_owner_do_today(string name)
    {
        var table = Case(name);
        var set = Build(table.GetProperty("plugins"));
        var registry = ProviderRegistry.Of(set);
        var roster = new HarnessRoster(set, DaorisHome.Entry(_home, HomeEntry.Harnesses));

        foreach (var door in set.Names)
        {
            var today = Today(set, roster, door);
            Assert.Equal(today, Registered(registry, door));
            Assert.Equal(roster.LimitsOf(door), registry.LimitsOf(new DoorId(door)));
            Assert.Equal(roster.SignInOf(door), registry.SignInOf(new DoorId(door)));
            Assert.Equal(roster.WindowsOf(door), registry.WindowsOf(new DoorId(door)));
            Assert.Equal(roster.UsageOf(door), registry.UsageOf(new DoorId(door)));
        }

        // The written table: each door it lists, by what the copies say, by what it says, and which provider holds the door.
        foreach (var listed in table.GetProperty("doors").EnumerateObject())
        {
            var expected = Written(listed.Value);
            Assert.Equal(expected, Today(set, roster, listed.Name));
            Assert.Equal(expected, Registered(registry, listed.Name));
            Assert.Equal(listed.Value.GetProperty("provider").GetString(), registry.ProviderOf(new DoorId(listed.Name)).Value);
        }

        Assert.Equal(Strings(table, "disagrees"), Disagreeing(set).Order(StringComparer.Ordinal));
        Assert.Equal(Strings(table, "unavailable"), registry.Providers.Where(p => p.Unavailable).Select(p => p.Id.Value));
    }

    private static IEnumerable<string> Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(each => each.GetString()!);

    /// <summary>The agents a person or a record names, door or not: the family's owner and the settings file each is read through.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void The_registry_answers_each_agent_as_the_family_and_the_verbs_read_it(string name)
    {
        var table = Case(name);
        var set = Build(table.GetProperty("plugins"));
        var registry = ProviderRegistry.Of(set);
        var roster = new HarnessRoster(set, DaorisHome.Entry(_home, HomeEntry.Harnesses));

        // `AgentsCommand` reads a door's settings file through its account agent and any other agent's own toolchain.
        string? Settings(string agent) => set.Names.Contains(agent, StringComparer.OrdinalIgnoreCase)
            ? roster.AccountToolchain(agent)?.SettingsFile
            : set.Holder(agent)?.SettingsFile;
        if (table.TryGetProperty("agents", out var agents))
        {
            foreach (var listed in agents.EnumerateObject())
            {
                var owner = listed.Value.GetProperty("owner").GetString();
                var file = listed.Value.GetProperty("settingsFile").GetString();
                Assert.Equal(owner, AgentFamily.Of(set, listed.Name).Owner);
                Assert.Equal(owner, registry.OwnerOfAgent(listed.Name).Value);
                Assert.Equal(file, Settings(listed.Name));
                Assert.Equal(file, registry.SettingsFileOf(listed.Name));
            }
        }

        foreach (var agent in registry.Known)
        {
            Assert.Equal(AgentFamily.Of(set, agent).Owner, registry.OwnerOfAgent(agent).Value);
        }
    }

    /// <summary>§5: a key resolves to its pool, the providers holding it and none selected first, never to the door the key spells.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void An_account_key_resolves_to_its_pool_and_every_account_fact_read_through_it_is_todays(string name)
    {
        var table = Case(name);
        var set = Build(table.GetProperty("plugins"));
        var registry = ProviderRegistry.Of(set);

        foreach (var listed in table.GetProperty("pools").EnumerateObject())
        {
            var pool = registry.Resolve(listed.Name, IdKind.AccountKey).Pool!;
            Assert.Equal(new AccountKey(listed.Name), pool.Key);
            Assert.Equal(Strings(listed.Value, "providers"), pool.Providers.Select(id => id.Value));
            Assert.Equal(Strings(listed.Value, "doors"), pool.Doors.Select(id => id.Value));

            // Every door in the pool reads the key it was grouped by, as today's `Owner` gives it.
            foreach (var door in pool.Doors) Assert.Equal(listed.Name, registry.OwnerOf(door).Value, ignoreCase: true);
        }
    }

    /// <summary>
    /// The known agents are the build's and every installed plugin's, a disabled one's too (POOLSTRAY1): the registry's equal
    /// <c>AdapterSet.Agents</c>, which is <c>HarnessSettings.AgentsKnown</c>, for each case, with the disabled plugin's names among them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void The_known_agents_equal_the_sets_and_the_settings(string name)
    {
        var set = Build(Case(name).GetProperty("plugins"));
        var registry = ProviderRegistry.Of(set);

        Assert.Equal(set.Agents, registry.Known);
        Assert.Equal(HarnessSettings.AgentsKnown(_home), registry.Known);
        Assert.Equal(AdapterSet.Built().Reserved, registry.Reserved);
    }

    /// <summary>Mirrors <c>AccountSetTests</c>' known agents: a disabled plugin's <c>acme-agent</c> and <c>acme</c> are among them.</summary>
    [Fact]
    public void A_disabled_plugins_names_stay_known_as_unavailable_descriptors_keeping_their_ids()
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, "acme.agent");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"], "accountOf": "acme" } ] }""");
        PluginState.Disable(_home, "acme.agent");
        var built = AdapterSet.Built();
        var registry = ProviderRegistry.Of(built.WithPlugins(AgentPluginReads.Current.Load(_home, built.Names)));

        Assert.Equal(HarnessSettings.AgentsKnown(_home), registry.Known);
        Assert.Contains("claude-code", registry.Known);
        Assert.Contains("codex", registry.Known);
        Assert.Contains("acme-agent", registry.Known);
        Assert.Contains("acme", registry.Known);
        foreach (var id in new[] { "acme", "acme-agent" })
        {
            var descriptor = registry.Provider(id);
            Assert.True(descriptor.Unavailable);
            Assert.Empty(descriptor.Doors);
            Assert.Throws<DriverException>(() => registry.Door(id));
        }
    }

    /// <summary>§2.2: an unknown id is D23's error, the one <c>AdapterSet.Resolve</c> gives; a provider's id where a door is asked is that same error.</summary>
    [Fact]
    public void An_unknown_id_is_the_error_naming_what_exists_and_a_provider_is_no_door()
    {
        var set = AdapterSet.Built();
        var registry = ProviderRegistry.Of(set);

        foreach (var asked in new[] { "nope", "codex" })
        {
            var today = Assert.Throws<DriverException>(() => set.Resolve(asked));
            Assert.Equal(today.Message, Assert.Throws<DriverException>(() => registry.Door(asked)).Message);
            Assert.Equal(today.Message, Assert.Throws<DriverException>(() => registry.Resolve(asked, IdKind.Door)).Message);
        }

        var provider = Assert.Throws<DriverException>(() => registry.Provider("nope"));
        Assert.Contains("unknown agent 'nope'", provider.Message);
        Assert.Contains("claude-code", provider.Message);
        Assert.Contains("An agent is added deliberately, never guessed.", provider.Message);
        Assert.Throws<DriverException>(() => registry.Resolve("nope", IdKind.AccountKey));
        Assert.Throws<DriverException>(() => registry.Resolve("nope", IdKind.Program));
        Assert.Throws<DriverException>(() => registry.ProviderOf(new DoorId("codex")));
    }

    /// <summary>§5: every kind of id resolves by its kind; <c>codex</c> resolves as a provider and is refused as a door.</summary>
    [Fact]
    public void Each_kind_of_id_resolves_by_its_kind_and_codex_is_a_provider_never_a_door()
    {
        var registry = ProviderRegistry.Of(AdapterSet.Built());

        Assert.Equal(new DoorId("claude-code-acp"), registry.Resolve("claude-code-acp", IdKind.Door).Door!.Id);
        Assert.Equal(new ProviderId("claude-code"), registry.Resolve("claude-code", IdKind.Provider).Provider!.Id);
        Assert.Equal(new AccountKey("claude-code"), registry.Resolve("claude-code", IdKind.AccountKey).Pool!.Key);
        Assert.Equal(new ProviderId("claude-code"), registry.Resolve("claude-code", IdKind.Program).Provider!.Id);
        Assert.Equal(new DoorId("claude-code-acp"), registry.Resolve("claude-code-acp", IdKind.Program).Door!.Id);

        var codex = registry.Resolve("codex", IdKind.Provider).Provider!;
        Assert.Equal([new DoorId("codex-acp")], codex.Doors.Select(door => door.Id));
        Assert.Equal(new AccountKey("codex"), registry.Resolve("codex", IdKind.AccountKey).Pool!.Key);
        Assert.Throws<DriverException>(() => registry.Resolve("codex", IdKind.Door));

        // The door of the provider whose key is its id and the door of a protocol door the key names are not the same pool member.
        Assert.Throws<DriverException>(() => registry.Resolve("claude-code-acp", IdKind.AccountKey));
        Assert.Throws<DriverException>(() => registry.Resolve("claude-code-acp", IdKind.Provider));
    }

    /// <summary>Each provider's parts, as today's toolchains state them: the contract's records hold what the code declares.</summary>
    [Fact]
    public void The_providers_and_doors_hold_what_the_toolchains_declare()
    {
        var set = AdapterSet.Built();
        var registry = ProviderRegistry.Of(set);

        Assert.Equal(["claude-code", "codex", "dsh", "stub"], registry.Providers.Select(p => p.Id.Value));
        Assert.All(registry.Providers, provider => Assert.Equal(Reach.Everywhere, provider.Reach));
        Assert.All(registry.Providers, provider => Assert.False(provider.Unavailable));
        Assert.Equal(["stub"], registry.Providers.Where(p => p.Identity.StandIn).Select(p => p.Id.Value));

        var claude = registry.Provider("claude-code");
        Assert.Equal(["claude-code", "claude-code-acp"], claude.Doors.Select(door => door.Id.Value));
        Assert.Equal(("Claude Code", "Anthropic", "CC", false), (claude.Identity.Product, claude.Identity.Maker, claude.Identity.Mark, claude.Identity.StandIn));
        Assert.Empty(claude.Identity.DeclaredBy);
        Assert.Equal(["claude"], claude.Program.Required.Binary);
        Assert.Equal(new AccountKey("claude-code"), claude.Accounts.Required.Key);
        Assert.Equal("ANTHROPIC_API_KEY", claude.Accounts.Required.Facts.KeyVariable);
        Assert.Same(set.Resolve("claude-code").Toolchain!.Limits, claude.Tables.Required.Limits);
        Assert.NotNull(claude.Tables.Required.ErrorWords);
        Assert.Same(set.Resolve("claude-code").Toolchain!.Usage, claude.Usage.Required.Question);
        Assert.Equal(Transport.Native, claude.Doors[0].Transport);
        Assert.Equal(Transport.Protocol, claude.Doors[1].Transport);
        Assert.All(claude.Doors, door => Assert.Equal(new DoorIdentity(null, null, null), door.Identity));

        // The pool's own door states its home as the door's own, never the provider's (O2-F2): the protocol door repeats both.
        var acp = registry.Door("claude-code-acp");
        Assert.Equal(new DoorHome("CLAUDE_CONFIG_DIR", false, ".claude.json", false), acp.Home);
        Assert.Equal(new DoorHome("CODEX_HOME", false, null, true), registry.Door("codex-acp").Home);
        Assert.Equal("claude-agent-acp", acp.Program.Required.Binary.Single());
        Assert.False(registry.Door("claude-code").Program.IsKnown);
        Assert.NotNull(acp.Tables.ErrorWords);
        Assert.Null(registry.Door("claude-code").Tables.ErrorWords);

        // The abilities are each door's declaration, never the transport's: dsh is a protocol door that is not interactive.
        Assert.True(registry.Door("claude-code-acp").Abilities.Interactive.Required);
        Assert.False(registry.Door("dsh").Abilities.Interactive.Required);
        Assert.Equal(Transport.Protocol, registry.Door("dsh").Transport);
        Assert.True(registry.Door("claude-code").Abilities.Interactive.Required);
        var door = set.Resolve("claude-code-acp");
        Assert.Equal((door.AcpPosture, door.AskingPosture, door.FullPosture), (
            registry.Door("claude-code-acp").Postures.Ordinary, registry.Door("claude-code-acp").Postures.Asking,
            registry.Door("claude-code-acp").Postures.Full));
        Assert.NotNull(registry.Door("claude-code-acp").Postures.Ordinary);
    }

    /// <summary>§2.3's <c>Lent</c>: what a harness naming this id reads of it, equal to what the roster reads of an owner today (O4-F1).</summary>
    [Fact]
    public void Each_lent_part_is_what_a_harness_naming_that_id_reads_today()
    {
        var set = AdapterSet.Built();
        var registry = ProviderRegistry.Of(set);
        var claude = set.Resolve("claude-code").Toolchain!;
        var stub = set.Resolve("stub").Toolchain!;
        var codexAcp = set.Resolve("codex-acp").Toolchain!;

        // claude-code, stub and dsh lend all of their pool, tables and usage question.
        var lent = registry.Provider("claude-code").Lent.Required;
        Assert.Same(claude.Limits, lent.Tables.Limits);
        Assert.Same(claude.Windows, lent.Tables.Windows);
        Assert.Same(claude.SignIn, lent.Tables.SignIn);
        Assert.Same(claude.Usage, lent.Usage.Question);
        Assert.Equal(claude.LoginCheck!.LoggedIn, lent.Accounts.Login!.LoggedIn);
        Assert.Same(stub.Limits, registry.Provider("stub").Lent.Required.Tables.Limits);
        Assert.NotNull(registry.Provider("dsh").Lent.Required);

        // codex lends its holder's sign-in alone: no words and no usage question.
        var holder = registry.Provider("codex").Lent.Required;
        Assert.Same(CodexAccounts.Toolchain.LoginCheck, holder.Accounts.Login);
        Assert.Equal(CodexAccounts.Toolchain.DeviceLoginArguments, holder.Accounts.DeviceLoginArguments);
        Assert.Equal(CodexAccounts.Toolchain.LoginPorts, holder.Accounts.LoginPorts);
        Assert.Equal(TablesPart.None, holder.Tables);
        Assert.Null(holder.Usage.Question);

        // codex-acp lends Codex's limit words and usage question; claude-code-acp and acp-stub lend none.
        Assert.Same(codexAcp.Limits, registry.Door("codex-acp").Lent.Tables.Limits);
        Assert.Same(codexAcp.Usage, registry.Door("codex-acp").Lent.Usage.Question);
        foreach (var none in new[] { "claude-code-acp", "acp-stub" })
        {
            var door = registry.Door(none).Lent;
            Assert.Null(door.Tables.Limits);
            Assert.Null(door.Usage.Question);
            Assert.Null(door.Accounts.Login);
        }
    }

    /// <summary>
    /// A door made from a plugin keeps its own identity and home as it declares them (C1-2.2, O2-F2): its plugin as <c>DeclaredBy</c>,
    /// its own variable and no trust file, and the interactive ability the factory states for every declared protocol door.
    /// </summary>
    [Fact]
    public void A_plugins_door_keeps_its_own_identity_and_home_whatever_provider_it_joins()
    {
        var set = Build(Case("joins a registered provider whose key is that string").GetProperty("plugins"));
        var registry = ProviderRegistry.Of(set);

        var joined = registry.Door("p-claude");
        Assert.Equal(new ProviderId("claude-code"), registry.ProviderOf(joined.Id));
        Assert.Equal("p.claude", joined.Identity.DeclaredBy);
        Assert.Equal(new DoorHome("P_HOME", false, null, false), joined.Home);
        Assert.Equal(Transport.Protocol, joined.Transport);
        Assert.True(joined.Abilities.Interactive.Required);
        Assert.Equal(new AccountKey("claude-code"), joined.AccountOf);
        Assert.Equal(["p.claude"], registry.Provider("claude-code").Identity.DeclaredBy);
        Assert.Equal(["p.claude", "p.others"], registry.Providers.SelectMany(p => p.Identity.DeclaredBy).Distinct().Order(StringComparer.Ordinal));

        // A door declaring neither variable nor ownHome starts in the tool's own home, as `StartsInOwnHome` reads today.
        Assert.Null(registry.Door("p-claude-bare").Home.Variable);
        Assert.True(registry.Door("p-claude-bare").Home.StartsInOwnHome);
        Assert.True(registry.Door("p-dsh").Home.OwnHome);
        Assert.False(joined.Home.StartsInOwnHome);
    }

    /// <summary>
    /// A made provider's account facts are the named harness's own declarations, referenced by name (O3-F2): <c>codex-acp</c>'s limit
    /// words and usage question for the plugin naming it, none for the one naming <c>claude-code-acp</c>.
    /// </summary>
    [Fact]
    public void A_provider_made_for_a_door_named_account_of_references_that_doors_declarations_by_name()
    {
        var named = ProviderRegistry.Of(Build(Case("names codex-acp and reads its limit words and usage question").GetProperty("plugins")));
        var made = named.Provider("p-cx");

        Assert.Equal(new AccountKey("codex-acp"), made.Accounts.Required.Key);
        Assert.Equal(new DoorId("codex-acp"), made.Accounts.Required.References);
        Assert.Same(AdapterSet.Built().Resolve("codex-acp").Toolchain!.Limits, made.Tables.Required.Limits);
        Assert.Same(AdapterSet.Built().Resolve("codex-acp").Toolchain!.Usage, made.Usage.Required.Question);
        Assert.False(made.Lent.Required.Tables.Limits is not null);
        Assert.Equal(["p-cx"], named.Resolve("codex-acp", IdKind.AccountKey).Pool!.Providers.Select(id => id.Value));
        Assert.Equal(["codex-acp"], named.Provider("codex").Doors.Select(door => door.Id.Value));
        Assert.Equal(new AccountKey("codex-acp"), named.OwnerOf(new DoorId("p-cx")));
        Assert.Equal(new ProviderId("p-cx"), named.ProviderOf(new DoorId("p-cx")));
    }

    // ---- Registration refuses what the identity rules forbid (the design §2.2) ----

    private static AgentDoor FakeDoor(string id) => new(
        new DoorId(id), Transport.Native, new DoorIdentity(null, null, null), Known<ProgramFacts>.Absent,
        new DoorHome(null, false, null, false), LentPart.None, Known<PortRef>.Absent, new PosturesPart(null, null, null),
        Known<PortRef>.Absent, Known<PortRef>.Absent,
        new AbilitiesPart(Known<bool>.Absent, Known<bool>.Absent, Known<bool>.Absent), new DoorTablesPart(null, null), null);

    private static Provider FakeProvider(string id, bool unavailable = false, params string[] doors) => new(
        new ProviderId(id), new ProviderIdentity(null, null, null, false, []), [.. doors.Select(FakeDoor)], Known<ProgramFacts>.Absent,
        Known<AccountsPart>.Absent, Known<TablesPart>.Absent, Known<UsagePart>.Absent, Known<LentPart>.Absent, Known<PortRef>.Absent,
        Known<PortRef>.Absent, Known<PortRef>.Absent, Known<PortRef>.Absent, Known<PortRef>.Absent, Reach.Everywhere,
        Known<VocabularyPart>.Absent, unavailable);

    [Fact]
    public void Registration_refuses_a_provider_named_with_another_providers_door_s_id()
    {
        // A door may take its own provider's id, never another's.
        _ = new ProviderRegistry([FakeProvider("claude-code", false, "claude-code", "claude-code-acp")], []);

        var error = Assert.Throws<DriverException>(() =>
            new ProviderRegistry([FakeProvider("a", false, "a"), FakeProvider("b", false, "b", "a2"), FakeProvider("a2", false, "c")], []));
        Assert.Contains("door 'a2' of provider 'b' is also the id of provider 'a2'", error.Message);
    }

    [Fact]
    public void Registration_refuses_a_door_named_with_another_doors_id_and_a_provider_registered_twice()
    {
        var door = Assert.Throws<DriverException>(() =>
            new ProviderRegistry([FakeProvider("a", false, "x"), FakeProvider("b", false, "X")], []));
        Assert.Contains("door 'X' is named twice", door.Message);

        var provider = Assert.Throws<DriverException>(() =>
            new ProviderRegistry([FakeProvider("a", false, "a"), FakeProvider("A", false, "a2")], []));
        Assert.Contains("provider 'A' is registered twice", provider.Message);
    }

    [Fact]
    public void Registration_refuses_a_provider_with_no_door_unless_it_is_an_unavailable_descriptor_with_none()
    {
        Assert.Contains("has no door", Assert.Throws<DriverException>(() => new ProviderRegistry([FakeProvider("a")], [])).Message);
        Assert.Contains(
            "unavailable descriptor and carries doors",
            Assert.Throws<DriverException>(() => new ProviderRegistry([FakeProvider("a", true, "a")], [])).Message);

        var registry = new ProviderRegistry([FakeProvider("acme", true)], []);
        Assert.True(registry.Provider("acme").Unavailable);
        Assert.Contains("acme", registry.Known);
    }
}
