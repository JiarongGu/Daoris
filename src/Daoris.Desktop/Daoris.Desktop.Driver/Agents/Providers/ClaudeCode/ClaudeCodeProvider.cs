namespace Daoris.Driver;

/// <summary>
/// Claude Code written on the contract: provider <c>claude-code</c> with its
/// two doors, <c>claude-code</c> on the pipe and <c>claude-code-acp</c> on the protocol, which runs as its accounts.
/// </summary>
/// <remarks>
/// Each door's spawn is its own (<see cref="ClaudeCodeSpawn"/>, <see cref="ClaudeAcpSpawn"/>); its dialect is its class's. The
/// protocol door's home variable and trust file are its own <see cref="DoorHome"/>, the same values as the pipe door's.
/// </remarks>
internal static class ClaudeCodeProvider
{
    /// <summary>Its sign-in check (one email may sign in to two organisations; these tell the two accounts apart).</summary>
    /// <remarks>The CLI's twin is <c>toolchain.ts</c>' <c>claude-code</c> entry.</remarks>
    public static LoginQuestion Login { get; } = new(
        ["auth", "status"],
        LoggedIn: @"""loggedIn""\s*:\s*true",
        LoggedOut: @"""loggedIn""\s*:\s*false",
        Account: @"""email""\s*:\s*""([^""]+)""",
        Org: @"""orgName""\s*:\s*""([^""]+)""",
        Plan: @"""subscriptionType""\s*:\s*""([^""]+)""");

    /// <summary>Its id, which the settings' <em>questions</em> pause also keeps as its one value: the console's way of asking.</summary>
    public const string Id = ConsoleProviderId.Value;

    private const string Evidence ="Claude Code's declaration";
    private const string SteerEvidence = "docs/2026-10-03-steer-evidence.md";
    private static readonly MeasuredAt SteerMeasuredAt = new("2.1.284", "0.84.0");

    private const string ConfigVariable = "CLAUDE_CONFIG_DIR";

    public static Provider Registration()
    {
        var program = new ToolSpec(
            ["claude"], ["--version"], ["npm", "install", "-g", "@anthropic-ai/claude-code"], ["update"], Package: null,
            // A pin comes from the release bucket, against its signed manifest.
            Channel: ClaudeReleases.Channel,
            PinnedEnvironment: ClaudeCodeSpawn.StayPinned,
            ProbeByPresence: false);
        var pool = new PoolFacts(
            // An account that is an API key, measured on 2.1.280.
            KeyVariable: "ANTHROPIC_API_KEY",
            Login: Login,
            LoginArguments: ["auth", "login"],
            DeviceLoginArguments: null,
            LoginPorts: null,
            // Its user-tier settings, `model` and `effortLevel` among them; the protocol door's are this one's.
            SettingsFile: AgentSettings.FileName,
            // Its maker fixes an account's weekly reset at one time each week.
            WeekFixed: true);
        var tables = new TablesPart(
            Limits: ClaudeLimits.Words,
            Windows: ClaudeWindows.Words,
            SignIn: ClaudeSignIn.Words,
            // What it prints when its provider refuses the credential, measured on 2.1.280.
            Refused: "API Error: 401",
            ErrorWords: ClaudeErrors.Words);
        var usage = new UsagePart(UsageReads.Of(ClaudeUsage.Question));
        var home = new DoorHome(ConfigVariable, OwnHome: false, TrustFile: ClaudeTrust.FileName);
        var skills = new InstructionsPart(LoadsTreeSkills: true, FileLimit: null);
        var choices = ClaudeCodeSpawn.ChoiceVariablesOfClaude;
        var vocabulary = new VocabularyPart(
            Variables:
            [
                ConfigVariable, pool.KeyVariable!, choices.Model!, choices.Effort!, ClaudeAcp.ExecutableVariable,
                .. ClaudeCodeSpawn.StayPinned.Keys, .. ClaudeCodeSpawn.LoadToolsUpFront.Keys,
            ],
            Files: [ClaudeTrust.FileName, ClaudeServers.FileSuffix],
            // Its protocol door's meta keys: a rate limit, a model and a turn's origin under `_claude/`, its capabilities under `claudeCode`.
            WireKeys: ["_claude/", "claudeCode."]);

        var native = new AgentDoor(
            new DoorId("claude-code"), Transport.Native, new DoorIdentity(null, null, null),
            Known<ToolSpec>.Absent, home, LentPart.None, Known<IDoorOffers>.Declared(new SettingsOffers(SessionOnlyEfforts: true), Evidence),
            new PosturesPart(null, Asking: "default", Full: "bypassPermissions"),
            Known<ISpawn>.Declared(new ClaudeCodeSpawn(), Evidence),
            Known<IDialect>.Declared(new ClaudeNativeDialect(), Evidence),
            new AbilitiesPart(
                Known<bool>.Declared(true, Evidence),
                // It resumes a conversation it kept by its id (`--resume`), measured.
                Known<bool>.Declared(true, Evidence),
                Known<bool>.Declared(true, Evidence),
                // Its servers inherit its environment (the driven path through the repository's own `.mcp.json`).
                ServersInherit: Known<bool>.Declared(true, Evidence)),
            new DoorTablesPart(null, UsageHistory.PerTurn), null, skills);
        var protocol = new AgentDoor(
            new DoorId("claude-code-acp"), Transport.Protocol, new DoorIdentity(null, null, null),
            Known<ToolSpec>.Declared(
                new ToolSpec(
                    ["claude-agent-acp"], ["--version"], ["npm", "install", "-g", "@agentclientprotocol/claude-agent-acp"], null,
                    "@agentclientprotocol/claude-agent-acp", null, null, ProbeByPresence: false),
                Evidence),
            home, LentPart.None, Known<IDoorOffers>.Declared(new SettingsOffers(SessionOnlyEfforts: false), Evidence),
            new PosturesPart("auto|acceptEdits", Asking: "default", Full: "bypassPermissions"),
            Known<ISpawn>.Declared(new ClaudeAcpSpawn(), Evidence),
            Known<IDialect>.Declared(new ClaudeProtocolDialect(), Evidence),
            new AbilitiesPart(
                Known<bool>.Declared(true, Evidence),
                Known<bool>.Declared(true, "the protocol transport's default", Eligibility.Applies),
                Known<bool>.Declared(true, Evidence),
                WordsAtNextStep: Known<bool>.Measured(true, SteerEvidence, "posture default", SteerMeasuredAt),
                Steering: Known<bool>.Measured(true, SteerEvidence, "posture default", SteerMeasuredAt)),
            // Its own server's word that -32000 means authentication required, read before its provider's.
            new DoorTablesPart(ClaudeAcpErrors.Words, UsageHistory.PerTurn), new AccountKey("claude-code"), skills);

        return new Provider(
            new ProviderId(Id),
            new ProviderIdentity("Claude Code", "Anthropic", "CC", StandIn: false, DeclaredBy: []),
            [native, protocol],
            Known<ToolSpec>.Declared(program, Evidence),
            Known<AccountsPart>.Declared(new AccountsPart(new AccountKey("claude-code"), null, pool), Evidence),
            Known<TablesPart>.Declared(tables, Evidence),
            Known<UsagePart>.Declared(usage, Evidence),
            Known<LentPart>.Declared(new LentPart(pool, tables with { ErrorWords = null }, usage), Evidence),
            Known<ISettingsFile>.Declared(new ClaudeSettingsFile(), Evidence), Known<IChildVocabulary>.Declared(ClaudeChildVocabulary.Vocabulary, Evidence),
            Known<IRuleLanguage>.Declared(ClaudeRules.Language, Evidence),
            Known<ISkillLayout>.Declared(ClaudeSkills.Layout, Evidence),
            Known<IConversationHome>.Declared(ClaudeConversationHome.Home, Evidence),
            Reach.Everywhere,
            Known<VocabularyPart>.Declared(vocabulary, Evidence));
    }
}
