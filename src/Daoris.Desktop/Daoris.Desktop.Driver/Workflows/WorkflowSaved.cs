using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What a save did, or why it did not.</summary>
public sealed record WorkflowSaved(
    string Id, bool Done, string? Refused, string? Message, int? Version, bool Written, bool Appended,
    IReadOnlyList<WorkflowScope> Scopes, IReadOnlyList<WorkflowReach> Reach)
{
    /// <summary>Each refusal of a graph refused, by its code at the step or wire it names (WORKFLOWEDIT1b); empty for every other answer.</summary>
    public IReadOnlyList<WireProblem> Problems { get; init; } = [];
}
