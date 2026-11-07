namespace Daoris.Driver;

/// <summary>
/// The second stand-in written on the contract alone: provider
/// <c>alt-stub</c> with its two doors, <c>alt-stub</c> on the pipe and <c>alt-acp-stub</c> on the protocol, which run whatever command the
/// machine's configuration names and no model. It mirrors <see cref="StubProvider"/> in what it declares and differs in what it says: a
/// home variable, a sign-in and login question, a limit, window and sign-in grammar of its own (<see cref="AltStubWords"/>), so no
/// caller that read the first stand-in's by habit passes with it. No class of an older seam stands behind it; the registration list's one
/// line is the whole of its place.
/// </summary>
/// <remarks>
/// Its error codes are Claude Code's by reference, as the first stand-in's are, so a stand-in's <i>529 Overloaded</i> still reads
/// transient on the protocol door's own kind and ends on the native one; its refused sentence is its own. Its pin is served by no
/// release channel: the case is pending, named (<c>pending.json</c>).
/// </remarks>
internal static class AltStubProvider
{
    /// <summary>The stand-in's configuration-home variable, its own and its protocol door's, which runs as its accounts.</summary>
    public const string ConfigVariable = "ALT_STUB_CONFIG_HOME";

    private const string Evidence = "the stand-in's declaration";

    public static Provider Registration()
    {
        var program = new ToolSpec([], ["--version"], null, null, null, null, null, ProbeByPresence: false);
        var pool = new PoolFacts(
            KeyVariable: null,
            Login: new LoginQuestion(
                ["--login-state"], LoggedIn: @"session: active", LoggedOut: @"session: none",
                Account: @"session: active as (\S+)",
                Org: @"(?m)^workspace:[ \t]*(.+)$",
                Plan: @"(?m)^tier:[ \t]*(.+)$"),
            LoginArguments: ["--login"],
            DeviceLoginArguments: null,
            LoginPorts: null,
            SettingsFile: null,
            WeekFixed: false);
        var tables = new TablesPart(
            Limits: AltStubWords.Limits,
            Windows: AltStubWords.Windows,
            SignIn: AltStubWords.SignIn,
            Refused: "credential rejected (401)",
            ErrorWords: ClaudeErrors.Codes);
        var lent = new LentPart(pool, tables with { ErrorWords = null }, UsagePart.None);
        var native = new AgentDoor(
            new DoorId("alt-stub"), Transport.Native, new DoorIdentity(null, null, null),
            Known<ToolSpec>.Absent,
            new DoorHome(ConfigVariable, OwnHome: true, TrustFile: null),
            LentPart.None, Known<IDoorOffers>.Absent, new PosturesPart(null, null, null, null),
            Known<ISpawn>.Declared(new StandInSpawn("alt-stub", "agent.mjs", protocol: false), Evidence),
            Known<IDialect>.Absent,
            new AbilitiesPart(
                Known<bool>.Declared(true, Evidence),
                Known<bool>.Declared(false, Evidence),
                Known<bool>.Declared(false, Evidence)),
            new DoorTablesPart(null, null), null, InstructionsPart.None);
        var protocol = new AgentDoor(
            new DoorId("alt-acp-stub"), Transport.Protocol, new DoorIdentity(null, null, null),
            // Present by a file look: the agent a rehearsal names waits on its stdin for frames, so asking it a version would stall.
            Known<ToolSpec>.Declared(new ToolSpec([], [], null, null, null, null, null, ProbeByPresence: true), Evidence),
            new DoorHome(ConfigVariable, OwnHome: true, TrustFile: null),
            new LentPart(PoolFacts.None, TablesPart.None, UsagePart.None),
            Known<IDoorOffers>.Absent, new PosturesPart(null, null, null, null),
            Known<ISpawn>.Declared(new StandInSpawn("alt-acp-stub", "agent.mjs", protocol: true), Evidence),
            Known<IDialect>.Absent,
            new AbilitiesPart(
                Known<bool>.Declared(true, Evidence),
                Known<bool>.Declared(true, "the protocol transport's default", Eligibility.Applies),
                Known<bool>.Declared(false, Evidence)),
            new DoorTablesPart(null, null), new AccountKey("alt-stub"), InstructionsPart.None);

        return new Provider(
            new ProviderId("alt-stub"),
            new ProviderIdentity(null, null, null, StandIn: true, DeclaredBy: []),
            [protocol, native],
            Known<ToolSpec>.Declared(program, Evidence),
            Known<AccountsPart>.Declared(new AccountsPart(new AccountKey("alt-stub"), null, pool), Evidence),
            Known<TablesPart>.Declared(tables, Evidence),
            Known<UsagePart>.Absent,
            Known<LentPart>.Declared(lent, Evidence),
            Known<ISettingsFile>.Absent, Known<IChildVocabulary>.Absent, Known<IRuleLanguage>.Absent, Known<ISkillLayout>.Absent,
            Known<IConversationHome>.Declared(AltStubConversationHome.Home, Evidence),
            Reach.Everywhere,
            Known<VocabularyPart>.Absent);
    }
}
