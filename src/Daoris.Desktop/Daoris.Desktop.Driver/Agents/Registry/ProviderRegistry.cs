namespace Daoris.Driver;

/// <summary>What an id resolved to, by the kind it was asked as (<see cref="ProviderRegistry.Resolve"/>).</summary>
public sealed record ResolvedId(IdKind Kind, AgentDoor? Door, Provider? Provider, AccountPool? Pool);

/// <summary>The agent that answers for a door's accounts, one level (AGT7): its name, its program and its account facts.</summary>
public sealed record AccountAgent(string Name, ToolSpec? Program, PoolFacts Accounts);

/// <summary>
/// Every provider and door this build and its enabled plugins carry, and the questions the contract asks of them (AGENTPROVIDER1 row
/// a; the agent-provider design §2.2, §11). Made over today's adapters (<see cref="Of"/>) and answering, for every door, what the
/// code answers today by reading the owner, its toolchain and its holder in several places: the registry answers each once. Nothing
/// reads it but its tests; no behaviour changes.
/// </summary>
/// <remarks>
/// <para><b>Identity's rules</b> are checked at registration: a provider's id is unique among providers and a door's among doors; a
/// door's id may equal its own provider's, never another provider's; a provider has at least one door, but for an unavailable
/// descriptor, which has none.</para>
/// <para><b>One level.</b> A door reads, beyond its own declarations, what the provider or door its account key names lends
/// (<see cref="LentPart"/>), never what that one reads in turn, as <c>Toolchain.Owner</c> follows none today.</para>
/// </remarks>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, Provider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (AgentDoor Door, Provider Provider)> _doors = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, HarnessToolchain> _toolchains;

    /// <summary>Registers providers, refusing a set that breaks the identity rules (design §2.2).</summary>
    /// <param name="providers">The providers, each with its doors.</param>
    /// <param name="reserved">The ids a plugin may not declare: the built set's (D64's rule 3).</param>
    /// <param name="toolchains">
    /// Each door's toolchain and each holder's, as the set the registry was made over declared them (<see cref="ToolchainOf"/>); none for
    /// a registry made over providers alone.
    /// </param>
    public ProviderRegistry(
        IEnumerable<Provider> providers, IReadOnlyList<string> reserved, IReadOnlyDictionary<string, HarnessToolchain>? toolchains = null)
        : this([.. providers], reserved, toolchains, null)
    {
    }

    /// <summary>
    /// The registry over doors written as adapters (the dictionary of <see cref="ISessionAdapter"/> every test and the plugins' doors
    /// still make): <paramref name="holders"/> the agents a door runs as with no door of that agent here, each by its toolchain
    /// (CODEXACCT1); <paramref name="declared"/> every agent an installed plugin's harness names, enabled or not (POOLSTRAY1). None
    /// by default, so a registry a test builds asks nobody.
    /// </summary>
    public ProviderRegistry(
        IReadOnlyDictionary<string, ISessionAdapter> adapters, IReadOnlyDictionary<string, HarnessToolchain>? holders = null,
        IReadOnlyCollection<string>? declared = null)
        : this(new AdapterTable(adapters, holders, declared))
    {
    }

    private ProviderRegistry(AdapterTable table) : this(ProviderRegistration.Of(table))
    {
    }

    private ProviderRegistry(ProviderRegistration.Made made) : this(made.Providers, made.Reserved, made.Toolchains, made.Table)
    {
    }

    private ProviderRegistry(
        List<Provider> providers, IReadOnlyList<string> reserved, IReadOnlyDictionary<string, HarnessToolchain>? toolchains, AdapterTable? table)
    {
        _table = table ?? OverDoors(providers);
        _toolchains = toolchains ?? new Dictionary<string, HarnessToolchain>();
        foreach (var provider in providers)
        {
            if (!_providers.TryAdd(provider.Id.Value, provider))
            {
                throw new DriverException($"provider '{provider.Id}' is registered twice — a provider's id is unique among providers.");
            }

            if (provider.Unavailable && provider.Doors.Count > 0)
            {
                throw new DriverException($"provider '{provider.Id}' is an unavailable descriptor and carries doors — it is known and never started.");
            }

            // A holder (the agent a door runs as, with no door of that agent here: `codex`) declares its program and has no door; a
            // set of tests' own may carry one with no door onto it at all.
            if (!provider.Unavailable && provider.Doors.Count == 0 && !provider.Program.IsKnown)
            {
                throw new DriverException($"provider '{provider.Id}' has no door — a provider has at least one, else it is an unavailable descriptor or a holder naming its program.");
            }

            foreach (var door in provider.Doors)
            {
                if (!_doors.TryAdd(door.Id.Value, (door, provider)))
                {
                    throw new DriverException($"door '{door.Id}' is named twice — a door's id is unique among doors.");
                }
            }
        }

        foreach (var (door, provider) in _doors.Values)
        {
            if (_providers.TryGetValue(door.Id.Value, out var same) && same != provider)
            {
                throw new DriverException(
                    $"door '{door.Id}' of provider '{provider.Id}' is also the id of provider '{same.Id}' — "
                    + "a door's id may equal its own provider's, never another's.");
            }
        }

        Providers = [.. _providers.Values.OrderBy(provider => provider.Id.Value, StringComparer.Ordinal)];
        Reserved = reserved;
        Known = [.. Providers.SelectMany(ProviderRegistration.Names).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.Ordinal)];
        RegisteredVariables.Register(this);
    }

    /// <summary>
    /// The registry this build carries: every provider registered (<see cref="Providers.Built"/>) with its doors, and the agent
    /// <c>codex-acp</c> runs as, which has no door of its own here (CODEXACCT1): its sign-in and status question. `claude-code` first
    /// and supported, `codex` second and explicit (D23): an unknown name is an error naming what exists, never a silent fallback.
    /// </summary>
    public static ProviderRegistry Built() => new(
        new Dictionary<string, ISessionAdapter>(LegacyDoor.Over(global::Daoris.Driver.Providers.Built()), StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, HarnessToolchain>(StringComparer.OrdinalIgnoreCase) { [CodexAccounts.Agent] = CodexAccounts.Toolchain });

    private readonly AdapterTable _table;

    /// <summary>The table of a registry made over providers alone; a door named twice is left for the identity rules to refuse.</summary>
    private static AdapterTable OverDoors(IEnumerable<Provider> providers)
    {
        var doors = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var each in LegacyDoor.Over(providers)) doors.TryAdd(each.Key, each.Value);
        return new AdapterTable(doors);
    }

    /// <summary>Every door this registry has, by name, in a stable order — what a roster enumerates.</summary>
    public IReadOnlyList<string> Names => _table.Names;

    /// <summary>
    /// The agents whose accounts Daoris keeps (POOLSTRAY1, D197): each name run as a harness, each agent a door runs as, and each an
    /// installed plugin's harness names, a disabled one's too, so turning a plugin off loses none of its wiring. A folder under the
    /// account homes naming none of these is no agent (<see cref="HarnessSettings.AccountsOf"/>).
    /// </summary>
    public IReadOnlyList<string> Agents => _table.Agents;

    /// <summary>The door named <paramref name="name"/> as the old seam's readers ask it; an unknown name is an error naming what exists.</summary>
    public ISessionAdapter Resolve(string name) => _table.Resolve(name);

    /// <summary>
    /// This registry plus <paramref name="more"/> doors and the agents they declare. A name it carries is kept as it is, so nothing can
    /// be replaced: a door joins, and the registry it joins is exactly what it was (<c>WithPlugins</c> says who offers the doors).
    /// </summary>
    public ProviderRegistry Joined(IEnumerable<ISessionAdapter> more, IEnumerable<string> declaredAgents) => new(_table.Joined(more, declaredAgents));

    /// <summary>Every provider, in id order, unavailable descriptors among them.</summary>
    public IReadOnlyList<Provider> Providers { get; }

    /// <summary>The names a plugin may not declare: the built set's doors and the agents its doors run as (D64's rule 3).</summary>
    public IReadOnlyList<string> Reserved { get; }

    /// <summary>The agents whose accounts Daoris keeps (POOLSTRAY1): every provider, door and account key, a disabled plugin's names too.</summary>
    public IReadOnlyList<string> Known { get; }

    /// <summary>Each agent's facts and its doors' sheets, read from the registrations and the measurements table (COAGENT1e).</summary>
    public IAgentFacts Facts => LazyInitializer.EnsureInitialized(ref _facts, () => new ProviderFacts(this, Measurement.Table))!;

    private IAgentFacts? _facts;

    /// <summary>
    /// The toolchain of a door, or of an agent a door runs as with no door of its own here (CODEXACCT1, <c>codex</c>): its binary,
    /// sign-in, status question and account seam, what the roster's probes and the agent verbs hand on. Null for an agent nobody
    /// declared. The one reader of a toolchain by name, until the probes read the contract's parts.
    /// </summary>
    public HarnessToolchain? ToolchainOf(string agent) => _toolchains.TryGetValue(agent, out var toolchain) ? toolchain : null;

    /// <summary>The plugin a door came from (D64), or null for a door this build carries and for a name that is no door.</summary>
    public string? DeclaredBy(string door) => _doors.TryGetValue(door, out var entry) ? entry.Door.Identity.DeclaredBy : null;

    /// <summary>Every door, in id order.</summary>
    public IReadOnlyList<DoorId> Doors => [.. _doors.Values.Select(entry => entry.Door.Id).OrderBy(id => id.Value, StringComparer.Ordinal)];

    /// <summary>The door of this id; an unknown id, a provider's included, is an error naming what exists (D23).</summary>
    public AgentDoor Door(string id) => _doors.TryGetValue(id, out var found) ? found.Door : throw UnknownDoor(id);

    private DriverException UnknownDoor(string id) => new(
        $"unknown adapter '{id}' — one of: {string.Join(", ", _doors.Keys.OrderBy(key => key, StringComparer.Ordinal))}. "
        + "An adapter is added deliberately, never guessed.");

    public AgentDoor Door(DoorId id) => Door(id.Value);

    /// <summary>The provider of this id, an unavailable descriptor too; an unknown id is an error naming what exists (D23).</summary>
    public Provider Provider(string id) =>
        _providers.TryGetValue(id, out var found)
            ? found
            : throw new DriverException(
                $"unknown agent '{id}' — one of: {string.Join(", ", Providers.Select(provider => provider.Id.Value))}. "
                + "An agent is added deliberately, never guessed.");

    public Provider Provider(ProviderId id) => Provider(id.Value);

    /// <summary>The provider a door belongs to.</summary>
    public ProviderId ProviderOf(DoorId door) => Entry(door).Provider.Id;

    /// <summary>
    /// The door's account key (design §2.2): its harness's <c>accountOf</c> verbatim, else its provider's id, never followed further:
    /// today's <c>HarnessToolchain.Owner(name)</c>, and the name itself for a door with no toolchain.
    /// </summary>
    public AccountKey OwnerOf(DoorId door) => Entry(door).Door.AccountOf ?? new AccountKey(door.Value);

    /// <summary>
    /// The owner of the account of an agent a person or a record names, door or not (today's family owner,
    /// <c>AgentFamily.Of(...).Owner</c>): a door's key, a provider's, else the name itself.
    /// </summary>
    public AccountKey OwnerOfAgent(string agent)
    {
        var given = agent.Trim();
        if (_doors.TryGetValue(given, out var door)) return OwnerOf(door.Door.Id);
        return _providers.TryGetValue(given, out var provider) ? provider.KeyOf() : new AccountKey(given);
    }

    /// <summary>What an account key resolves to: the providers holding it and their doors, none selected first (C3-2.2).</summary>
    public AccountPool Pool(AccountKey key)
    {
        var holding = Providers.Where(provider => Ids.Same(provider.KeyOf().Value, key.Value)).ToList();
        if (holding.Count == 0)
        {
            throw new DriverException(
                $"unknown account key '{key}' — one of: {string.Join(", ", Providers.Select(provider => provider.KeyOf().Value).Distinct(StringComparer.OrdinalIgnoreCase))}. "
                + "An agent is added deliberately, never guessed.");
        }

        return new AccountPool(
            new AccountKey(holding[0].KeyOf().Value),
            [.. holding.Select(provider => provider.Id)],
            [.. holding.SelectMany(provider => provider.Doors).Select(door => door.Id)]);
    }

    /// <summary>Resolves an id by the kind of id its field holds (design §5, <see cref="RecordIds"/>); an unknown one is D23's error.</summary>
    public ResolvedId Resolve(string id, IdKind kind) => kind switch
    {
        IdKind.Door => new ResolvedId(kind, Door(id), null, null),
        IdKind.Provider => new ResolvedId(kind, null, Provider(id), null),
        IdKind.AccountKey => new ResolvedId(kind, null, null, Pool(new AccountKey(id))),
        IdKind.Program => _providers.TryGetValue(id, out var provider)
            ? new ResolvedId(kind, null, provider, null)
            : new ResolvedId(kind, Door(id), null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // ---- What a door reads of its accounts: its own declarations, else what its account key's provider or door lends. ----

    /// <summary>The words this door says an account's limit in: its own, else its owner's (AGT7, <c>HarnessRoster.LimitsOf</c>).</summary>
    public LimitWords? LimitsOf(DoorId door) => Own(door).Tables.Limits ?? LentTo(door).Tables.Limits;

    /// <summary>The words this door's agent refuses a start for its sign-in in (<c>HarnessRoster.SignInOf</c>).</summary>
    public SignInWords? SignInOf(DoorId door) => Own(door).Tables.SignIn ?? LentTo(door).Tables.SignIn;

    /// <summary>How this door says an account's windows (<c>HarnessRoster.WindowsOf</c>).</summary>
    public WindowWords? WindowsOf(DoorId door) => Own(door).Tables.Windows ?? LentTo(door).Tables.Windows;

    /// <summary>How this door's accounts' windows are asked (<c>HarnessRoster.UsageOf</c>).</summary>
    public IUsageQuestion? UsageOf(DoorId door) => Own(door).Usage.Question ?? LentTo(door).Usage.Question;

    /// <summary>
    /// The agent that answers for this door's accounts, by name, program and account facts (<c>HarnessRoster.AccountAgentOf</c>): the
    /// door itself where it is keyed by its own name, else the door its key names where this build carries one, else the holder of
    /// that name, else the door itself. Null for a door that declares no toolchain.
    /// </summary>
    public AccountAgent? AccountAgentOf(DoorId door)
    {
        var entry = Entry(door);
        if (!Declares(entry)) return null;
        var key = OwnerOf(door);
        if (Ids.Same(key.Value, door.Value)) return AgentOf(entry);

        if (_doors.TryGetValue(key.Value, out var named) && Declares(named)) return AgentOf(named);
        if (_providers.TryGetValue(key.Value, out var holder) && holder.Program.IsKnown && !_doors.ContainsKey(key.Value))
        {
            return new AccountAgent(key.Value, holder.Program.Required, holder.Accounts.Required.Facts);
        }

        return AgentOf(entry);
    }

    /// <summary>The door whose report says this door's accounts (<c>HarnessRoster</c>'s reporting door): its account agent's where that is a door, else itself.</summary>
    public DoorId ReportingDoorOf(DoorId door) =>
        AccountAgentOf(door) is { } agent && _doors.TryGetValue(agent.Name, out var named) ? named.Door.Id : door;

    /// <summary>The settings file port of the agent whose account <paramref name="agent"/> runs as (its <c>Models</c> part), or null where its provider states none.</summary>
    public ISettingsFile? SettingsOf(string agent) =>
        (_doors.TryGetValue(agent, out var door) ? AccountAgentOf(door.Door.Id)?.Name : agent) is { } owner
        && _providers.TryGetValue(owner, out var holder) && holder.Models.IsKnown
            ? holder.Models.Required
            : null;

    /// <summary>The tool's settings file an agent's account keeps its model and effort in (<c>AgentsCommand</c>): a door's account agent's, a holder's own.</summary>
    public string? SettingsFileOf(string agent) =>
        _doors.TryGetValue(agent, out var door)
            ? AccountAgentOf(door.Door.Id)?.Accounts.SettingsFile
            : _providers.TryGetValue(agent, out var holder) && holder.Accounts.IsKnown ? holder.Accounts.Required.Facts.SettingsFile : null;

    // ---- Whether an agent is listed or named: two rules, kept apart (AGENTPROVIDER1 row b, C1-4.1) ----

    /// <summary>
    /// The roster's listing (<c>HarnessRoster.RosterAsync</c>): a door is listed when it names a binary of its own or declares no
    /// program at all, or when the home's <c>commands</c> configure one for it. The stand-ins name none, so they are listed only
    /// where a command is configured.
    /// </summary>
    public bool Listed(DoorId door, bool configured) => configured || Declarations(Entry(door)).Program is not { Binary.Count: 0 };

    /// <summary>
    /// The agents a verb may name as an operand (<c>AgentsCommand</c>, read by <c>AgentLoginCommand</c> through <c>Agent</c>), in the
    /// roster's order: each door that names a real program and each agent a door runs as with no door here (a holder, <c>codex</c>),
    /// configured or not, so no stand-in is one.
    /// </summary>
    public IReadOnlyList<string> Operands() =>
    [
        .. _doors.Values.Where(entry => Declarations(entry).Program is { Binary.Count: > 0 }).Select(entry => entry.Door.Id.Value)
            .Concat(_providers.Values
                .Where(provider => !_doors.ContainsKey(provider.Id.Value) && provider.Program is { IsKnown: true, Value.Binary.Count: > 0 })
                .Select(provider => provider.Id.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal),
    ];

    // ---- Internals ----

    private (AgentDoor Door, Provider Provider) Entry(DoorId door) =>
        _doors.TryGetValue(door.Value, out var found) ? found : throw UnknownDoor(door.Value);

    /// <summary>Whether the adapter declared a toolchain: its provider holds the program of the door that gives it its id, a door its own.</summary>
    private static bool Declares((AgentDoor Door, Provider Provider) entry) =>
        Ids.Same(entry.Provider.Id.Value, entry.Door.Id.Value) ? entry.Provider.Program.IsKnown : entry.Door.Program.IsKnown;

    private static AccountAgent AgentOf((AgentDoor Door, Provider Provider) entry)
    {
        var (program, accounts, _, _) = Declarations(entry);
        return new AccountAgent(entry.Door.Id.Value, program, accounts);
    }

    private (ToolSpec? Program, PoolFacts Accounts, TablesPart Tables, UsagePart Usage) Own(DoorId door) => Declarations(Entry(door));

    /// <summary>
    /// What a door declares itself. A door that gives its provider its id and is keyed by it (<c>claude-code</c>) declares through its
    /// provider; any other door through its own record.
    /// </summary>
    private static (ToolSpec? Program, PoolFacts Accounts, TablesPart Tables, UsagePart Usage) Declarations((AgentDoor Door, Provider Provider) entry)
    {
        var (door, provider) = entry;
        var owning = Ids.Same(provider.Id.Value, door.Id.Value);
        var program = owning ? provider.Program : door.Program;
        var programFacts = program.IsKnown ? program.Required : null;
        if (owning && Ids.Same(provider.KeyOf().Value, door.Id.Value))
        {
            return (
                programFacts,
                provider.Accounts.IsKnown ? provider.Accounts.Required.Facts : PoolFacts.None,
                provider.Tables.IsKnown ? provider.Tables.Required : TablesPart.None,
                provider.Usage.IsKnown ? provider.Usage.Required : UsagePart.None);
        }

        // A built-in door of a holder (`codex-acp` of `codex`) says its accounts' words and asks their windows as its provider does: the
        // agent's, not the door's. A plugin's door joined to it, and a door of a provider with a door of its own id, read their own.
        var ofHolder = door.Identity.DeclaredBy is null && provider.Tables.IsKnown
            && door.AccountOf is { } key && Ids.Same(key.Value, provider.Id.Value)
            && !provider.Doors.Any(each => Ids.Same(each.Id.Value, provider.Id.Value));
        return (
            programFacts, door.Lent.Accounts,
            ofHolder ? provider.Tables.Required : door.Tables.Own,
            ofHolder && provider.Usage.IsKnown ? provider.Usage.Required : door.Lent.Usage);
    }

    /// <summary>
    /// What this door reads beyond its own declarations, one level (O4-F1): from the provider its account key is, what it lends; from
    /// the door its key names, what that door lends; nothing for a door keyed by its own name or whose key names nothing here.
    /// </summary>
    private LentPart LentTo(DoorId door)
    {
        var entry = Entry(door);
        var key = OwnerOf(door);
        if (Ids.Same(key.Value, door.Value)) return LentPart.None;

        var (_, provider) = entry;
        if (Ids.Same(provider.Id.Value, key.Value))
        {
            return provider.Lent.IsKnown ? provider.Lent.Required : LentPart.None;
        }

        return _doors.TryGetValue(key.Value, out var named) ? named.Door.Lent : LentPart.None;
    }
}
