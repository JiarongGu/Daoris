using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One version as saved: its steps or its graph, its composed line, and the graph's problems.</summary>
public sealed record WorkflowShown(
    string Id, string Name, int Version, bool Newest, IReadOnlyList<int> Versions, string? At, string? Door, string? Digest, int Schema,
    string Line, JsonNode Body, IReadOnlyList<WireProblem> Problems);
