using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A choice made (or found already made): the edit, its words, and the repositories it reaches.</summary>
/// <param name="Refusal">Why nothing was written, in the twins' words; null where the edit stands.</param>
/// <param name="Changed">Whether the edit changes what is kept; false is "nothing was written".</param>
public sealed record WorkflowUsed(
    WorkflowChoiceEdit? Edit, bool Changed, IReadOnlyList<string> Lines, IReadOnlyList<WorkflowReach> Reach, string? Refusal);
