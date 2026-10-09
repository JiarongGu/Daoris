using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A run bound to the workflow that chose it, at its first start in its repository on this machine (WORKFLOW1e, D157 point 11,
/// the workflow design §4.5, §5.1): the workflow and its version, or Current and the graph it drew; the level that chose it,
/// the kind with its label and paths, and when. Kept in <c>&lt;home&gt;/workflows/runs/&lt;run&gt;.json</c>, written once.
/// </summary>
/// <param name="Run">The run's id: its chain's first quest in the repository, which names it on this machine.</param>
/// <param name="Workflow">The workflow's id, or <c>current</c>.</param>
/// <param name="Level">One of <see cref="WorkflowLevels"/>: which level of §4.1's table chose it.</param>
public sealed record WorkflowRunBinding(string Run, string Repository, string Workspace, string Workflow, string Level, DateTimeOffset At)
{
    /// <summary>The ask the chain was asked by, whose choice is the task's; null for none.</summary>
    public string? Ask { get; init; }

    /// <summary>The named workflow's newest version when the run started, which the run keeps; null under Current.</summary>
    public int? Version { get; init; }

    /// <summary>That version's digest; under Current, the version Current drew (its digest too).</summary>
    public string? Digest { get; init; }

    /// <summary>The task's kind, where its workspace declares it.</summary>
    public string? Kind { get; init; }

    /// <summary>The kind's label as it was declared when the run started.</summary>
    public string? Label { get; init; }

    /// <summary>The kind's paths as they were declared when the run started: what WORKFLOW1f checks the changed paths against.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>A kind the task named that its workspace did not declare, read as none.</summary>
    public string? Undeclared { get; init; }

    /// <summary>Why the named workflow chosen could not be read here when the run started: its file gone, or its newest version unreadable.</summary>
    public string? Problem { get; init; }

    /// <summary>Under Current, its steps as <see cref="WorkflowCurrent.ToJson"/> wrote them at the start, for the trace (design §2.6).</summary>
    public IReadOnlyList<string> CurrentSteps { get; init; } = [];

    /// <summary>
    /// The person's <i>Keep</i> (WORKFLOW1f, design §4.4): they kept the kind's workflow for this work though it changed paths outside
    /// the kind's, with when, their words and the door. Null where none was given, which every binding from before reads as.
    /// </summary>
    public WorkflowKept? Kept { get; init; }

    /// <summary>Whether the run follows Current, which every gate reads live, as today (design §2.6).</summary>
    public bool IsCurrent => Workflow == WorkflowSelection.Current;
}

/// <summary>The person's say-so that a run keeps its workflow (WORKFLOW1f, design §4.4): when, at which door, and their words.</summary>
/// <param name="Door"><c>terminal</c> or <c>screen</c> (<see cref="ReviewDoors"/>).</param>
public sealed record WorkflowKept(DateTimeOffset At, string Door)
{
    public string? Words { get; init; }
}

/// <summary>
/// Runs bound to their workflow (WORKFLOW1e, D157 point 11; the workflow design §4.5, §5.1): which run a quest's start belongs to,
/// what chose its workflow, the binding kept once at its first start, and the versions a kept run names, which a workflow's file
/// keeps beyond its newest 20 (§2.6).
/// </summary>
/// <remarks>
/// <para><b>Bound at the first start, on the machine that drives it</b> (§4.5): the selection is resolved then (§4.1,
/// <see cref="WorkflowSelection.Resolve"/>), the newest version of a named workflow taken, and kept; editing the workflow, or the
/// choice, later changes new work only. A run already bound is never bound again. A run that first started before this build
/// is bound at its next start here.</para>
///
/// <para><b>What reads it</b>: the gate (WORKFLOW1f, <see cref="WorkflowProcesses.Read"/>), which reads a named run's version from the
/// store (<see cref="WorkflowStore.Load"/>) by <see cref="WorkflowRunBinding.Version"/>, holds a run whose
/// <see cref="WorkflowRunBinding.Problem"/> is set, checks the changed paths against <see cref="WorkflowRunBinding.Paths"/> where the
/// kind's workflow lowers the person's part (§4.4) until the person's <see cref="WorkflowRunBinding.Kept"/>, and reads a run under
/// Current, or one with no binding, live from the rules, as every gate does today.</para>
///
/// <para>A TWIN, in part, with the CLI's <c>workflowchoice.ts</c>: the versions a kept run names (<see cref="KeptVersions(IEnumerable{JsonElement}, string)"/>)
/// are held to the shared table's <c>kept</c> rows, since <c>daoris driver workflow edit</c> keeps them too. The file is the driver's
/// alone to write. Machine-local, never on the wire (D47 §4).</para>
/// </remarks>
public static partial class WorkflowRunBindings
{
    /// <summary>The folder under the home's workflows that holds the runs.</summary>
    public const string Folder = "runs";

    private static readonly JsonWriterOptions Indented = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The folder of a home's runs.</summary>
    public static string FolderOf(string home) => Path.Combine(WorkflowStore.FolderOf(home), Folder);

    /// <summary>A run's id as a file may be named by it: a quest's id, letters, digits, dashes and underscores, at most 64.</summary>
    public static bool IsRunId(string? run) => run is { Length: > 0 and <= 64 } && RunShape().IsMatch(run);

    /// <summary>A run's file, its id passed by <see cref="IsRunId"/>, so it names no path.</summary>
    public static string PathOf(string home, string run) =>
        IsRunId(run) ? Path.Combine(FolderOf(home), run + ".json") : throw new DriverException($"`{run}` is not a run's id.");

    /// <summary>
    /// The run a chain's work in <paramref name="repository"/> is (design §1, §3.8): named by the chain's first quest there, which
    /// every later quest of the chain in that repository shares; null where the chain has none there.
    /// </summary>
    public static string? RunOf(IReadOnlyList<QuestView> chain, string repository) =>
        chain.FirstOrDefault(quest => string.Equals(quest.To, repository, StringComparison.OrdinalIgnoreCase))?.Id;

    /// <summary>
    /// The task's choice an ask carries (design §4.1 row 1, §4.3): its latest, the latest standing; null where it made none, or the
    /// latest names neither a kind nor a workflow, which is a choice cleared.
    /// </summary>
    public static WorkflowTaskChoice? TaskOf(AskView? ask) =>
        ask?.WorkflowChoices.LastOrDefault() is { } latest && (latest.Kind ?? latest.Workflow) is not null
            ? new WorkflowTaskChoice(latest.Kind, latest.Workflow)
            : null;

    /// <summary>
    /// The binding a run's first start makes (design §4.5): §4.1 resolved for its repository, and the workflow it names read here,
    /// its newest version taken; under Current, the graph Current draws now. A named workflow that cannot be read is kept with
    /// why, never swapped for another: what was chosen is what the run says.
    /// </summary>
    /// <param name="workspace">The repository's workspace, or null for none.</param>
    /// <param name="plugins">The plugins switched on and sound, as Current reads them (<see cref="WorkflowCurrent.PluginsOf"/>).</param>
    public static WorkflowRunBinding Plan(
        DriverConfig config, string home, string run, string repository, string? workspace, string? ask, WorkflowTaskChoice? task,
        IReadOnlyList<WorkflowPlugin> plugins, DateTimeOffset at)
    {
        var selected = WorkflowSelection.Resolve(config, repository, workspace, task);
        var binding = new WorkflowRunBinding(
            run, repository, RemoteTarget.Workspace(workspace), selected.Workflow, selected.Level, ToTheSecond(at))
        {
            Ask = ask,
            Kind = selected.Kind,
            Label = selected.Label,
            Paths = selected.Paths,
            Undeclared = selected.Undeclared,
        };

        if (binding.IsCurrent)
        {
            var current = WorkflowCurrent.Derive(config, repository, workspace, plugins);
            return binding with { Digest = current.Version, CurrentSteps = [.. current.Steps.Select(WorkflowCurrent.ToJson)] };
        }

        if (!WorkflowNamed.IsId(selected.Workflow) || WorkflowStore.Load(home, selected.Workflow) is not { } found)
        {
            return binding with { Problem = $"no workflow `{selected.Workflow}` is saved here." };
        }

        if (found.Read.Problem is { } unread) return binding with { Problem = $"`{selected.Workflow}` cannot be read: {unread}" };
        var newest = found.Read.Versions[^1];
        return newest.Steps is null
            ? binding with { Version = newest.Version, Problem = $"`{selected.Workflow}` v{newest.Version} cannot be read here: {newest.Problem}" }
            : binding with { Version = newest.Version, Digest = newest.Digest };
    }

    /// <summary>
    /// Keep a run's binding where none is (design §4.5): a run already bound keeps the version it started on. Written atomically,
    /// BOM-less and LF.
    /// </summary>
    /// <returns>Whether it was written: false where the run was bound already.</returns>
    public static bool Bind(string home, WorkflowRunBinding binding)
    {
        var path = PathOf(home, binding.Run);
        if (File.Exists(path)) return false;
        Directory.CreateDirectory(FolderOf(home));
        AtomicFile.WriteText(path, ToJson(binding));
        return true;
    }

    /// <summary>
    /// The person's <i>Keep</i> kept on a run's binding (WORKFLOW1f, design §4.4), atomically, every other field as it was read: the
    /// binding itself is never bound again. A run with no binding here keeps nothing.
    /// </summary>
    /// <returns>The binding with it, or null where the run has none here.</returns>
    public static WorkflowRunBinding? Keep(string home, string run, WorkflowKept kept)
    {
        if (Read(home, run) is not { } binding) return null;
        var with = binding with { Kept = kept with { At = ToTheSecond(kept.At) } };
        AtomicFile.WriteText(PathOf(home, run), ToJson(with));
        return with;
    }

    /// <summary>Whether a run is bound here.</summary>
    public static bool Bound(string home, string run) => IsRunId(run) && File.Exists(PathOf(home, run));

    /// <summary>
    /// Whether any run here is bound to a named workflow in <paramref name="repository"/> (WORKFLOW1f): where a gate cannot read
    /// which run a piece of work is, it holds only where one might govern it. An unreadable file cannot rule out a named binding.
    /// </summary>
    public static bool AnyNamed(string home, string repository)
    {
        var folder = FolderOf(home);
        if (!Directory.Exists(folder)) return false;
        foreach (var path in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                var binding = Parse(File.ReadAllText(path));
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

    /// <summary>A run's binding, or null where it has none here, or its file does not read as one.</summary>
    public static WorkflowRunBinding? Read(string home, string run)
    {
        if (!Bound(home, run)) return null;
        try
        {
            return Parse(File.ReadAllText(PathOf(home, run)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
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
                || !DateTimeOffset.TryParse(Text(root, "at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                return null;
            }

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
                CurrentSteps = root.TryGetProperty("current", out var steps) && steps.ValueKind == JsonValueKind.Array
                    ? [.. steps.EnumerateArray().Select(step => step.GetRawText())]
                    : [],
                // WORKFLOW1f: the person's Keep, whole or none; a binding from before has none.
                Kept = root.TryGetProperty("kept", out var kept) && kept.ValueKind == JsonValueKind.Object && Text(kept, "door") is { } door
                       && DateTimeOffset.TryParse(Text(kept, "at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var keptAt)
                    ? new WorkflowKept(keptAt, door) { Words = Text(kept, "words") }
                    : null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A binding as its file holds it: what every binding says first, then what this one has, LF, with a last newline.</summary>
    public static string ToJson(WorkflowRunBinding binding)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Indented))
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
            if (binding.CurrentSteps.Count > 0)
            {
                writer.WriteStartArray("current");
                foreach (var step in binding.CurrentSteps) writer.WriteRawValue(step);
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
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>
    /// The versions of <paramref name="workflow"/> the runs read name (design §2.6), in order, each once: a run whose file is an object
    /// naming that workflow exactly and a whole version from 1. A binding whose version did not read still keeps it.
    /// </summary>
    public static IReadOnlyList<int> KeptVersions(IEnumerable<JsonElement> runs, string workflow) =>
    [
        .. runs
            .Where(run => run.ValueKind == JsonValueKind.Object && Text(run, "workflow") == workflow)
            .Select(run => run.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number
                           && version.TryGetInt32(out var number) && number >= 1 ? number : 0)
            .Where(number => number >= 1)
            .Distinct()
            .Order(),
    ];

    /// <summary>The versions of <paramref name="workflow"/> this home's runs name; a run's file that does not read names none.</summary>
    public static IReadOnlyList<int> KeptVersions(string home, string workflow)
    {
        var folder = FolderOf(home);
        if (!Directory.Exists(folder)) return [];
        var runs = new List<JsonElement>();
        foreach (var path in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                runs.Add(document.RootElement.Clone());
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
            {
                // A run's file that does not read names no version; the workflow keeps its newest as ever.
            }
        }

        return KeptVersions(runs, workflow);
    }

    /// <summary>
    /// What the look says of a binding (design §4.1, *the run says why*): the workflow and its version, and the level that chose it;
    /// or why the workflow chosen could not be read here.
    /// </summary>
    public static string Said(WorkflowRunBinding binding, string quest)
    {
        var where = $"workflow  #{quest} → {binding.Repository}";
        var follows = binding.IsCurrent ? "Current" : binding.Version is { } version ? $"`{binding.Workflow}` v{version}" : $"`{binding.Workflow}`";
        return binding.Problem is { } problem
            ? $"{where}: its run was bound to {follows}, {Why(binding)}, which cannot start it: {problem}"
            : $"{where}: its run follows {follows}, {Why(binding)}.";
    }

    /// <summary>Which level chose a run's workflow, in words (design §4.1): <i>chosen by this repository for Documentation</i>.</summary>
    public static string Why(WorkflowRunBinding binding)
    {
        var kind = binding.Label ?? binding.Kind;
        return binding.Level switch
        {
            WorkflowLevels.Task => $"chosen by its ask{(binding.Ask is { } ask ? $" `#{ask}`" : "")}",
            WorkflowLevels.RepositoryKind => $"chosen by `{binding.Repository}` for {kind}",
            WorkflowLevels.Repository => $"`{binding.Repository}`'s default",
            WorkflowLevels.WorkspaceKind => $"chosen by the workspace `{binding.Workspace}` for {kind}",
            WorkflowLevels.Workspace => $"the workspace `{binding.Workspace}`'s default",
            _ => "since nothing names a workflow",
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset ToTheSecond(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, TimeSpan.Zero);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex RunShape();
}
