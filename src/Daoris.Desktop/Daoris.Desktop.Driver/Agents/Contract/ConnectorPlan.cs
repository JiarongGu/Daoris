namespace Daoris.Driver;

/// <summary>Where a connector a start hands comes from: the sources a workspace permits (CONNECTORS1).</summary>
public enum ConnectorSource
{
    /// <summary>Daoris's own knowledge connector, carrying the session's identity.</summary>
    Daoris,

    /// <summary>A server a plugin declares for sessions.</summary>
    Plugin,

    /// <summary>The in-app browser's server.</summary>
    Browser,

    /// <summary>The agent's own servers, its account's and its repository's, which it reads for itself.</summary>
    Own,
}

/// <summary>
/// The connectors one start hands its session (the design §4.4, CONNECTORS1): the servers it chose, Daoris's connector first where
/// there is one, each with its own environment, which carries the session's identity Daoris wrote into it, never an inherited one.
/// Which servers a start gets is its caller's choice, made once per workspace (<see cref="ConnectorPlans"/>); a door only answers how it
/// takes them, on its wire (<see cref="AcpProtocol"/>'s open) or by a file in its provider's layout (<see cref="IServerHanding"/>).
/// </summary>
/// <param name="SessionId">The session the servers are handed to, which names the file a door writes for it.</param>
/// <param name="Servers">The servers, in the order a door hands them; by name, so a name given twice is refused.</param>
public sealed record ConnectorPlan(string SessionId, IReadOnlyList<AcpMcpServer> Servers)
{
    /// <summary>The workspace whose connectors these are; null for a start that has none.</summary>
    public string? Workspace { get; init; }

    /// <summary>The names of the servers this start requires: a plan lacking one is refused rather than handed short.</summary>
    public IReadOnlyList<string> Includes { get; init; } = [];

    /// <summary>The agent's own servers are to be left out: only a door that can leave them out may take this start.</summary>
    public bool ExcludesOwn { get; init; }

    /// <summary>The sources the workspace permits; a server from another is refused. Null permits Daoris's, the plugins' and the browser's.</summary>
    public IReadOnlyList<ConnectorSource>? Permitted { get; init; }

    /// <summary>
    /// Why a door of these abilities cannot take this plan, said for a person; null where it can. A start that depends on an exclusion its
    /// door cannot honour is refused, and a required server nothing chose is the plan's own fault.
    /// </summary>
    public string? Refusal(string door, DoorAbilities abilities)
    {
        if (Fault() is { } fault) return fault;
        return ExcludesOwn && !abilities.OwnServersLeftOut
            ? $"this start leaves the agent's own servers out, which `{door}` cannot do, so it does not start there with them in"
            : null;
    }

    /// <summary>What is wrong with the plan whatever door takes it: a name given twice, or a required server nothing handed; null where nothing is.</summary>
    public string? Fault()
    {
        if (Servers.FirstOrDefault(server => Servers.Count(other => other.Name == server.Name) > 1) is { } twice)
        {
            return $"the connector `{twice.Name}` is named twice, so which one a door is handed is not said";
        }

        return Includes.FirstOrDefault(name => Servers.All(server => server.Name != name)) is { } missing
            ? $"this start requires the connector `{missing}`, which nothing handed it"
            : null;
    }

    /// <summary>The sources this plan permits.</summary>
    public bool Allows(ConnectorSource source) => Permitted is null
        ? source != ConnectorSource.Own
        : Permitted.Contains(source);
}
