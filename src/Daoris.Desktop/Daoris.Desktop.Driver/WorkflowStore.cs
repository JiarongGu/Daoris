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
/// Where named workflows live and how a version is added (WORKFLOW1d, D157 point 5, the workflow design §2.4–§2.6): one file
/// per workflow, <c>&lt;home&gt;/workflows/&lt;id&gt;.json</c>, the home's (D63), written atomically, BOM-less and LF, and
/// machine-local. A version is whole and never edited: saving adds the next number, keeping the file's other fields and every
/// version as written, then the newest 20 and every version a kept run names.
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>planAddVersion</c> (<c>namedworkflows.ts</c>) and its door (<c>workflowdoor.ts</c>): the plan is held
/// to this suite's tests' <c>fixtures/workflow-named.json</c> <c>store</c>, cell for cell. A run binds a named workflow's newest
/// version at its first start (WORKFLOW1e, <see cref="WorkflowRunBindings"/>), which every gate reads (WORKFLOW1f, <see cref="WorkflowProcesses"/>); the
/// screen's editor (WORKFLOW1g) and Ask Daoris (WORKFLOW1h) write through here.
/// </remarks>
public static class WorkflowStore
{
    /// <summary>The folder under the home that holds the workflows.</summary>
    public const string Folder = "workflows";

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The folder of a home's workflows.</summary>
    public static string FolderOf(string home) => Path.Combine(home, Folder);

    /// <summary>A workflow's file: its id, which <see cref="WorkflowNamed.IsId"/> has passed, so it names no path.</summary>
    public static string PathOf(string home, string id) =>
        WorkflowNamed.IsId(id) ? Path.Combine(FolderOf(home), id + ".json") : throw new DriverException(WorkflowNamed.IdProblem(id)!);

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
            if ((WorkflowNamed.IdProblem(add.Id) ?? WorkflowNamed.NameProblem(add.Name)) is { } problem) return Refused(problem);
            version["version"] = 1;
            var first = WorkflowNamed.ReadVersion(Element(version), 1, []);
            return first.Problem is not null
                ? Refused(first.Problem)
                : new WorkflowAddPlan(new JsonObject { ["id"] = add.Id, ["name"] = add.Name, ["versions"] = new JsonArray(version) }, 1, null);
        }

        var read = WorkflowNamed.Read(Element(file));
        if (read.Problem is not null) return Refused($"nothing was written: the workflow's file cannot be read — {read.Problem}");
        if (read.Id != add.Id) return Refused($"nothing was written: the file holds the workflow `{read.Id}`, not `{add.Id}`.");
        var number = read.Versions[^1].Version + 1;
        version["version"] = number;
        var candidate = WorkflowNamed.ReadVersion(Element(version), number, [.. read.Versions.Where(each => each.Steps is not null)]);
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

    /// <summary>A workflow's file under the home, as found: null where there is none; its problem where it is not JSON.</summary>
    public static WorkflowFileFound? Load(string home, string id)
    {
        var path = PathOf(home, id);
        if (!File.Exists(path)) return null;
        JsonNode? file;
        try
        {
            file = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return new WorkflowFileFound(id, null, new NamedWorkflowRead(null, null, [], $"`{id}.json` is not readable JSON."));
        }

        var read = file is null ? WorkflowNamed.Read(default) : WorkflowNamed.Read(Element(file));
        if (read.Problem is null && read.Id != id)
        {
            read = read with { Problem = $"`{id}.json` holds the workflow `{read.Id}`; a workflow's file is named by its id." };
        }

        return new WorkflowFileFound(id, file, read);
    }

    /// <summary>Every workflow under the home, by its file's name in ordinal order: each file named as an id, read or not.</summary>
    public static IReadOnlyList<WorkflowFileFound> List(string home)
    {
        var folder = FolderOf(home);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(WorkflowNamed.IsId)
                .Order(StringComparer.Ordinal)
                .Select(id => Load(home, id!)!),
        ];
    }

    /// <summary>
    /// A version added and written, atomically: the plan, then the file whole. Refused, naming why, where the plan writes nothing.
    /// </summary>
    /// <param name="kept">The versions a kept run names; absent, those this home's runs name (WORKFLOW1e, <see cref="WorkflowRunBindings.KeptVersions(string, string)"/>).</param>
    /// <returns>The version's number.</returns>
    public static int Add(string home, WorkflowVersionAdded add, IReadOnlyCollection<int>? kept = null)
    {
        if (WorkflowNamed.IdProblem(add.Id) is { } idProblem) throw new DriverException(idProblem);
        var found = Load(home, add.Id);
        if (found is { File: null }) throw new DriverException($"nothing was written: the workflow's file cannot be read — {found.Read.Problem}");
        var plan = PlanAdd(found?.File, add, kept ?? WorkflowRunBindings.KeptVersions(home, add.Id));
        if (plan.Problem is not null) throw new DriverException(plan.Problem);
        Directory.CreateDirectory(FolderOf(home));
        AtomicFile.WriteText(PathOf(home, add.Id), Text(plan.Result!));
        return plan.Version!.Value;
    }

    /// <summary>A workflow's file as written: indented by two, LF, with a last newline, its words as written.</summary>
    public static string Text(JsonNode file) => file.ToJsonString(Indented) + "\n";

    private static JsonElement Element(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
