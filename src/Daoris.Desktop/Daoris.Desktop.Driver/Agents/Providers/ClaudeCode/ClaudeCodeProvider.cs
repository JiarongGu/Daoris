namespace Daoris.Driver;

/// <summary>
/// Claude Code written on the contract (AGENTPROVIDER1 row d; the agent-provider design §2.3, §11): provider <c>claude-code</c> with its
/// two doors, <c>claude-code</c> on the pipe and <c>claude-code-acp</c> on the protocol, which runs as its accounts. Its facts are what
/// <see cref="ClaudeCodeAdapter"/> and <see cref="ClaudeAcpAdapter"/> declare until row t takes the classes away; the shim
/// (<see cref="LegacyDoor"/>) presents them to today's readers and a test holds the two sides equal.
/// </summary>
/// <remarks>
/// Each door's spawn is its own (<see cref="ClaudeCodeSpawn"/>, <see cref="ClaudeAcpSpawn"/>); its dialect is its class's until rows i and k. The
/// protocol door's home variable and trust file are its own <see cref="DoorHome"/>, the same values as the pipe door's (O2-F2).
/// </remarks>
internal static class ClaudeCodeProvider
{
    /// <summary>Its sign-in check (ACCOUNTID1: one email may sign in to two organisations; these tell the two accounts apart).</summary>
    /// <remarks>The CLI's twin is <c>toolchain.ts</c>' <c>claude-code</c> entry.</remarks>
    public static LoginQuestion Login { get; } = new(
        ["auth", "status"],
        LoggedIn: @"""loggedIn""\s*:\s*true",
        LoggedOut: @"""loggedIn""\s*:\s*false",
        Account: @"""email""\s*:\s*""([^""]+)""",
        Org: @"""orgName""\s*:\s*""([^""]+)""",
        Plan: @"""subscriptionType""\s*:\s*""([^""]+)""");

    /// <summary>Its id, which the settings' <em>questions</em> pause also keeps as its one value: the console's way of asking (D184 point 4).</summary>
    public const string Id = "claude-code";

    private const string Evidence ="Claude Code's declaration";

    private const string ConfigVariable = "CLAUDE_CONFIG_DIR";

    public static Provider Registration()
    {
        var program = new ProgramFacts(
            ["claude"], ["--version"], ["npm", "install", "-g", "@anthropic-ai/claude-code"], ["update"], Package: null,
            // A pin comes from the release bucket, against its signed manifest (AGT2b).
            Channel: ClaudeReleases.Channel,
            PinnedEnvironment: ClaudeCodeSpawn.StayPinned,
            ProbeByPresence: false);
        var pool = new PoolFacts(
            // An account that is an API key (AGT3), measured on 2.1.280.
            KeyVariable: "ANTHROPIC_API_KEY",
            Login: Login,
            LoginArguments: ["auth", "login"],
            DeviceLoginArguments: null,
            LoginPorts: null,
            // Its user-tier settings, `model` and `effortLevel` among them (AGT6); the protocol door's are this one's.
            SettingsFile: AgentSettings.FileName,
            // Its maker fixes an account's weekly reset at one time each week (TOOL6b, D130 §0.3 C1).
            WeekFixed: true);
        var tables = new TablesPart(
            Limits: ClaudeLimits.Words,
            Windows: ClaudeWindows.Words,
            SignIn: ClaudeSignIn.Words,
            // What it prints when its provider refuses the credential (AGT3b), measured on 2.1.280.
            Refused: "API Error: 401",
            ErrorWords: ClaudeErrors.Words);
        var usage = new UsagePart(ClaudeUsage.Question);
        var home = new DoorHome(ConfigVariable, OwnHome: false, TrustFile: ClaudeTrust.FileName);
        var skills = new InstructionsPart(LoadsTreeSkills: true, FileLimit: null);

        var native = new AgentDoor(
            new DoorId("claude-code"), Transport.Native, new DoorIdentity(null, null, null),
            Known<ProgramFacts>.Absent, home, LentPart.None, Known<PortRef>.Absent,
            new PosturesPart(null, Asking: "default", Full: "bypassPermissions", Reading: null),
            Known<ISpawn>.Declared(new ClaudeCodeSpawn(), Evidence),
            Known<IDialect>.Declared(new AdapterNativeDialect(new ClaudeCodeAdapter()), Evidence),
            new AbilitiesPart(
                Known<bool>.Declared(true, Evidence),
                // It resumes a conversation it kept by its id (`--resume`), measured (AGENTLYNTAI1s-i).
                Known<bool>.Declared(true, Evidence),
                Known<bool>.Declared(true, Evidence),
                // Its servers inherit its environment (D70's driven path through the repository's own `.mcp.json`), and
                // `--strict-mcp-config` leaves the agent's own out (the holder's terminal).
                ServersInherit: Known<bool>.Declared(true, Evidence),
                OwnServersLeftOut: Known<bool>.Declared(true, "`--strict-mcp-config`")),
            new DoorTablesPart(null, UsageHistory.PerTurn), null, skills);
        var protocol = new AgentDoor(
            new DoorId("claude-code-acp"), Transport.Protocol, new DoorIdentity(null, null, null),
            Known<ProgramFacts>.Declared(
                new ProgramFacts(
                    ["claude-agent-acp"], ["--version"], ["npm", "install", "-g", "@agentclientprotocol/claude-agent-acp"], null,
                    "@agentclientprotocol/claude-agent-acp", null, null, ProbeByPresence: false),
                Evidence),
            home, LentPart.None, Known<PortRef>.Absent,
            new PosturesPart("auto|acceptEdits", Asking: "default", Full: "bypassPermissions", Reading: null),
            Known<ISpawn>.Declared(new ClaudeAcpSpawn(), Evidence),
            Known<IDialect>.Declared(new AdapterProtocolDialect(new ClaudeAcpAdapter()), Evidence),
            new AbilitiesPart(
                Known<bool>.Declared(true, Evidence),
                Known<bool>.Declared(true, "the protocol transport's default", Eligibility.Applies),
                Known<bool>.Declared(true, Evidence)),
            // Its own server's word that -32000 means authentication required (AGENTERRORS1a), read before its provider's.
            new DoorTablesPart(ClaudeAcpErrors.Words, UsageHistory.PerTurn), new AccountKey("claude-code"), skills);

        return new Provider(
            new ProviderId(Id),
            new ProviderIdentity("Claude Code", "Anthropic", "CC", StandIn: false, DeclaredBy: []),
            [native, protocol],
            Known<ProgramFacts>.Declared(program, Evidence),
            Known<AccountsPart>.Declared(new AccountsPart(new AccountKey("claude-code"), null, pool), Evidence),
            Known<TablesPart>.Declared(tables, Evidence),
            Known<UsagePart>.Declared(usage, Evidence),
            Known<LentPart>.Declared(new LentPart(pool, tables with { ErrorWords = null }, usage), Evidence),
            Known<PortRef>.Absent, Known<IChildVocabulary>.Absent,
            Known<IRuleLanguage>.Declared(ClaudeRules.Language, Evidence),
            Known<ISkillLayout>.Declared(ClaudeSkills.Layout, Evidence),
            Known<IConversationHome>.Absent,
            Reach.Everywhere,
            Known<VocabularyPart>.Absent);
    }
}
