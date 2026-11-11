using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What only a workflow save's card draws: the graph the press saves, drawn as the editor draws it, and its changes against the version
/// it replaces, each by what changed, which the page words; null changes for a new workflow, which replaces none.
/// </summary>
public sealed record PressWorkflow(JsonElement Graph, IReadOnlyList<GraphChange>? Changes);
