using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What a choice's reach is: the lines the terminal says, each repository's row, and the refusal where one cannot carry the version.</summary>
public sealed record WorkflowReached(IReadOnlyList<string> Lines, IReadOnlyList<WorkflowReach> Rows, string? Refusal);
