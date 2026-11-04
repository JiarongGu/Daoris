using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One saved workflow as the step editor lists it: the newest version's schema, digest and line, and the places that choose it.</summary>
/// <param name="Schema">1 for steps, 2 for a graph; null where the newest version does not read here.</param>
public sealed record WorkflowListed(
    string Id, string? Name, string? Problem, IReadOnlyList<int> Versions, int? Schema, string? Digest, string? Line, string? Pipeline,
    IReadOnlyList<WorkflowScope> Chosen);
