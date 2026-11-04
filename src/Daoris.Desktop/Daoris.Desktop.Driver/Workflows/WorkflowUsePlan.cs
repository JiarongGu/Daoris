using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A choice read as a plan of one (nothing written): the judgement, the places it changes, what lowers, what the kinds keep, and the token a press carries.</summary>
public sealed record WorkflowUsePlan(
    WorkflowUsed Used, IReadOnlyList<WorkflowUsePlace> Places, IReadOnlyList<string> Lowers, WorkflowUseKeeps? Keeps, string Expected);
