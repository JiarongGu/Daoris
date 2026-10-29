namespace Daoris.Driver;

/// <summary>
/// The stand-ins written on the contract (AGENTPROVIDER1 row b; the agent-provider design §3.3, §11): provider <c>stub</c> with its two
/// doors, <c>stub</c> on the pipe and <c>acp-stub</c> on the protocol, which run whatever command the machine's configuration names
/// and no model (D46 §8). Their facts are those <see cref="StubAdapter"/> and <see cref="AcpStubAdapter"/> declare until row t takes the
/// classes away; the shim (<see cref="LegacyDoor"/>) presents them to today's readers and a test holds the two sides equal.
/// </summary>
/// <remarks>
/// <para>The words are Claude Code's by reference, named exactly (O1-F5): its limit, window and sign-in words and its error
/// <b>codes</b>, never its <c>Words</c>, so a stand-in's <i>529 Overloaded</i> still ends rather than reading transient. Its refused
/// sentence, login question and home variable are the stand-in's own. <c>stub</c> states no native dialect (it maps no
/// <c>stream-json</c> and is read by the plain capture, as today), and neither door states one yet: <c>acp-stub</c>'s protocol dialects
/// are row i's, and it takes no <c>AuthRequiredCode</c>. A protocol door's resume is eligibility to negotiate, never a resume known.</para>
/// </remarks>
internal static class StubProvider
{
    /// <summary>The stand-in's configuration-home variable: its own, and its protocol door's, which runs as its accounts (TOOL4j).</summary>
    public const string ConfigVariable = "DAORIS_STUB_CONFIG_DIR";

    private const string Evidence = "the stand-in's declaration";

    public static Provider Registration()
    {
        var program = new ProgramFacts([], ["--version"], null, null, null, null, null, ProbeByPresence: false);
        var pool = new PoolFacts(
            KeyVariable: null,
            // A login flow too, so signing in to another account (D66 §3) can be gated with no account: whatever the configured
            // command does with `--login` is the stand-in's sign-in.
            Login: new LoginQuestion(
                ["--login-state"], LoggedIn: @"logged-in", LoggedOut: @"logged-out",
                Account: @"logged-in as (\S+)",
                // And its way to say an organisation and a plan (ACCOUNTID1), each on a line of its own after who.
                Org: @"(?m)^org:[ \t]*(.+)$",
                Plan: @"(?m)^plan:[ \t]*(.+)$"),
            LoginArguments: ["--login"],
            DeviceLoginArguments: null,
            LoginPorts: null,
            SettingsFile: null,
            // Claude Code's weekly reset fixed per account (TOOL6b), mirrored so a rehearsal can gate a week carried on.
            WeekFixed: true);
        var tables = new TablesPart(
            Limits: ClaudeLimits.Words,
            Windows: ClaudeWindows.Words,
            SignIn: ClaudeSignIn.Words,
            // Claude Code's own words for a refused credential (AGT3b), mirrored so a refused account can be gated with none.
            Refused: "API Error: 401",
            // The kind words and statuses alone, so a stand-in's failure that names no code keeps today's end (AGENTERRORS1a).
            ErrorWords: ClaudeErrors.Codes);
        var lent = new LentPart(pool, tables with { ErrorWords = null }, UsagePart.None);
        var stub = new AgentDoor(
            new DoorId("stub"), Transport.Native, new DoorIdentity(null, null, null),
            Known<ProgramFacts>.Absent,
            // Its own home is nobody's, so a start that names no account still runs there (OWNSPACE1a).
            new DoorHome(ConfigVariable, OwnHome: true, TrustFile: null, MustExist: false),
            LentPart.None, Known<PortRef>.Absent, new PosturesPart(null, null, null),
            Known<ISpawn>.Declared(new StandInSpawn("stub", "agent.mjs", protocol: false), Evidence),
            Known<IDialect>.Absent,
            new AbilitiesPart(
                // Interactive, so the whole conversation loop can be gated with no model in it.
                Known<bool>.Declared(true, Evidence),
                Known<bool>.Declared(false, Evidence),
                Known<bool>.Declared(false, Evidence)),
            new DoorTablesPart(null, null), null);
        var acp = new AgentDoor(
            new DoorId("acp-stub"), Transport.Protocol, new DoorIdentity(null, null, null),
            // Present by a file look: the agent a rehearsal names waits on its stdin for frames, so asking it a version would stall.
            Known<ProgramFacts>.Declared(new ProgramFacts([], [], null, null, null, null, null, ProbeByPresence: true), Evidence),
            new DoorHome(ConfigVariable, OwnHome: true, TrustFile: null, MustExist: false),
            // A door onto the stand-in's accounts (TOOL4j): the sign-in, the login question and the limit words are the stub's.
            new LentPart(PoolFacts.None, TablesPart.None, UsagePart.None),
            Known<PortRef>.Absent, new PosturesPart(null, null, null),
            Known<ISpawn>.Declared(new StandInSpawn("acp-stub", "acp-agent.mjs", protocol: true), Evidence),
            Known<IDialect>.Absent,
            new AbilitiesPart(
                Known<bool>.Declared(true, Evidence),
                // The transport's default, eligibility to negotiate at the open and no resume known (X).
                Known<bool>.Declared(true, "the protocol transport's default", Eligibility.Applies),
                Known<bool>.Declared(false, Evidence)),
            new DoorTablesPart(null, null), new AccountKey("stub"));

        return new Provider(
            new ProviderId("stub"),
            new ProviderIdentity(null, null, null, StandIn: true, DeclaredBy: []),
            [acp, stub],
            Known<ProgramFacts>.Declared(program, Evidence),
            Known<AccountsPart>.Declared(new AccountsPart(new AccountKey("stub"), null, pool), Evidence),
            Known<TablesPart>.Declared(tables, Evidence),
            Known<UsagePart>.Absent,
            Known<LentPart>.Declared(lent, Evidence),
            Known<PortRef>.Absent, Known<IChildVocabulary>.Absent, Known<PortRef>.Absent, Known<PortRef>.Absent,
            Known<IConversationHome>.Absent,
            Reach.Everywhere,
            Known<VocabularyPart>.Absent);
    }
}
