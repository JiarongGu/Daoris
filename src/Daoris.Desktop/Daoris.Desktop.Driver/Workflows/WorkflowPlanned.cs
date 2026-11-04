using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A version planned against its base: the file after it, the version read back, and whether it says what the base does.</summary>
public sealed record WorkflowPlanned(WorkflowAddPlan Plan, NamedVersion? Next, IReadOnlyList<NamedStep>? NextSteps, bool Same);
