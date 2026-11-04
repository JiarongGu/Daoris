using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>Where a new version comes from, resolved: steps or a graph, its name, and how the terminal says its source.</summary>
public sealed record WorkflowVersionSource(string Description, string Name, JsonArray? Steps, JsonObject? Graph, WorkflowGraphPreset? GraphPreset)
{
    /// <summary>The default it copies, which the version keeps as <c>from</c>; null for every other source.</summary>
    public WorkflowFrom? From { get; init; }
}
