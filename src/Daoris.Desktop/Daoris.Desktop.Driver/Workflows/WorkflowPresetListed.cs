using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A preset built in, with the line composed from its steps or graph.</summary>
public sealed record WorkflowPresetListed(string Id, string Name, bool Graph, string Line)
{
    /// <summary>A graph preset's graph as a version saves it, which the step editor starts from; null for one of steps.</summary>
    public JsonNode? Body { get; init; }

    /// <summary>The default itself, for its purpose, order, whether the set-up offers it and its delivery; null for a preset that is no default.</summary>
    public WorkflowDefault? Default { get; init; }
}
