namespace Daoris.Driver;

/// <summary>
/// A registered door a test builds with the abilities, transport, postures, spawn and program facts it needs: what a test that needs
/// a harness the shipped doors are not (a cooling account, a refused model, a protocol door that spawns nothing) holds instead of a
/// class of its own. Each door comes as the provider that owns it by its own name, stating the program, accounts, words and identity
/// a test's <see cref="TestAgent"/> gives it; a registry is made of providers alone (<see cref="Registry(IEnumerable{Provider})"/>).
/// </summary>
internal static class TestDoors
{
    private const string Evidence = "a test door";

    /// <summary>The provider of the one door <paramref name="id"/>, over <paramref name="agent"/>'s program, accounts and words.</summary>
    public static Provider Door(
        string id,
        TestAgent? agent = null,
        Transport transport = Transport.Native,
        bool interactive = false,
        bool takesSettings = false,
        bool? resumes = null,
        PosturesPart? postures = null,
        ISpawn? spawn = null,
        IDialect? dialect = null,
        InstructionsPart? instructions = null) =>
        ProviderOf(Registered(id, agent, transport, interactive, takesSettings, resumes, postures, spawn, dialect, instructions), agent);

    /// <summary>The registry of the providers a test gives.</summary>
    public static ProviderRegistry Registry(params IEnumerable<Provider> providers) => new(providers);

    /// <summary>The shipped providers that own <paramref name="doors"/>, each narrowed to the doors named: a registry of some stand-ins and no other agent.</summary>
    public static IReadOnlyList<Provider> Shipped(params string[] doors)
    {
        var built = ProviderRegistry.Built();
        return [.. doors.Select(id => built.Provider(built.ProviderOf(built.Door(id).Id)))
            .DistinctBy(provider => provider.Id.Value)
            .Select(provider => provider with
            {
                Doors = [.. provider.Doors.Where(door => doors.Contains(door.Id.Value, StringComparer.OrdinalIgnoreCase))],
            })];
    }

    /// <summary>The registry of the providers a test names, each under its door's name.</summary>
    public static ProviderRegistry Registry(IReadOnlyDictionary<string, Provider> named) => new(named.Values);

    /// <summary>Claude Code's <c>stream-json</c> as a native door's dialect: a door that reads a structured wire.</summary>
    public static IDialect StreamJson => new ClaudeNativeDialect();

    /// <summary>The spawn of a shipped door, for a test door that runs the same program and states something else beside it.</summary>
    public static ISpawn SpawnOf(AgentDoor shipped) => shipped.Spawned();

    /// <summary>A shipped door's provider, the door stating the postures a test gives it and the rest as it ships.</summary>
    public static Provider Postured(AgentDoor shipped, PosturesPart postures)
    {
        var provider = ProviderRegistry.Built().ProviderOfDoor(shipped);
        return provider with
        {
            Doors = [.. provider.Doors.Select(door => door.Id == shipped.Id ? door with { Postures = postures } : door)],
        };
    }

    /// <summary>The agent <paramref name="id"/> that has no door here and states its program and accounts: what a door's account key names (a holder).</summary>
    public static Provider Holder(string id, TestAgent agent) => ProviderOf(Registered(id, agent), agent) with { Doors = [] };

    /// <summary>The door's record alone, for a test that reads a door without its provider.</summary>
    public static AgentDoor Registered(
        string id,
        TestAgent? agent = null,
        Transport transport = Transport.Native,
        bool interactive = false,
        bool takesSettings = false,
        bool? resumes = null,
        PosturesPart? postures = null,
        ISpawn? spawn = null,
        IDialect? dialect = null,
        InstructionsPart? instructions = null) => new(
        new DoorId(id),
        transport,
        new DoorIdentity(null, null, null),
        Known<ToolSpec>.Absent,
        agent?.Home ?? new DoorHome(null, false, null),
        agent is null ? LentPart.None : new LentPart(agent.Pool, agent.Tables(withErrors: false), new UsagePart(agent.Usage)),
        Known<IDoorOffers>.Absent,
        postures ?? new PosturesPart(null, null, null),
        Known<ISpawn>.Declared(spawn ?? new UnusedSpawn(), Evidence),
        dialect is null ? Known<IDialect>.Absent : Known<IDialect>.Declared(dialect, Evidence),
        new AbilitiesPart(
            Known<bool>.Declared(interactive, Evidence),
            resumes is { } r ? Known<bool>.Declared(r, Evidence) : Known<bool>.Absent,
            Known<bool>.Declared(takesSettings, Evidence)),
        agent is null
            ? new DoorTablesPart(null, null)
            : new DoorTablesPart(agent.ErrorWords, agent.TurnReport, agent.Limits, agent.Windows, agent.SignIn, agent.Refused),
        agent?.AccountOf is { Length: > 0 } of ? new AccountKey(of) : null,
        instructions ?? InstructionsPart.None);

    /// <summary>The provider that owns <paramref name="door"/> by its own name, stating the program, accounts, words and identity a test gave it.</summary>
    public static Provider ProviderOf(AgentDoor door, TestAgent? agent = null) => new(
        new ProviderId(door.Id.Value),
        new ProviderIdentity(agent?.Product, agent?.Maker, agent?.Mark, false, []),
        [door],
        agent is null ? Known<ToolSpec>.Absent : Known<ToolSpec>.Declared(agent.Program, Evidence),
        agent is null ? Known<AccountsPart>.Absent : Known<AccountsPart>.Declared(new AccountsPart(new AccountKey(door.Id.Value), null, agent.Pool), Evidence),
        agent is null ? Known<TablesPart>.Absent : Known<TablesPart>.Declared(agent.Tables(withErrors: true), Evidence),
        agent is null ? Known<UsagePart>.Absent : Known<UsagePart>.Declared(new UsagePart(agent.Usage), Evidence),
        agent is null
            ? Known<LentPart>.Absent
            : Known<LentPart>.Declared(new LentPart(agent.Pool, agent.Tables(withErrors: false), new UsagePart(agent.Usage)), Evidence),
        Known<ISettingsFile>.Absent, Known<IChildVocabulary>.Absent,
        door.Abilities.TakesSettings.Value ? Known<IRuleLanguage>.Declared(ClaudeRules.Language, Evidence) : Known<IRuleLanguage>.Absent,
        Known<ISkillLayout>.Declared(ClaudeSkills.Layout, Evidence),
        Known<IConversationHome>.Absent, Reach.Everywhere, Known<VocabularyPart>.Absent);

    /// <summary>A spawn whose process is never run: the program a test names, and no arguments.</summary>
    public sealed class UnusedSpawn : ISpawn
    {
        public ProcessRecipe Start(SpawnRequest request) => new("unused", []);

        public ProcessRecipe Chat(SpawnRequest request) => new("unused", []);
    }

    /// <summary>A spawn that refuses to be run, for a test whose subject spawns nothing.</summary>
    public sealed class RefusingSpawn(string why) : ISpawn
    {
        public ProcessRecipe Start(SpawnRequest request) => throw new InvalidOperationException(why);

        public ProcessRecipe Chat(SpawnRequest request) => throw new InvalidOperationException(why);
    }

    /// <summary>A spawn made of the three recipes a test states; a resume is the door's own, null where none is given.</summary>
    public sealed class ScriptedSpawn(
        Func<SpawnRequest, ProcessRecipe> start,
        Func<SpawnRequest, ProcessRecipe>? chat = null,
        Func<SpawnRequest, ProcessRecipe?>? resume = null) : ISpawn
    {
        public ProcessRecipe Start(SpawnRequest request) => start(request);

        public ProcessRecipe Chat(SpawnRequest request) => (chat ?? start)(request);

        public ProcessRecipe? Resume(SpawnRequest request) => resume?.Invoke(request);
    }
}
