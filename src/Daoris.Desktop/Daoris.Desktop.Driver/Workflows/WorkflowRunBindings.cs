using System.Text.Json;
using static Daoris.Driver.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

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
/// <para><b>What reads it</b>: the gate (WORKFLOW1f, <see cref="WorkflowProcesses"/>' <c>Read</c>), which reads a named run's
/// version from the store (<see cref="WorkflowStore.Load"/>) by <see cref="WorkflowRunBinding.Version"/>,
/// holds a run whose <see cref="WorkflowRunBinding.Problem"/> is set, checks the changed paths against
/// <see cref="WorkflowRunBinding.Paths"/> where the kind's workflow lowers the person's part (§4.4) until the person's
/// <see cref="WorkflowRunBinding.Kept"/>, and reads a run under Current, or one with no binding, live from the rules, as every gate
/// does today.</para>
///
/// <para>The versions a kept run names (<see cref="KeptVersions(IEnumerable{JsonElement}, string)"/>) are held to the table's
/// <c>kept</c> rows, and <c>daoris driver workflow edit</c> keeps them through here since PUSH1f2b (<see cref="DriverWorkflowsCommand"/>). The file is the driver's
/// alone to write. Machine-local, never on the wire (D47 §4).</para>
///
/// <para><b>Over ports</b> (STRUCT3b, D166): the runs' use cases, written against <see cref="IWorkflowRunStore"/> and
/// <see cref="IWorkflowStore"/>; the file and its format are behind them. A caller that holds only the home reaches
/// them by it, as every module did before.</para>
/// </remarks>
public static class WorkflowRunBindings
{
    /// <summary>The folder under the home's workflows that holds the runs.</summary>
    public const string Folder = "runs";

    /// <summary>The folder of a home's runs.</summary>
    public static string FolderOf(string home) => Path.Combine(WorkflowStore.FolderOf(home), Folder);

    /// <summary>A run's id as a file may be named by it: a quest's id, letters, digits, dashes and underscores, at most 64.</summary>
    public static bool IsRunId(string? run) => run is not null && SafeNames.IsPlainId(run);

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
        IReadOnlyList<WorkflowPlugin> plugins, DateTimeOffset at) =>
        Plan(config, WorkflowPorts.Store(home), run, repository, workspace, ask, task, plugins, at);

    /// <summary>The binding a run's first start makes, its named workflow read from <paramref name="store"/>.</summary>
    public static WorkflowRunBinding Plan(
        DriverConfig config, IWorkflowStore store, string run, string repository, string? workspace, string? ask, WorkflowTaskChoice? task,
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

        if (!WorkflowNamed.IsId(selected.Workflow) || store.Load(selected.Workflow) is not { } found)
        {
            return binding with { Problem = $"no workflow `{selected.Workflow}` is saved here." };
        }

        if (found.Read.Problem is { } unread) return binding with { Problem = $"`{selected.Workflow}` cannot be read: {unread}" };
        var newest = found.Read.Versions[^1];
        // FABRIC-PIPELINE1a (the pipeline design §2.1): a route naming a verification pipeline is judged at its bind, kept with why.
        if (found.Read.Verifies) return binding with { Version = newest.Version, Problem = WorkflowSelection.VerificationElsewhere(selected.Workflow) };
        // A graph version (schema 2) binds its graph's digest (FABRIC-GRAPH1b); it is held as unrun where the process reads it.
        return newest.Steps is null && newest.Graph is null
            ? binding with { Version = newest.Version, Problem = $"`{selected.Workflow}` v{newest.Version} cannot be read here: {newest.Problem}" }
            : binding with { Version = newest.Version, Digest = newest.Digest };
    }

    /// <summary>
    /// Keep a run's binding where none is (design §4.5): a run already bound keeps the version it started on. Written atomically,
    /// BOM-less and LF.
    /// </summary>
    /// <returns>Whether it was written: false where the run was bound already.</returns>
    public static bool Bind(string home, WorkflowRunBinding binding) => WorkflowPorts.Runs(home).Bind(binding);

    /// <summary>
    /// The person's <i>Keep</i> kept on a run's binding (WORKFLOW1f, design §4.4), atomically, every other field as it was read: the
    /// binding itself is never bound again. A run with no binding here keeps nothing.
    /// </summary>
    /// <returns>The binding with it, or null where the run has none here.</returns>
    public static WorkflowRunBinding? Keep(string home, string run, WorkflowKept kept) => Keep(WorkflowPorts.Runs(home), run, kept);

    /// <summary>The person's <i>Keep</i> kept on a run's binding through <paramref name="runs"/>, its moment to the second.</summary>
    /// <returns>The binding with it, or null where the run has none there.</returns>
    public static WorkflowRunBinding? Keep(IWorkflowRunStore runs, string run, WorkflowKept kept)
    {
        if (runs.Read(run) is not { } binding) return null;
        var with = binding with { Kept = kept with { At = ToTheSecond(kept.At) } };
        runs.Write(run, with);
        return with;
    }

    /// <summary>Whether a run is bound here.</summary>
    public static bool Bound(string home, string run) => WorkflowPorts.Runs(home).Bound(run);

    /// <summary>
    /// Whether any run here is bound to a named workflow in <paramref name="repository"/> (WORKFLOW1f): where a gate cannot read
    /// which run a piece of work is, it holds only where one might govern it. An unreadable file cannot rule out a named binding.
    /// </summary>
    public static bool AnyNamed(string home, string repository) => WorkflowPorts.Runs(home).AnyNamed(repository);

    /// <summary>A run's binding, or null where it has none here, or its file does not read as one.</summary>
    public static WorkflowRunBinding? Read(string home, string run) => WorkflowPorts.Runs(home).Read(run);

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
    public static IReadOnlyList<int> KeptVersions(string home, string workflow) => WorkflowPorts.Runs(home).KeptVersions(workflow);

    /// <summary>
    /// What the look says of a binding (design §4.1, *the run says why*): the workflow and its version, and the level that chose it;
    /// or why the workflow chosen could not be read here.
    /// </summary>
    public static string Said(WorkflowRunBinding binding, string quest)
    {
        var where = $"workflow  #{quest} → {binding.Repository}";
        var follows = binding.IsCurrent ? "Current" : binding.Version is { } version ? $"`{binding.Workflow}` v{version}" : $"`{binding.Workflow}`";
        return binding.Problem is { } problem
            ? $"{where}: its run was bound to {follows}, {WorkflowRunBinding.Why(binding)}, which cannot start it: {problem}"
            : $"{where}: its run follows {follows}, {WorkflowRunBinding.Why(binding)}.";
    }

    private static DateTimeOffset ToTheSecond(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, TimeSpan.Zero);
    }
}
