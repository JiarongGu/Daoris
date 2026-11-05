using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A conversation's agent as its runner reads it (ONEWAY1h-iii, AGENTPROVIDER1m-i part 4): the door's own facts, its process prepared and
/// equipped, what that door is handed in its own way, what it frames and how it reads a refusal, and the run that holds it. The
/// conversation's runner, its turns, its account reads and what it is told hold this and the <see cref="TransportRun"/> it makes, never the
/// adapter seam, so row t replaces what is beneath a conversation here, as it does beneath the run.
/// </summary>
internal sealed class ChatAgent(ISessionAdapter adapter)
{
    /// <summary>The door's name, which a record, a reading and a log line name the agent by.</summary>
    public string Name => adapter.Name;

    /// <summary>The door the conversation runs on: the native pipe or the protocol door.</summary>
    public SessionWire Wire => adapter.Wire;

    /// <summary>Whether the agent takes Daoris's rules file: a flag on the pipe, the session's meta on the protocol door.</summary>
    public bool TakesSettings => adapter.TakesSettings;

    /// <summary>The protocol door's posture a conversation asks for where its place names none.</summary>
    public string? Posture => adapter.AcpPosture;

    /// <summary>The agent's mode that asks nothing, which Ask Daoris's full access runs under; null where Daoris knows none.</summary>
    public string? FullPosture => adapter.FullPosture;

    /// <summary>Whether the protocol door reads a failure by its typed code before its words.</summary>
    public bool TypedFailures => adapter.Toolchain?.ErrorWords?.TypedFailures == true;

    /// <summary>The conversation's process as the door makes it, then equipped as every session's is (D230 point 3).</summary>
    public ProcessStartInfo Prepare(ChatTarget target, IReadOnlyList<string>? command, HarnessSelection selection)
    {
        var info = adapter.PrepareChat(target, command);
        SpawnEquipment.Equip(adapter, info, selection);
        return info;
    }

    /// <summary>The run over today's transports that holds the conversation's process from its bracket to its close.</summary>
    public TransportRun Run(ITransportHost host, TransportStart start) => new(host, adapter, start);

    /// <summary>The rules file named on the pipe's command line.</summary>
    public void HandSettings(ProcessStartInfo info, string rules) => adapter.HandSettings(info, rules);

    /// <summary>The rules file and the skill plugins carried on the protocol door's <c>session/new</c>.</summary>
    public object? Meta(string rules, IReadOnlyList<string> plugins) => adapter.AcpSessionMeta(rules, plugins);

    /// <summary>The skills the plugins hand <paramref name="to"/>, composed under the home and handed by the door's session-only way.</summary>
    public (string? Folder, IReadOnlyList<string> Plugins) HandSkills(
        ProcessStartInfo info, string home, string sessionId, IAgentPlugins catalog, string to) =>
        SpawnSkills.Hand(adapter, info, home, sessionId, catalog, to);

    /// <summary>The servers handed on the pipe as a file, or null on the protocol door, which carries them at its open.</summary>
    public string? HandServers(ProcessStartInfo info, string home, string sessionId, IReadOnlyList<AcpMcpServer> servers) =>
        SpawnServers.Hand(adapter, info, home, sessionId, servers);

    /// <summary>A standing block bound to this agent in <paramref name="tree"/>.</summary>
    public StandingFacts OnHarness(StandingFacts facts, string tree) => AgentContext.Current.OnHarness(facts, adapter, tree);

    /// <summary>Its turns' binding in the usage history, under the account <paramref name="profile"/> it runs as.</summary>
    public TurnBinding Binding(string? profile) => TurnBinding.Of(adapter, profile);

    /// <summary>The structured wire's mapper where the door says where a turn ends; null for a text-only pipe.</summary>
    public IStreamMapper? StructuredOutput() => adapter.StructuredOutput();

    /// <summary>A person's message as the native door takes it on stdin.</summary>
    public string FrameMessage(string text) => adapter.FrameMessage(text);

    /// <summary>The native door's line that stops a turn, or null where it has none.</summary>
    public string? FrameInterrupt() => adapter.FrameInterrupt();

    /// <summary>A refused call read as one kind, from this door's tables and, through <paramref name="adapters"/>, its owner's.</summary>
    public AgentError Read(AdapterSet adapters, DoorFailure failure, DateTimeOffset seen, TimeZoneInfo zone) =>
        AgentErrors.Read(adapters, adapter, failure, seen, zone);

    /// <summary>A refused call read for the sign-in of the account <paramref name="profile"/>, signing it out where the door's table says so.</summary>
    public Noted? SignInRefused(HarnessRoster harnesses, string? profile, string failure) =>
        RefusedAccounts.SignInRefused(harnesses, adapter, profile, failure);

    /// <summary>The maker's trailer a commit of this agent carries, and the earlier maker's where it left work in the tree.</summary>
    public string Trailers(EarlierMaker? earlier = null) => CommitTrailers.For(adapter, earlier);
}
