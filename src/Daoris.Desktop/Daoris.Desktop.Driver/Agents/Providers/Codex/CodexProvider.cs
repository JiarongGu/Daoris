namespace Daoris.Driver;

/// <summary>
/// Codex written on the contract (AGENTPROVIDER1 row d; the agent-provider design §2.3, §11): agent <c>codex</c>, whose program,
/// sign-in and status question are <see cref="CodexAccounts"/>' (CODEXACCT1), with its one door <c>codex-acp</c> on the protocol,
/// which runs as its accounts. The door's facts are what <see cref="CodexAcpAdapter"/> declares until row t takes the class away; the
/// shim (<see cref="LegacyDoor"/>) presents them to today's readers and a test holds the two sides equal.
/// </summary>
/// <remarks>
/// What a harness naming <c>codex</c> reads of it is its sign-in alone, no words and no usage question, as today. Codex's limit
/// words and usage question are the agent's, which its door is read through, and they are lent by reference to a harness naming
/// <c>codex-acp</c>.
/// </remarks>
internal static class CodexProvider
{
    private const string Evidence = "Codex's declaration";

    public static Provider Registration()
    {
        var holder = CodexAccounts.Toolchain;
        var pool = PoolFacts.Of(holder);
        var door = new AgentDoor(
            new DoorId("codex-acp"), Transport.Protocol, new DoorIdentity(null, null, null),
            // The binary is the adapter's own bin, `codex-acp`, verified against the installed package's `bin` map.
            Known<ProgramFacts>.Declared(
                new ProgramFacts(
                    ["codex-acp"], ["--version"], ["npm", "install", "-g", "@agentclientprotocol/codex-acp"], null,
                    "@agentclientprotocol/codex-acp", null, null, ProbeByPresence: false),
                Evidence),
            // Its home must exist before `initialize` completes (ACP3); every managed home is made before a launch.
            new DoorHome("CODEX_HOME", OwnHome: false, TrustFile: null),
            // The same words and question, lent to a harness naming this door (O3-F2): a plugin's door onto `codex-acp` reads Codex's
            // by reference, while one joined to `codex` reads neither.
            new LentPart(PoolFacts.None, TablesPart.None with { Limits = CodexLimits.Words }, new UsagePart(CodexUsage.Question)),
            Known<IDoorOffers>.Absent,
            new PosturesPart("agent", Asking: "workspace-write", Full: "agent-full-access", Reading: "read-only"),
            Known<ISpawn>.Declared(new CodexAcpSpawn(), Evidence),
            Known<IDialect>.Absent,
            new AbilitiesPart(
                Known<bool>.Declared(true, Evidence),
                Known<bool>.Declared(true, "the protocol transport's default", Eligibility.Applies),
                Known<bool>.Declared(false, Evidence)),
            // Its typed failure categories, marked only for a client that asks for them (CODEXSTATUS1).
            new DoorTablesPart(CodexErrors.Words, null),
            new AccountKey(CodexAccounts.Agent),
            // Codex reads at most 32 KiB of its instruction file, so the standing block carries the repository's rules.
            new InstructionsPart(LoadsTreeSkills: false, FileLimit: 32_768));

        return new Provider(
            new ProviderId(CodexAccounts.Agent),
            new ProviderIdentity(holder.Product, holder.Maker, holder.Mark, StandIn: false, DeclaredBy: []),
            [door],
            Known<ProgramFacts>.Declared(ProgramFacts.Of(holder), Evidence),
            Known<AccountsPart>.Declared(new AccountsPart(new AccountKey(CodexAccounts.Agent), null, pool), Evidence),
            // What Codex says when a limit refuses a turn (TOOL4k) and how an account's windows are asked (CODEXUSE1) are the agent's:
            // its built door is read through them, and a harness naming `codex` still reads its sign-in alone (its `Lent` part below).
            Known<TablesPart>.Declared(TablesPart.None with { Limits = CodexLimits.Words }, Evidence),
            Known<UsagePart>.Declared(new UsagePart(CodexUsage.Question), Evidence),
            Known<LentPart>.Declared(new LentPart(pool, TablesPart.None, UsagePart.None), Evidence),
            Known<ISettingsFile>.Absent, Known<IChildVocabulary>.Absent, Known<IRuleLanguage>.Absent, Known<ISkillLayout>.Absent,
            Known<IConversationHome>.Absent,
            Reach.Everywhere,
            Known<VocabularyPart>.Absent);
    }
}
