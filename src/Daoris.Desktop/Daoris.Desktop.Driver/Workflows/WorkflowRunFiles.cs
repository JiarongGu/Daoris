using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The runs' binding files under a home (WORKFLOW1e, D157 point 11; the workflow design §4.5, §5.1): one file per run,
/// <c>&lt;home&gt;/workflows/runs/&lt;run&gt;.json</c>, written once, atomically, BOM-less and LF; the driver's alone to write, and
/// machine-local, never on the wire (D47 §4). The I/O behind <see cref="IWorkflowRunStore"/> and the file's format (STRUCT3b, D166);
/// the layout is <see cref="WorkflowRunBindings.FolderOf"/>'s and <see cref="WorkflowRunBindings.PathOf"/>'s.
/// </summary>
public sealed class WorkflowRunFiles(string home) : IWorkflowRunStore
{
    /// <summary>Whether a run is bound here.</summary>
    public bool Bound(string run) => WorkflowRunBindings.IsRunId(run) && File.Exists(WorkflowRunBindings.PathOf(home, run));

    /// <summary>
    /// A run's binding, or null where it has none here, or its file does not read as one. Read sharing its deletion
    /// (<see cref="AtomicFile.ReadText"/>, BINDRACE1), so a start that lost the race for its run's binding reads the winner's as it lands.
    /// </summary>
    public WorkflowRunBinding? Read(string run)
    {
        if (!Bound(run)) return null;
        try
        {
            return Parse(AtomicFile.ReadText(WorkflowRunBindings.PathOf(home, run)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Keep a run's binding where none is (design §4.5): a run already bound keeps the version it started on. Written atomically,
    /// BOM-less and LF, and moved into place without overwriting (FABRIC-CUTOVER1g, the cutover design §8): starts run beside one
    /// another, and two of one run checked for a binding and then each wrote one, so the second's replaced the first's.
    /// </summary>
    /// <returns>Whether it was written: false where the run was bound already, by an earlier start or by one beside this one.</returns>
    public bool Bind(WorkflowRunBinding binding)
    {
        var path = WorkflowRunBindings.PathOf(home, binding.Run);
        if (File.Exists(path)) return false;
        Directory.CreateDirectory(WorkflowRunBindings.FolderOf(home));
        if (!AtomicFile.TryCreateText(path, ToJson(binding))) return false;
        // This process's own write wakes no landing (FABRIC-END7, EVENT2's rule): the gate's files are heard for another's.
        HomeWrites.Mine(path);
        return true;
    }

    /// <summary>A run's binding written over its file, atomically, and marked as this process's own (FABRIC-END7).</summary>
    public void Write(string run, WorkflowRunBinding binding)
    {
        var path = WorkflowRunBindings.PathOf(home, run);
        AtomicFile.WriteText(path, ToJson(binding));
        HomeWrites.Mine(path);
    }

    /// <summary>
    /// Whether any run here is bound to a named workflow in <paramref name="repository"/> (WORKFLOW1f): where a gate cannot read
    /// which run a piece of work is, it holds only where one might govern it. An unreadable file cannot rule out a named binding.
    /// </summary>
    public bool AnyNamed(string repository)
    {
        var folder = WorkflowRunBindings.FolderOf(home);
        if (!Directory.Exists(folder)) return false;
        foreach (var path in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                var binding = Parse(AtomicFile.ReadText(path));
                if (binding is null || (!binding.IsCurrent
                    && string.Equals(binding.Repository, repository, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Its repository and requirements are unknown until the record can be read.
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The versions of <paramref name="workflow"/> this home's runs name, a verification run's under <c>lines/</c> among them (the pipeline design
    /// §4.3), so a version a kept run binds is never pruned under it; a run's file that does not read names none.
    /// </summary>
    public IReadOnlyList<int> KeptVersions(string workflow)
    {
        var lines = VerifyRuns.All(home).Where(run => string.Equals(run.Workflow, workflow, StringComparison.Ordinal)).Select(run => run.Version).ToList();
        var folder = WorkflowRunBindings.FolderOf(home);
        if (!Directory.Exists(folder)) return [.. lines.Distinct().Order()];
        var runs = new List<JsonElement>();
        foreach (var path in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                // Shared as the run's own read is (BINDRACE1): a version a binding landing now names is still kept.
                using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
                runs.Add(document.RootElement.Clone());
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
            {
                // A run's file that does not read names no version; the workflow keeps its newest as ever.
            }
        }

        return [.. WorkflowRunBindings.KeptVersions(runs, workflow).Concat(lines).Distinct().Order()];
    }

    /// <summary>A binding's file read back: null where it is not JSON, or misses what every binding says.</summary>
    public static WorkflowRunBinding? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (Text(root, "run") is not { } run || Text(root, "repository") is not { } repository || Text(root, "workspace") is not { } workspace
                || Text(root, "workflow") is not { } workflow || Text(root, "level") is not { } level
                || Moments.Read(Text(root, "at")) is not { } at)
            {
                return null;
            }

            // ROLESTEP1b: a step's role that does not read leaves the binding unread, so no start takes another role in its place.
            if (RolesOf(root) is not { } roles) return null;

            return new WorkflowRunBinding(run, repository, workspace, workflow, level, at)
            {
                Ask = Text(root, "ask"),
                Version = root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number
                          && version.TryGetInt32(out var number) ? number : null,
                Digest = Text(root, "digest"),
                Kind = Text(root, "kind"),
                Label = Text(root, "label"),
                Paths = root.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Array
                    ? [.. paths.EnumerateArray().Where(path => path.ValueKind == JsonValueKind.String).Select(path => path.GetString()!)]
                    : [],
                Undeclared = Text(root, "undeclared"),
                Problem = Text(root, "problem"),
                Land = root.TryGetProperty("land", out var land) && land.ValueKind == JsonValueKind.Object
                    ? land.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.String).ToDictionary(each => each.Name, each => each.Value.GetString()!)
                    : LandNodes.NoResolved,
                // WORKFLOW1f: the person's Keep, whole or none; a binding from before has none.
                Kept = root.TryGetProperty("kept", out var kept) && kept.ValueKind == JsonValueKind.Object && Text(kept, "door") is { } door
                       && Moments.Read(Text(kept, "at")) is { } keptAt
                    ? new WorkflowKept(keptAt, door) { Words = Text(kept, "words") }
                    : null,
                Roles = roles,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A binding's steps' roles as its file holds them: none where it names none, null where one does not read.</summary>
    private static IReadOnlyList<WorkflowStepRole>? RolesOf(JsonElement root)
    {
        if (!root.TryGetProperty("roles", out var roles)) return [];
        if (roles.ValueKind != JsonValueKind.Array) return null;
        var read = new List<WorkflowStepRole>();
        foreach (var each in roles.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Text(each, "step") is not { } step || Text(each, "kind") is not { } kind
                || Text(each, "role") is not { } role || !each.TryGetProperty("revision", out var revision)
                || revision.ValueKind != JsonValueKind.Number || !revision.TryGetInt64(out var number))
            {
                return null;
            }

            read.Add(new WorkflowStepRole(step, kind, role, number));
        }

        return read;
    }

    /// <summary>A binding as its file holds it: what every binding says first, then what this one has, LF, with a last newline.</summary>
    public static string ToJson(WorkflowRunBinding binding) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("run", binding.Run);
        writer.WriteString("repository", binding.Repository);
        writer.WriteString("workspace", binding.Workspace);
        if (binding.Ask is { } ask) writer.WriteString("ask", ask);
        writer.WriteString("at", binding.At.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        writer.WriteString("workflow", binding.Workflow);
        if (binding.Version is { } version) writer.WriteNumber("version", version);
        if (binding.Digest is { } digest) writer.WriteString("digest", digest);
        writer.WriteString("level", binding.Level);
        if (binding.Kind is { } kind) writer.WriteString("kind", kind);
        if (binding.Label is { } label) writer.WriteString("label", label);
        if (binding.Paths.Count > 0)
        {
            writer.WriteStartArray("paths");
            foreach (var path in binding.Paths) writer.WriteStringValue(path);
            writer.WriteEndArray();
        }

        if (binding.Undeclared is { } undeclared) writer.WriteString("undeclared", undeclared);
        if (binding.Problem is { } problem) writer.WriteString("problem", problem);
        if (binding.Land.Count > 0)
        {
            writer.WriteStartObject("land");
            foreach (var (name, value) in binding.Land.OrderBy(each => each.Key, StringComparer.Ordinal)) writer.WriteString(name, value);
            writer.WriteEndObject();
        }

        if (binding.Roles.Count > 0)
        {
            writer.WriteStartArray("roles");
            foreach (var each in binding.Roles)
            {
                writer.WriteStartObject();
                writer.WriteString("step", each.Step);
                writer.WriteString("kind", each.Kind);
                writer.WriteString("role", each.Role);
                writer.WriteNumber("revision", each.Revision);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (binding.Kept is { } kept)
        {
            writer.WriteStartObject("kept");
            writer.WriteString("at", kept.At.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            writer.WriteString("door", kept.Door);
            if (kept.Words is { } words) writer.WriteString("words", words);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    });
}
