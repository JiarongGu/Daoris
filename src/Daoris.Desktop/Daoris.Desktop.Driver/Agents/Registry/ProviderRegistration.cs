namespace Daoris.Driver;

/// <summary>
/// The registration of today's adapters as providers and doors (AGENTPROVIDER1 row a; the agent-provider design §2.2, §11): the one
/// place that reads <see cref="AdapterSet"/> and its toolchains to make the contract's records, grouping the doors by the account
/// key each declares. It changes nothing it reads.
/// </summary>
/// <remarks>
/// <para>A door's account key is its harness's <c>accountOf</c> verbatim, else its own name, never followed further (C3-2.1). Three
/// rules place a door whose key is not its own name, and none depends on the order the plugins were declared in:</para>
/// <list type="number">
/// <item>the key names a provider whose own key is that same string (<c>claude-code</c>, <c>codex</c>, <c>dsh</c>, <c>stub</c>, or a
/// plugin's harness that declares no <c>accountOf</c>): the door joins it;</item>
/// <item>the key names any other door a harness holds (<c>claude-code-acp</c>, <c>codex-acp</c>, a plugin's harness that itself names
/// another): the door is a provider of its own, keyed by that string, its pool's facts the named door's own declarations
/// (O3-F2);</item>
/// <item>the key names no door and no provider (<c>acme</c>, or a disabled plugin's harness): a provider of that id is made from the
/// declarations that name it, every door naming it joined to it.</item>
/// </list>
/// <para>A name the set keeps for a disabled plugin (<see cref="AdapterSet.Agents"/>) with no door of any provider is an unavailable
/// descriptor: known, never started (C1-1.2).</para>
/// </remarks>
internal static class ProviderRegistration
{
    private sealed class Group(string id)
    {
        public string Id { get; } = id;

        public List<string> Doors { get; } = [];

        /// <summary>The toolchain of an agent a door runs as with no door of its own here (<c>codex</c>).</summary>
        public HarnessToolchain? Holder { get; init; }

        /// <summary>The door a provider of its own is keyed by (rule 2).</summary>
        public string? References { get; init; }
    }

    public static ProviderRegistry Of(AdapterSet set)
    {
        var doors = set.Names;
        var held = new HashSet<string>(doors, StringComparer.OrdinalIgnoreCase);
        var keyOf = doors.ToDictionary(name => name, name => KeyOf(set, name), StringComparer.OrdinalIgnoreCase);

        // Rule 1's targets: the providers whose key is their own id, which no declaration made after them can change.
        var keyed = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in doors.Where(name => Ids.Same(keyOf[name], name))) keyed[name] = new Group(name) { Doors = { name } };
        foreach (var name in set.Reserved.Where(name => !held.Contains(name)))
        {
            if (set.Holder(name) is { } holder) keyed[name] = new Group(name) { Holder = holder };
        }

        var own = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        var made = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in doors.Where(name => !Ids.Same(keyOf[name], name)))
        {
            var key = keyOf[name];
            if (keyed.TryGetValue(key, out var joined)) joined.Doors.Add(name);
            else if (held.Contains(key)) own[name] = new Group(name) { Doors = { name }, References = key };
            else
            {
                if (!made.TryGetValue(key, out var group)) made[key] = group = new Group(key);
                group.Doors.Add(name);
            }
        }

        var providers = keyed.Values.Concat(own.Values).Concat(made.Values)
            .Select(group => BuildProvider(set, group))
            .ToList();

        // A name kept for a plugin turned off: no door, so no provider of its own, but known with its id (POOLSTRAY1).
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers) named.UnionWith(Names(provider));
        var unavailable = set.Agents.Where(agent => !named.Contains(agent)).Select(Descriptor);
        providers.AddRange(unavailable);

        var reserved = set.Reserved.Where(name => set.DeclaredBy(name) is null).ToList();
        return new ProviderRegistry([.. providers.OrderBy(provider => provider.Id.Value, StringComparer.Ordinal)], reserved);
    }

    /// <summary>Every name a provider answers for: its id, its doors', its account key and each door's declared one.</summary>
    internal static IEnumerable<string> Names(Provider provider) =>
        provider.Doors.Select(door => door.Id.Value)
            .Append(provider.Id.Value)
            .Append(provider.KeyOf().Value)
            .Concat(provider.Doors.Select(door => door.AccountOf?.Value).OfType<string>());

    private static string KeyOf(AdapterSet set, string name) =>
        set.Resolve(name).Toolchain?.Owner(name) ?? name;

    private static Provider Descriptor(string agent)
    {
        var id = new ProviderId(agent);
        return new Provider(
            id, new ProviderIdentity(null, null, null, false, []), [], Known<ProgramFacts>.Absent,
            Known<AccountsPart>.Declared(new AccountsPart(new AccountKey(agent), null, PoolFacts.None), "a name a disabled plugin declares"),
            Known<TablesPart>.Absent, Known<UsagePart>.Absent, Known<LentPart>.Absent, Absent(), Known<IChildVocabulary>.Absent, Absent(),
            Absent(), Known<IConversationHome>.Absent, Reach.Everywhere, Known<VocabularyPart>.Absent, Unavailable: true);
    }

    private static Known<PortRef> Absent() => Known<PortRef>.Absent;

    private static Provider BuildProvider(AdapterSet set, Group group)
    {
        var id = new ProviderId(group.Id);
        var doors = group.Doors.Order(StringComparer.Ordinal).Select(name => BuildDoor(set, name, group.Id)).ToList();
        var owning = group.Doors.FirstOrDefault(name => Ids.Same(name, group.Id));
        var declaring = owning is null ? group.Holder : set.Resolve(owning).Toolchain;
        var evidence = $"the toolchain of {group.Id}";

        var accounts = Accounts(set, group, declaring, evidence);
        var references = group.References is null ? null : set.Resolve(group.References).Toolchain;
        var tables = references is not null
            ? TablesPart.Of(references, withErrors: false)
            : declaring is null ? TablesPart.None : TablesPart.Of(declaring, withErrors: true);
        var usage = new UsagePart((references ?? declaring)?.Usage);

        // What a harness naming this id reads of it, one level (O4-F1): all that it declares, but a holder's words and usage
        // question, which no door carries here and none reads (`codex`, until a correction of its own).
        var lent = group.Holder is { } holder
            ? new LentPart(PoolFacts.Of(holder), TablesPart.None, UsagePart.None)
            : owning is not null && declaring is not null
                ? group.References is null
                    ? new LentPart(PoolFacts.Of(declaring!), TablesPart.Of(declaring!, withErrors: false), new UsagePart(declaring!.Usage))
                    : doors.Single(door => Ids.Same(door.Id.Value, owning)).Lent
                : (LentPart?)null;

        return new Provider(
            id,
            new ProviderIdentity(
                declaring?.Product, declaring?.Maker, declaring?.Mark, Providers.IsStandIn(group.Id),
                [.. group.Doors.Select(set.DeclaredBy).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]),
            doors,
            declaring is null ? Known<ProgramFacts>.Absent : Known<ProgramFacts>.Declared(ProgramFacts.Of(declaring), evidence),
            Known<AccountsPart>.Declared(accounts, evidence),
            Empty(tables) ? Known<TablesPart>.Absent : Known<TablesPart>.Declared(tables, evidence),
            usage.Question is null ? Known<UsagePart>.Absent : Known<UsagePart>.Declared(usage, evidence),
            lent is null ? Known<LentPart>.Absent : Known<LentPart>.Declared(lent, evidence),
            Absent(), Known<IChildVocabulary>.Absent, Absent(), Absent(), Known<IConversationHome>.Absent,
            Reach.Everywhere,
            Known<VocabularyPart>.Absent);
    }

    private static AccountsPart Accounts(AdapterSet set, Group group, HarnessToolchain? declaring, string evidence)
    {
        if (group.References is { } named)
        {
            var toolchain = set.Resolve(named).Toolchain;
            return new AccountsPart(new AccountKey(named), new DoorId(named), toolchain is null ? PoolFacts.None : PoolFacts.Of(toolchain));
        }

        return new AccountsPart(new AccountKey(group.Id), null, declaring is null ? PoolFacts.None : PoolFacts.Of(declaring));
    }

    private static bool Empty(TablesPart tables) =>
        tables.Limits is null && tables.Windows is null && tables.SignIn is null && tables.Refused is null && tables.ErrorWords is null;

    private static AgentDoor BuildDoor(AdapterSet set, string name, string provider)
    {
        var adapter = set.Resolve(name);
        var toolchain = adapter.Toolchain;
        var plugin = set.DeclaredBy(name);
        var owning = Ids.Same(name, provider);
        var key = KeyOf(set, name);
        var declared = toolchain?.AccountOf is { Length: > 0 } accountOf ? new AccountKey(accountOf) : (AccountKey?)null;
        // The provider holds what the door that gives it its id declares, each fact in one place; a door's own account words are
        // lent (O4-F1) and a door keyed by its own name has none of its own to lend.
        var keyedItself = owning && Ids.Same(key, name);
        var lent = toolchain is null || keyedItself
            ? LentPart.None
            : new LentPart(PoolFacts.Of(toolchain), TablesPart.Of(toolchain, withErrors: false), new UsagePart(toolchain.Usage));
        var transport = TransportOf.Wire(adapter.Wire);

        return new AgentDoor(
            new DoorId(name),
            transport,
            plugin is null ? new DoorIdentity(null, null, null) : new DoorIdentity(plugin, toolchain?.Product, toolchain?.Maker),
            toolchain is null || owning ? Known<ProgramFacts>.Absent : Known<ProgramFacts>.Declared(ProgramFacts.Of(toolchain), $"the toolchain of {name}"),
            DoorHome.Of(toolchain),
            lent,
            Absent(),
            new PosturesPart(adapter.AcpPosture, adapter.AskingPosture, adapter.FullPosture),
            Known<ISpawn>.Absent,
            Known<IDialect>.Absent,
            new AbilitiesPart(
                Known<bool>.Declared(adapter.Interactive, "ISessionAdapter.Interactive"),
                // A protocol door's resume is the transport's default, eligibility to negotiate at the open and never a resume known,
                // as the stand-in written on the contract states it, so both readings of one door agree (AGENTPROVIDER1 row b, §2.6).
                transport == Transport.Protocol
                    ? Known<bool>.Declared(adapter.Resumes, "the protocol transport's default", Eligibility.Applies)
                    : Known<bool>.Declared(adapter.Resumes, "ISessionAdapter.Resumes"),
                Known<bool>.Declared(adapter.TakesSettings, "ISessionAdapter.TakesSettings")),
            new DoorTablesPart(keyedItself ? null : toolchain?.ErrorWords, toolchain?.TurnReport),
            declared);
    }
}
