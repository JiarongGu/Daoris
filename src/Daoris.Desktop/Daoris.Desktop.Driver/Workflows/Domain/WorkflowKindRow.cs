using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One kind of step: how much of it this build runs, the limit it is said with (a <see cref="WorkflowLimits"/> code), and its fields.</summary>
/// <param name="Runtime">One of <see cref="WorkflowRuntime"/>'s, or <see cref="WorkflowKindTable.None"/>: never offered.</param>
public sealed record WorkflowKindRow(string Kind, string Runtime, string? Limit, IReadOnlyList<WorkflowKindField> Fields);
