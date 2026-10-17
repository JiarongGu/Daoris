using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A version to add to a workflow's file: its steps as saved, when, and through which door; the name only for a new one.</summary>
/// <param name="Door"><c>screen</c>, <c>terminal</c> or <c>ask-daoris:&lt;proposal&gt;</c>: never <i>the person</i> (design §2.5, D156 §4.1).</param>
/// <param name="At">The moment, as ISO 8601 writes it.</param>
/// <param name="Graph">A named graph as the version (schema 2, FABRIC-GRAPH1b), instead of <paramref name="Steps"/>: never both.</param>
public sealed record WorkflowVersionAdded(string Id, string? Name, string Door, string At, JsonNode? Steps, JsonNode? Graph = null);

/// <summary>A version planned: the file after it, and the version's number; or the problem that writes nothing.</summary>
public sealed record WorkflowAddPlan(JsonObject? Result, int? Version, string? Problem);

/// <summary>A workflow's file as found under the home: the file as parsed (null where absent or not JSON) and what it reads as.</summary>
public sealed record WorkflowFileFound(string Id, JsonNode? File, NamedWorkflowRead Read);
