namespace Daoris.Driver;

/// <summary>
/// A registered door a test builds with the abilities, transport, postures, spawn and program facts it needs, read through the one
/// reading every door has: what a test that needs a harness the shipped doors are not (a cooling account, a refused model, a
/// protocol door that spawns nothing) holds instead of a class of its own. A door built here states its own identity, home and
/// words, as a plugin's does, so it takes nothing from a provider and strips no other provider's variables.
/// </summary>
internal static class TestDoors
{
    private const string Evidence = "a test door";

    /// <summary>The door <paramref name="id"/> over <paramref name="toolchain"/>'s program, accounts and words.</summary>
    public static ISessionAdapter Door(
        string id,
        HarnessToolchain? toolchain = null,
        Transport transport = Transport.Native,
        bool interactive = false,
        bool takesSettings = false,
        bool? resumes = null,
        PosturesPart? postures = null,
        ISpawn? spawn = null,
        IDialect? dialect = null,
        InstructionsPart? instructions = null)
    {
        var door = Registered(id, toolchain, transport, interactive, takesSettings, resumes, postures, spawn, dialect, instructions);
        return new LegacyDoor(ProviderOf(door, toolchain), door);
    }

    /// <summary>The door's record, for a test that reads the registry rather than the old seam.</summary>
    public static AgentDoor Registered(
        string id,
        HarnessToolchain? toolchain = null,
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
        DoorHome.Of(toolchain),
        toolchain is null
            ? LentPart.None
            : new LentPart(PoolFacts.Of(toolchain), TablesPart.None, new UsagePart(toolchain.Usage)),
        Known<IDoorOffers>.Absent,
        postures ?? new PosturesPart(null, null, null, null),
        Known<ISpawn>.Declared(spawn ?? new UnusedSpawn(), Evidence),
        dialect is null ? Known<IDialect>.Absent : Known<IDialect>.Declared(dialect, Evidence),
        new AbilitiesPart(
            Known<bool>.Declared(interactive, Evidence),
            resumes is { } r ? Known<bool>.Declared(r, Evidence) : Known<bool>.Absent,
            Known<bool>.Declared(takesSettings, Evidence)),
        toolchain is null
            ? new DoorTablesPart(null, null)
            : new DoorTablesPart(toolchain.ErrorWords, toolchain.TurnReport, toolchain.Limits, toolchain.Windows, toolchain.SignIn, toolchain.Refused),
        toolchain?.AccountOf is { Length: > 0 } of ? new AccountKey(of) : null,
        instructions ?? InstructionsPart.None);

    /// <summary>The provider that owns <paramref name="door"/> by its own name, stating the program, accounts, words and identity of the toolchain a test gave it.</summary>
    public static Provider ProviderOf(AgentDoor door, HarnessToolchain? toolchain = null) => new(
        new ProviderId(door.Id.Value),
        new ProviderIdentity(toolchain?.Product, toolchain?.Maker, toolchain?.Mark, false, []),
        [door],
        toolchain is null ? Known<ToolSpec>.Absent : Known<ToolSpec>.Declared(ToolSpecs.Of(toolchain), Evidence),
        toolchain is null ? Known<AccountsPart>.Absent : Known<AccountsPart>.Declared(new AccountsPart(new AccountKey(door.Id.Value), null, PoolFacts.Of(toolchain)), Evidence),
        toolchain is null ? Known<TablesPart>.Absent : Known<TablesPart>.Declared(TablesPart.Of(toolchain, true), Evidence),
        toolchain is null ? Known<UsagePart>.Absent : Known<UsagePart>.Declared(new UsagePart(toolchain.Usage), Evidence),
        Known<LentPart>.Absent, Known<ISettingsFile>.Absent, Known<IChildVocabulary>.Absent,
        Known<IRuleLanguage>.Declared(ClaudeRules.Language, Evidence), Known<ISkillLayout>.Declared(ClaudeSkills.Layout, Evidence),
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
