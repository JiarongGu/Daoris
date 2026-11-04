using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>Which home an agent verb writes (the alone-path design §1.2).</summary>
public enum AgentHome
{
    /// <summary>The home <c>DAORIS_HOME</c> names, whatever <c>DAORIS_HARNESS_CONFIG</c> names: the rules and their proposals.</summary>
    Daoris,

    /// <summary>The wiring file's home: the one <c>DAORIS_HARNESS_CONFIG</c> names where it is set, acted on alone, else <c>DAORIS_HOME</c>.</summary>
    Wiring,
}
