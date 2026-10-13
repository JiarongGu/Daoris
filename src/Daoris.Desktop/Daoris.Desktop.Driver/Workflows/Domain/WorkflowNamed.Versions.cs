using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A version to add to a workflow's file: its steps as saved, when, and through which door; the name only for a new one.</summary>
/// <param name="Door"><c>screen</c>, <c>terminal</c> or <c>ask-daoris:&lt;proposal&gt;</c>: never <i>the person</i> (design §2.5, D156 §4.1).</param>
/// <param name="At">The moment, as ISO 8601 writes it.</param>
public sealed record WorkflowVersionAdded(string Id, string? Name, string Door, string At, JsonNode? Steps);

/// <summary>A version planned: the file after it, and the version's number; or the problem that writes nothing.</summary>
public sealed record WorkflowAddPlan(JsonObject? Result, int? Version, string? Problem);

/// <summary>A workflow's file as found under the home: the file as parsed (null where absent or not JSON) and what it reads as.</summary>
public sealed record WorkflowFileFound(string Id, JsonNode? File, NamedWorkflowRead Read);

/// <summary>
/// How a version is added to a named workflow's file (WORKFLOW1d, D157 point 5, the workflow design §2.4–§2.6), planned and not
/// written: the store's use case (<see cref="WorkflowStore"/>'s <c>Add</c>) writes the plan through its port. A version is whole and
/// never edited: saving adds the next number, keeping the file's other fields and every version as written, then the newest 20
/// and every version a kept run names. Pure, so Workflows' Domain holds it (STRUCT3b).
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>planAddVersion</c> (<c>namedworkflows.ts</c>): the plan is held to the driver suite's
/// <c>fixtures/workflow-named.json</c> <c>store</c>, cell for cell.
/// </remarks>
public static partial class WorkflowNamed
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// A version added to a workflow's file, planned and not written (design §2.6): the next number after the last, the file's
    /// other fields and every version kept as written, a version that does not read here included, then the newest 20 kept with
    /// every version a kept run names. A file that does not read is never written over, and steps that do not read write nothing.
    /// </summary>
    /// <param name="file">The file as it stands, or null for a new workflow.</param>
    /// <param name="kept">The versions a kept run names.</param>
    public static WorkflowAddPlan PlanAdd(JsonNode? file, WorkflowVersionAdded add, IReadOnlyCollection<int> kept)
    {
        static WorkflowAddPlan Refused(string problem) => new(null, null, problem);
        var version = new JsonObject { ["version"] = 0, ["at"] = add.At, ["door"] = add.Door, ["steps"] = add.Steps?.DeepClone() };
        if (file is null)
        {
            if ((IdProblem(add.Id) ?? NameProblem(add.Name)) is { } problem) return Refused(problem);
            version["version"] = 1;
            var first = ReadVersion(ElementOf(version), 1, []);
            return first.Problem is not null
                ? Refused(first.Problem)
                : new WorkflowAddPlan(new JsonObject { ["id"] = add.Id, ["name"] = add.Name, ["versions"] = new JsonArray(version) }, 1, null);
        }

        var read = Read(ElementOf(file));
        if (read.Problem is not null) return Refused($"nothing was written: the workflow's file cannot be read — {read.Problem}");
        if (read.Id != add.Id) return Refused($"nothing was written: the file holds the workflow `{read.Id}`, not `{add.Id}`.");
        var number = read.Versions[^1].Version + 1;
        version["version"] = number;
        var candidate = ReadVersion(ElementOf(version), number, [.. read.Versions.Where(each => each.Steps is not null)]);
        if (candidate.Problem is not null) return Refused(candidate.Problem);

        var result = file.DeepClone().AsObject();
        var versions = result["versions"]!.AsArray();
        versions.Add(version);
        var numbers = read.Versions.Select(each => each.Version).Append(number).ToList();
        while (versions.Count > WorkflowKindTable.KeptVersions)
        {
            var oldest = numbers.FindIndex(each => each != number && !kept.Contains(each));
            if (oldest == -1) break;
            versions.RemoveAt(oldest);
            numbers.RemoveAt(oldest);
        }

        return new WorkflowAddPlan(result, number, null);
    }

    /// <summary>A workflow's file as written: indented by two, LF, with a last newline, its words as written.</summary>
    public static string Text(JsonNode file) => file.ToJsonString(Indented) + "\n";

    /// <summary>A file's node as the readers read it, an element of its own.</summary>
    internal static JsonElement ElementOf(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
