using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A kind declared or dropped: the edit and its words, or the refusal.</summary>
public sealed record WorkflowKindSet(WorkflowChoiceEdit? Edit, IReadOnlyList<string> Lines, string? Refusal);
