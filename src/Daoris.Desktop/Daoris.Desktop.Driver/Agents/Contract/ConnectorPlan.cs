namespace Daoris.Driver;

/// <summary>
/// The connectors one start hands its session (the design §4.4, CONNECTORS1): the servers it chose, Daoris's connector first where
/// there is one, each with its own environment, which carries the session's identity Daoris wrote into it, never an inherited one.
/// Which servers a start gets is its caller's choice; the door only answers how it takes them.
/// </summary>
/// <param name="SessionId">The session the servers are handed to, which names the file a door writes for it.</param>
/// <param name="Servers">The servers, in the order a door hands them; by name, so a name given twice is refused.</param>
public sealed record ConnectorPlan(string SessionId, IReadOnlyList<AcpMcpServer> Servers);
