namespace Daoris.Driver;

/// <summary>
/// Where the doors a plugin declares join a registry (the agent-provider design §2.2, row q): each placed by its account key, its
/// harness's <c>accountOf</c> verbatim, else its own name, never followed further (C3-2.1), by three rules none of which depends on the
/// order the plugins were declared in.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>The key names a provider whose own key is that same string (<c>claude-code</c>, <c>codex</c>, <c>dsh</c>, <c>stub</c>, or a
/// declared door that names none): the door joins it, and the provider names the plugin among those that declared its doors.</item>
/// <item>The key names any other door (<c>claude-code-acp</c>, <c>codex-acp</c>, a declared door naming another): the door is a provider
/// of its own, keyed by that string, its pool's facts, words and usage question the named door's own declarations (O3-F2).</item>
/// <item>The key names no door and no provider (<c>acme</c>, a disabled plugin's harness): a provider of that id is made from the doors
/// that name it.</item>
/// </list>
/// <para>A door whose name the registry carries is kept out, as is the second declaration of one name, so nothing carried is replaced. A
/// made provider takes Claude Code's skill layout and no rule language, and states no program: each declared door states its own.</para>
/// </remarks>
internal static class JoinedProviders
{
    private const string Evidence = "a plugin's declaration";

    /// <summary>The available providers of <paramref name="carried"/>, with <paramref name="declared"/> placed among them and the ones they make.</summary>
    public static IReadOnlyList<Provider> Of(ProviderRegistry carried, IEnumerable<AgentDoor> declared)
    {
        var available = carried.Providers.Where(provider => !provider.Unavailable).ToList();
        var taken = new HashSet<string>(carried.Names.Concat(available.Select(provider => provider.Id.Value)), StringComparer.OrdinalIgnoreCase);
        var joining = declared.Where(door => taken.Add(door.Id.Value)).ToList();
        var doors = new HashSet<string>(carried.Names.Concat(joining.Select(door => door.Id.Value)), StringComparer.OrdinalIgnoreCase);

        var own = joining.Where(door => Ids.Same(KeyOf(door), door.Id.Value)).ToList();
        var keyed = new HashSet<string>(
            available.Where(provider => Ids.Same(provider.KeyOf().Value, provider.Id.Value)).Select(provider => provider.Id.Value)
                .Concat(own.Select(door => door.Id.Value)),
            StringComparer.OrdinalIgnoreCase);

        var joined = new Dictionary<string, List<AgentDoor>>(StringComparer.OrdinalIgnoreCase);
        var referencing = new List<AgentDoor>();
        var made = new Dictionary<string, List<AgentDoor>>(StringComparer.OrdinalIgnoreCase);
        foreach (var door in joining.Where(door => !Ids.Same(KeyOf(door), door.Id.Value)))
        {
            var key = KeyOf(door);
            if (keyed.Contains(key)) Add(joined, key, door);
            else if (doors.Contains(key)) referencing.Add(door);
            else Add(made, key, door);
        }

        var ofKey = joining.ToDictionary(door => door.Id.Value, StringComparer.OrdinalIgnoreCase);
        return
        [
            .. available.Select(provider => Join(provider, Joined(joined, provider.Id.Value))),
            .. own.Select(door => Join(Made(door.Id.Value, [door], new AccountsPart(new AccountKey(door.Id.Value), null, PoolFacts.None)), Joined(joined, door.Id.Value))),
            .. referencing.Select(door => Referencing(door, carried, ofKey)),
            .. made.Select(each => Made(each.Key, Sorted(each.Value), new AccountsPart(new AccountKey(each.Key), null, PoolFacts.None))),
        ];
    }

    private static string KeyOf(AgentDoor door) => door.AccountOf?.Value ?? door.Id.Value;

    private static void Add(Dictionary<string, List<AgentDoor>> groups, string key, AgentDoor door)
    {
        if (!groups.TryGetValue(key, out var group)) groups[key] = group = [];
        group.Add(door);
    }

    private static List<AgentDoor> Joined(Dictionary<string, List<AgentDoor>> joined, string provider) =>
        joined.TryGetValue(provider, out var doors) ? Sorted(doors) : [];

    private static List<AgentDoor> Sorted(IEnumerable<AgentDoor> doors) => [.. doors.OrderBy(door => door.Id.Value, StringComparer.Ordinal)];

    /// <summary>Rule 1: the provider with <paramref name="doors"/> after its own, naming every plugin its doors came from.</summary>
    private static Provider Join(Provider provider, IReadOnlyList<AgentDoor> doors) => doors.Count == 0 ? provider : provider with
    {
        Doors = [.. provider.Doors, .. doors],
        Identity = provider.Identity with { DeclaredBy = DeclaredBy(provider.Identity.DeclaredBy.Concat(doors.Select(door => door.Identity.DeclaredBy))) },
    };

    /// <summary>
    /// Rule 2: the door's own provider, keyed by the door its key names and reading that door's own declarations: a carried door's as the
    /// registry reads them, a declared door's from its record.
    /// </summary>
    private static Provider Referencing(AgentDoor door, ProviderRegistry carried, IReadOnlyDictionary<string, AgentDoor> joining)
    {
        var key = KeyOf(door);
        var (facts, tables, usage) = joining.TryGetValue(key, out var named)
            ? (named.Lent.Accounts, named.Tables.Own, named.Lent.Usage)
            : carried.AgentNamed(key) is { } agent
                ? (agent.Accounts, agent.Tables with { ErrorWords = null }, new UsagePart(agent.Usage))
                : (PoolFacts.None, TablesPart.None, UsagePart.None);
        var evidence = $"the declarations of {key}";
        return Made(door.Id.Value, [door], new AccountsPart(new AccountKey(key), new DoorId(key), facts)) with
        {
            Tables = tables == TablesPart.None ? Known<TablesPart>.Absent : Known<TablesPart>.Declared(tables, evidence),
            Usage = usage.Read is null ? Known<UsagePart>.Absent : Known<UsagePart>.Declared(usage, evidence),
        };
    }

    /// <summary>A provider made from declarations: its doors' plugins, the maker and product of the door it is named for, its accounts.</summary>
    private static Provider Made(string id, IReadOnlyList<AgentDoor> doors, AccountsPart accounts)
    {
        var named = doors.FirstOrDefault(door => Ids.Same(door.Id.Value, id));
        return new Provider(
            new ProviderId(id),
            new ProviderIdentity(named?.Identity.Product, named?.Identity.Maker, null, false, DeclaredBy(doors.Select(door => door.Identity.DeclaredBy))),
            doors,
            Known<ToolSpec>.Absent,
            Known<AccountsPart>.Declared(accounts, Evidence),
            Known<TablesPart>.Absent,
            Known<UsagePart>.Absent,
            Known<LentPart>.Absent,
            Known<ISettingsFile>.Absent,
            Known<IChildVocabulary>.Absent,
            Known<IRuleLanguage>.Absent,
            Known<ISkillLayout>.Declared(ClaudeSkills.Layout, "a plugin's harness takes Claude Code's skill layout"),
            Known<IConversationHome>.Absent,
            Reach.Everywhere,
            Known<VocabularyPart>.Absent);
    }

    private static IReadOnlyList<string> DeclaredBy(IEnumerable<string?> plugins) =>
        [.. plugins.OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
