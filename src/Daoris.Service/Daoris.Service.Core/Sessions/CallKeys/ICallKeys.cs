using System.Text.Json.Nodes;

namespace Daoris.Knowledge;

/// <summary>
/// One agent's key on an MCP call's <c>_meta</c> and how its lineage is read from it (the agent-provider design §3.3, §6, O1-F17): one
/// file per agent, read by <see cref="CallLineage"/>'s one ordered list. The service knows an agent nowhere else but D23's default.
/// </summary>
public interface ICallKeys
{
    /// <summary>The agent the key is written by, as the driver names its door.</summary>
    string Door { get; }

    /// <summary>The <c>_meta</c> key its lineage is read from.</summary>
    string Key { get; }

    /// <summary>The lineage the key's value carries: what is not shaped as an id is named malformed, never kept; null where it names nothing.</summary>
    CallLineage? Read(JsonNode value);
}
