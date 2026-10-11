using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>workflow</c> kind's writer (WORKFLOW1h-a, D157, design §8.1): a whole version of a workflow, new or an edit on a
/// base version, for a repository or a workspace, and optionally its choice there. Its <c>steps</c> are written as nested
/// JSON, the first kind to carry any. Only the shape is judged here; the editor's validation, the base's staleness and what
/// the steps need are the driver's to judge. Changes, a kind and an import are WORKFLOW1h2's, so they are refused by name.
/// </summary>
public sealed partial class HelpProposalBox
{
    /// <summary>A workflow's id as the workflow store takes it: lower-case letters, digits and dashes, at most 40.</summary>
    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowIdShape();

    /// <summary>
    /// Write one proposal of a workflow's whole version. <paramref name="steps"/> is the design's JSON list of step objects;
    /// <paramref name="baseVersion"/> the version an edit is made on, absent for a new workflow; <paramref name="choose"/> asks
    /// that the scope also use this workflow.
    /// </summary>
    public (string? Id, string Message) ProposeWorkflow(
        string? scope, string? workflow, int? baseVersion, string? steps, bool choose,
        string? changes, string? kind, string? import, string why, string? session, DateTimeOffset at)
    {
        var scopeName = Blank(scope);
        var workflowId = Blank(workflow);
        if (string.IsNullOrWhiteSpace(why)) return NoReason;
        var notYet = new (string Name, string? Value)[] { ("changes", changes), ("kind", kind), ("import", import) }
            .FirstOrDefault(field => Blank(field.Value) is not null).Name;

        JsonElement stepList = default;
        var refused =
            notYet is not null ? $"`{notYet}` is not proposed yet: propose the whole `steps`."
            : scopeName is null ? "a workflow proposal names its scope: the repository or workspace it applies to."
            : Word(scopeName, "a scope")
              ?? (workflowId is null ? "a workflow proposal names its workflow, by its id."
                 : !WorkflowIdShape().IsMatch(workflowId) || workflowId.Length > 40
                    ? $"`{workflowId}` is not a workflow's id — lower-case letters, digits and dashes, at most 40, such as `docs-to-pr`."
                 : workflowId == "current" ? "`current` names Current, the workflow drawn from the rules as they stand, so no saved workflow takes it."
                 : baseVersion is < 1 ? "the base version is a whole number from 1."
                 : Steps(steps, out stepList));
        if (refused is not null) return (null, $"{Capital(refused)} Nothing was proposed.");

        return Write(writer =>
        {
            writer.WriteString("kind", "workflow");
            writer.WriteString("scope", scopeName);
            writer.WriteString("workflow", workflowId);
            if (baseVersion is { } baseNumber) writer.WriteNumber("base", baseNumber);
            else writer.WriteNull("base");
            writer.WriteBoolean("choose", choose);
            writer.WritePropertyName("steps");
            stepList.WriteTo(writer);
        }, why, session, at);
    }

    /// <summary>Why <c>steps</c> is not a non-empty JSON list of objects, or null with the list.</summary>
    private static string? Steps(string? text, out JsonElement list)
    {
        list = default;
        if (string.IsNullOrWhiteSpace(text)) return "a workflow proposal carries its `steps`, a JSON list of step objects.";
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array) return "`steps` is a JSON list of step objects.";
            if (root.GetArrayLength() == 0) return "`steps` is a list of at least one step.";
            if (root.EnumerateArray().Any(step => step.ValueKind != JsonValueKind.Object)) return "each of `steps` is a step, a JSON object — not a bare value.";
            list = root.Clone();
            return null;
        }
        catch (JsonException)
        {
            return "`steps` is not valid JSON: send it as a JSON list of step objects.";
        }
    }
}
