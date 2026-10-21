namespace Daoris.Driver;

/// <summary>
/// A run's unit for the node runtime (FABRIC-CUTOVER1c; D190 point 2, the cutover design §2.1): <c>run:&lt;run&gt;</c>, the unit of its own node
/// record, projected for the evaluation alone and never listed by the inventory.
/// </summary>
public static class RunUnits
{
    public const string Prefix = "run:";

    public static string IdOf(string run) => Prefix + run;

    public static bool Is(string unit) => unit.StartsWith(Prefix, StringComparison.Ordinal);
}

/// <summary>
/// A run bound to a named workflow's graph version, as arming reads it (FABRIC-CUTOVER1c; D190 points 1 and 3, the cutover design §1, §3): its
/// binding, the version it is bound to (unreadable where it no longer reads, and the run is held whole), and where its record says it ended.
/// </summary>
/// <param name="Asked">Whether a pass is owed it though it ended: its chain's start asked one (<see cref="NodeRuntime.Return"/>), or this is the
/// process's first look.</param>
public sealed record GraphRun(WorkflowRunBinding Binding, DecodedVersion Version, RunEnd? Ended, bool Asked = false)
{
    public string Run => Binding.Run;

    public string Unit => RunUnits.IdOf(Binding.Run);

    /// <summary>Whether a binding names a graph version: a graph version's identity is its graph's own digest (FABRIC-GRAPH1b), a steps version's never.</summary>
    public static bool Names(WorkflowRunBinding binding) =>
        !binding.IsCurrent && binding.Version is not null && binding.Digest is { } digest && digest.StartsWith("gph-", StringComparison.Ordinal);
}

/// <summary>
/// The home's runs bound to graph versions (FABRIC-CUTOVER1c; the cutover design §3's arming): <c>&lt;home&gt;/workflows/runs/</c> listed, each
/// binding read only when its file is new or changed since this process read it, and a graph run's version and end read once and kept. A
/// home with no runs folder costs one stat; a run bound to Current or a steps version is D157's, and is never one.
/// </summary>
/// <remarks>Read from the plan's look and link 8 alike, so every read holds one lock.</remarks>
public sealed class GraphRuns(NodeRecordFiles records)
{
    private sealed record Kept(DateTime Written, long Length, WorkflowRunBinding? Binding);

    private sealed record Version(DateTime? Written, long Length, DecodedVersion Decoded);

    private readonly object _lock = new();

    private readonly Dictionary<string, Kept> _bindings = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Version> _versions = new(StringComparer.Ordinal);

    private readonly Dictionary<string, RunEnd?> _ends = new(StringComparer.Ordinal);

    /// <summary>How many binding files this process read: what a look over unchanged files adds nothing to.</summary>
    public int Reads { get; private set; }

    /// <summary>Every run here bound to a graph version, in run order.</summary>
    public IReadOnlyList<GraphRun> Read()
    {
        lock (_lock)
        {
            var folder = WorkflowRunBindings.FolderOf(records.Home);
            if (!Directory.Exists(folder))
            {
                _bindings.Clear();
                return [];
            }

            var runs = new List<GraphRun>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            FileInfo[] files;
            try
            {
                files = new DirectoryInfo(folder).GetFiles("*.json");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return [];
            }

            foreach (var file in files.OrderBy(file => file.Name, StringComparer.Ordinal))
            {
                seen.Add(file.FullName);
                if (!_bindings.TryGetValue(file.FullName, out var kept) || kept.Written != file.LastWriteTimeUtc || kept.Length != file.Length)
                {
                    WorkflowRunBinding? binding;
                    try
                    {
                        binding = WorkflowRunFiles.Parse(File.ReadAllText(file.FullName));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    Reads++;
                    _bindings[file.FullName] = kept = new Kept(file.LastWriteTimeUtc, file.Length, binding);
                }

                if (kept.Binding is not { } bound || !GraphRun.Names(bound)) continue;
                runs.Add(new GraphRun(bound, VersionOf(bound), EndOf(bound.Run)));
            }

            foreach (var gone in _bindings.Keys.Where(path => !seen.Contains(path)).ToList()) _bindings.Remove(gone);
            return runs;
        }
    }

    /// <summary>The run <paramref name="run"/> where it is bound here to a graph version, or null.</summary>
    public GraphRun? Find(string run) => Read().FirstOrDefault(each => string.Equals(each.Run, run, StringComparison.Ordinal));

    /// <summary>The runtime wrote <paramref name="ended"/> on the run's record, or took its end away: kept, so its record is not read again for it.</summary>
    public void Ended(string run, RunEnd? ended)
    {
        lock (_lock) _ends[run] = ended;
    }

    /// <summary>
    /// The graph version a binding names, as its workflow's file holds it (D190 point 1: no run is governed by a decoded graph, so nothing here
    /// decodes): read again only where that file changed, so a version that stops reading (its file removed or damaged, its version gone or not
    /// the one the run started on) holds the run whole until it reads again (D157 §5.2).
    /// </summary>
    private DecodedVersion VersionOf(WorkflowRunBinding binding)
    {
        var number = binding.Version ?? 0;
        DecodedVersion Unread(string why) => new(number, null, null, null, why);
        if (!WorkflowNamed.IsId(binding.Workflow)) return Unread($"`{binding.Workflow}` is not a workflow's id.");
        var file = new FileInfo(WorkflowStore.PathOf(records.Home, binding.Workflow));
        DateTime? written = file.Exists ? file.LastWriteTimeUtc : null;
        var length = file.Exists ? file.Length : -1;
        if (_versions.TryGetValue(binding.Run, out var kept) && kept.Written == written && kept.Length == length) return kept.Decoded;
        var read = new WorkflowFiles(records.Home).Load(binding.Workflow)?.Read;
        var stored = read?.Versions.FirstOrDefault(each => each.Version == number);
        var version = read is null ? Unread($"no workflow `{binding.Workflow}` is saved here any more.")
            : read.Problem is { } problem ? Unread(problem)
            : stored is null ? Unread($"the run `{binding.Run}` is bound to v{number}, which the workflow's file no longer holds.")
            : stored.Problem is { } broken ? Unread(broken)
            : stored is not { Graph: { } graph, Digest: { } digest } || digest != binding.Digest
                ? Unread($"the run `{binding.Run}` is bound to v{number} at `{binding.Digest}`, which the file's v{number} is not: a version is never edited.")
                : new DecodedVersion(number, digest, graph, digest, null);
        _versions[binding.Run] = new Version(written, length, version);
        return version;
    }

    /// <summary>Where the run's record says it ended, read once a process, then kept as the runtime writes it.</summary>
    private RunEnd? EndOf(string run)
    {
        if (_ends.TryGetValue(run, out var ended)) return ended;
        return _ends[run] = records.Read(RunUnits.IdOf(run))?.Ended;
    }
}

/// <summary>
/// What a governed run's nodes observe this look (FABRIC-CUTOVER1c; the cutover design §2.2, §2.4): the run itself, its newest work quest here,
/// the branch its newest session worked, every branch its quests produced, and the go-aheads its own sessions and quests asked.
/// </summary>
/// <param name="Run">The run as arming read it.</param>
/// <param name="Self">The run as a unit of its own, for its own nodes: its go-ahead, task, wait, stop and join.</param>
public sealed record RunScope(GraphRun Run, WorkUnit Self)
{
    /// <summary><c>claimed</c>'s subject: the newest quest of its chain in its repository, never a look's set-up quest; null where none was read.</summary>
    public WorkUnit? Quest { get; init; }

    /// <summary>The run's work quests here, in its chain's order: what its go-aheads and its branches are the run's by.</summary>
    public IReadOnlyList<QuestView> Quests { get; init; } = [];

    /// <summary>
    /// The branch the subjects of its gates, its landing and its plugin's nodes before the landing are: the one its newest work quest's session
    /// worked, found by its records whether its tree stands or not; null where none, or where it is held (two runs produced it).
    /// </summary>
    public WorkUnit? Branch { get; init; }

    /// <summary>Every branch the run's quests produced, its former subjects included: what an answer for one maps back to the run by.</summary>
    public IReadOnlyList<WorkUnit> Branches { get; init; } = [];

    /// <summary>The nodes a wire path leads to from <c>handed</c>: they observe the run's branch at its landing, at the landing's tip.</summary>
    public IReadOnlySet<string> AfterHanded { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The go-aheads on the run's ask that a session serving one of its quests, or a request naming one, asked (§2.4.2), as schema 1 counts them;
    /// null where its ask was not read, and its go-ahead waits.
    /// </summary>
    public IReadOnlyList<GoAheadView>? GoAheads { get; init; }

    /// <summary>The repository's checkout here, and its identity (§B5): where its branches are told and read.</summary>
    public string Root { get; init; } = "";

    public string Identity { get; init; } = "";

    /// <summary>The unit a governed run is told an answer under, mapped back to it (§2.4.1): its branches, former subjects included.</summary>
    public IEnumerable<string> Told => Branches.Select(branch => branch.Id).Concat(Branch is { } branch ? [branch.Id] : []).Distinct(StringComparer.Ordinal);
}

/// <summary>Which subject a run's node observes (the cutover design §2.2's table), by its kind and its place after <c>handed</c>.</summary>
public enum RunSubject
{
    /// <summary><c>claimed</c> and <c>quest.close</c>: the run's newest work quest here.</summary>
    Quest,

    /// <summary>The gates, <c>handed</c>, and a plugin's node before it: the run's branch at the tip read in the pass.</summary>
    Branch,

    /// <summary><c>pushed</c>, <c>pull-request</c>, <c>line</c> and a node after <c>handed</c>: the run's branch at its landing, at the landing's tip.</summary>
    Landing,

    /// <summary>A go-ahead, a person's task, a wait, a stop and a join: the run itself, with no revision.</summary>
    Run,
}

/// <summary>
/// Who owns each quest and branch a pass reads, between Current's graphs and the runs bound to graph versions (FABRIC-CUTOVER1c; the cutover
/// design §2.3): a quest whose run is a graph run is that run's; a branch is a run's where every quest that produced it is of that one run, held
/// where they name two runs and one of them is a graph run, and Current's otherwise. While the work's attribution is not read, no branch of a
/// repository holding an open graph run is Current's. Pure.
/// </summary>
public sealed class RunOwners
{
    private readonly Dictionary<string, QuestView> _quests;

    private readonly Dictionary<string, GraphRun> _runs;

    private readonly Dictionary<string, string> _runOf = new(StringComparer.Ordinal);

    private readonly IReadOnlyList<QuestView> _all;

    private readonly HashSet<string> _unreadIn = new(StringComparer.OrdinalIgnoreCase);

    public RunOwners(IReadOnlyDictionary<string, QuestView> quests, IReadOnlyList<GraphRun> runs, bool attributionRead)
    {
        _quests = new(quests, StringComparer.Ordinal);
        _all = [.. quests.Values];
        _runs = runs.GroupBy(run => run.Run, StringComparer.Ordinal).ToDictionary(same => same.Key, same => same.First(), StringComparer.Ordinal);
        if (!attributionRead)
        {
            foreach (var run in runs.Where(run => run.Ended is null)) _unreadIn.Add(run.Binding.Repository);
        }
    }

    /// <summary>The repositories whose branches no graph governs while the work's attribution is not read: each holds an open graph run.</summary>
    public IReadOnlyCollection<string> Unattributed => _unreadIn;

    /// <summary>The run a quest's work is (D157 §1): its chain's first quest in its repository, else its own id.</summary>
    public string RunOf(QuestView quest)
    {
        if (_runOf.TryGetValue(quest.Id, out var run)) return run;
        return _runOf[quest.Id] = WorkflowRunBindings.RunOf(ReviewGate.ChainOf(_all, quest.Id), quest.To) ?? quest.Id;
    }

    /// <summary>The graph run a quest is of, or null where its run is not bound to a graph version. A home binding none reads no chain.</summary>
    public GraphRun? Of(QuestView quest) => _runs.Count == 0 ? null : _runs.GetValueOrDefault(RunOf(quest));

    /// <summary>The runs whose quests produced a branch unit, each once, by its links; a quest not read is a run of its own.</summary>
    public IReadOnlyList<string> RunsOf(WorkUnit branch) =>
    [
        .. branch.Links.Where(link => link.Relation == WorkRelation.ProducedBy && link.Unit.StartsWith("quest:", StringComparison.Ordinal))
            .Select(link => link.Unit["quest:".Length..])
            .Select(id => _quests.TryGetValue(id, out var quest) ? RunOf(quest) : id)
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>The graph run a branch unit is, where every quest that produced it is of that one run; null otherwise.</summary>
    public GraphRun? Owns(WorkUnit branch) => RunsOf(branch) is [var only] ? _runs.GetValueOrDefault(only) : null;

    /// <summary>
    /// Why no graph governs a branch unit, or null where Current may: it is a graph run's, two runs produced it and one is a graph run's, or
    /// its repository holds an open graph run while the work's attribution was not read.
    /// </summary>
    public string? Keeps(WorkUnit branch)
    {
        if (branch.Kind != WorkUnitKind.Branch || _runs.Count == 0) return null;
        if (branch.Owner.Repository is { } repository && _unreadIn.Contains(repository)) return Unread;
        var runs = RunsOf(branch);
        if (!runs.Any(_runs.ContainsKey)) return null;
        return runs.Count == 1 ? Run : Held;
    }

    /// <summary>A graph run's branch: its graph holds what it wants done on it, and its repository's landing cycle no longer runs there (§2.3).</summary>
    public const string Run = "run";

    /// <summary>Two runs' work on one branch, one of them a graph run's: neither engine governs it, said.</summary>
    public const string Held = "held";

    /// <summary>A branch whose work's attribution was not read in a repository holding an open graph run: never handed to Current on a guess.</summary>
    public const string Unread = "unread";

    /// <summary>What the pass says of a held branch, naming the quests and runs on it.</summary>
    public string HeldSaid(WorkUnit branch)
    {
        var quests = branch.Links.Where(link => link.Relation == WorkRelation.ProducedBy && link.Unit.StartsWith("quest:", StringComparison.Ordinal))
            .Select(link => "#" + link.Unit["quest:".Length..]).Distinct(StringComparer.Ordinal);
        return $"work  {branch.Id}: its branch carries the work of {string.Join(", ", quests)}, quests of two runs ({string.Join(", ", RunsOf(branch).Select(run => $"`{run}`"))}), "
               + "one of them bound to a graph version, so neither governs it and nothing either would do on it starts.";
    }
}
