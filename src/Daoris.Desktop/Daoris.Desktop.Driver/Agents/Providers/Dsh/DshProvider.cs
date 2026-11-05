namespace Daoris.Driver;

/// <summary>
/// dsh written on the contract (AGENTPROVIDER1 row d; the agent-provider design §2.3, §11): provider <c>dsh</c> with its one door of the
/// same id on the protocol. Its facts are what <see cref="DshAdapter"/> declares until row t takes the class away; the shim
/// (<see cref="LegacyDoor"/>) presents them to today's readers and a test holds the two sides equal.
/// </summary>
/// <remarks>It has no sign-in and no login question: there is no account here to be out of.</remarks>
internal static class DshProvider
{
    private const string Evidence = "dsh's declaration";

    public static Provider Registration()
    {
        // `-V, --version`, verified against the installed CLI.
        var program = new ProgramFacts(
            ["dsh"], ["--version"], ["npm", "install", "-g", "@deepseek-ai/dsh"], null, "@deepseek-ai/dsh", null, null, ProbeByPresence: false);
        var door = new AgentDoor(
            new DoorId("dsh"), Transport.Protocol, new DoorIdentity(null, null, null),
            Known<ProgramFacts>.Absent,
            new DoorHome("DSH_HOME", OwnHome: false, TrustFile: null),
            LentPart.None, Known<PortRef>.Absent,
            // Its posture is its own mechanism, an environment variable its spawn sets, never a mode on the wire.
            new PosturesPart(null, null, null, null),
            Known<ISpawn>.Declared(new DshSpawn(), Evidence),
            Known<IDialect>.Absent,
            new AbilitiesPart(
                Known<bool>.Declared(false, Evidence),
                Known<bool>.Declared(true, "the protocol transport's default", Eligibility.Applies),
                Known<bool>.Declared(false, Evidence)),
            new DoorTablesPart(null, null), null, InstructionsPart.None);

        return new Provider(
            new ProviderId("dsh"),
            // The name its maker publishes it under (AGENTS2); the binary and the door stay `dsh`, which is what a terminal types.
            new ProviderIdentity("DeepSeek Harness", "DeepSeek", "DH", StandIn: false, DeclaredBy: []),
            [door],
            Known<ProgramFacts>.Declared(program, Evidence),
            Known<AccountsPart>.Declared(new AccountsPart(new AccountKey("dsh"), null, PoolFacts.None), Evidence),
            Known<TablesPart>.Absent,
            Known<UsagePart>.Absent,
            Known<LentPart>.Declared(new LentPart(PoolFacts.None, TablesPart.None, UsagePart.None), Evidence),
            Known<PortRef>.Absent, Known<IChildVocabulary>.Absent, Known<PortRef>.Absent, Known<PortRef>.Absent,
            Known<IConversationHome>.Absent,
            Reach.Everywhere,
            Known<VocabularyPart>.Absent);
    }
}
