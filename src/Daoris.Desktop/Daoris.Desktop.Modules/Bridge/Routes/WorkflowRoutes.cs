using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// How work moves in a repository or a workspace, the page's `bridge/workflow.ts` (WORKFLOW1b; D157 point 7, the workflow
/// design §2.7, §6.1): its Current workflow, which <see cref="WorkflowCurrent.Derive"/> draws from this machine's driver file,
/// its plugins and its registry exactly as `daoris driver workflow show` reads them; and where a piece of work stands in it
/// (WORKFLOW1c), the runs <see cref="WorkflowRunReader"/> reads. Read-only but two: <c>WORKFLOW_LANDED</c>, the person's press that sets
/// the landed route and accepts the gates it runs unasked (FABRIC-PIPELINE1a); and <c>WORKFLOW_NODE</c>, their skip or stop of a graph run's
/// node (FABRIC-CUTOVER1f).
/// </summary>
public sealed class WorkflowRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    // A repository's Current, or a workspace's: `{repository}` or `{workspace}`, exactly one. A repository's workspace is the
    // registry's, so it waits for the driver as the lines do; one the registry does not hold is read as in no workspace, and
    // the answer says so. A workspace's Current is the file's alone, and beside it each repository the registry holds there,
    // with whether a rule of its own makes it follow something else, or none where the registry is not up yet.
    [DriverRoute("WORKFLOW_CURRENT")]
    private async Task<object?> WorkflowCurrentAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a workflow is drawn for a `repository` or a `workspace` — name one of them.");
        }

        var config = DriverConfig.Load(_loop.ConfigPath);
        // The plugins beside the file, switched on and sound, as the landing's and the terminal's doors read them.
        var plugins = WorkflowCurrent.PluginsOf(PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names));

        if (repository is not null)
        {
            var service = _loop.Service ?? throw NotReady();
            var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
            var known = snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
            var drawn = WorkflowCurrent.Derive(config, repository, known?.Workspace, plugins);
            var start = await StartAsync(config, repository, known?.Workspace, cancellationToken).ConfigureAwait(false);
            return Answer(drawn, repository, RemoteTarget.Workspace(known?.Workspace), registered: known is not null, repositories: null, start);
        }

        var shared = WorkflowCurrent.Derive(config, null, workspace, plugins);
        IReadOnlyList<object>? here = null;
        if (_loop.Service is { } up)
        {
            var snapshot = await up.SnapshotAsync(cancellationToken).ConfigureAwait(false);
            here = [.. snapshot.Repositories
                .Where(each => string.Equals(RemoteTarget.Workspace(each.Workspace), workspace, StringComparison.OrdinalIgnoreCase))
                .OrderBy(each => each.Repository, StringComparer.Ordinal)
                .Select(each => new
                {
                    each.Repository,
                    Own = Shape(WorkflowCurrent.Derive(config, each.Repository, each.Workspace, plugins)) != Shape(shared),
                })];
        }

        return Answer(shared, null, workspace!, registered: null, repositories: here,
            await StartAsync(config, null, workspace, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The start node a Workflow tab draws first (FABRIC-PIPELINE1a, the pipeline design §1.3): <see cref="WorkflowStart.Read"/>'s routes,
    /// each with the pipeline it names and what chose it; the landed route with its pipeline's name, the gates accepted and where they
    /// stand against the line's tip (<see cref="LandedGates.StateOf"/>), the tip and the declaration there where it is to be accepted, or
    /// why the tip could not be read; and the verification pipelines saved here. A workspace has no line, so its route's gates are
    /// accepted repository by repository, and its tip is never read. git is asked only where a repository's route names a pipeline.
    /// </summary>
    private async Task<object> StartAsync(DriverConfig config, string? repository, string? workspace, CancellationToken ct)
    {
        var start = WorkflowStart.Read(config, repository, workspace, WorkflowStore.List(_loop.Home));
        var (root, tip, problem) = repository is not null && start.Landed.Workflow != WorkflowSelection.Current
            ? await TipAsync(config, repository, workspace, ct).ConfigureAwait(false)
            : (null, null, null);
        var state = repository is null && start.Landed.Workflow != WorkflowSelection.Current ? LandedGates.Unread : LandedGates.StateOf(start.Landed, tip);
        var declaration = root is not null && tip is not null && state is LandedGates.Changed or LandedGates.Unaccepted
            ? await LandedGates.TextAsync(root, tip.Gates, ct).ConfigureAwait(false)
            : null;
        return new
        {
            Routes = start.Routes.Select(route => new { route.Kind, route.Label, route.Paths, route.Workflow, route.Level, route.Name }).ToArray(),
            Landed = new
            {
                start.Landed.Workflow,
                start.Landed.Level,
                start.Landed.Gates,
                Name = start.LandedName,
                State = state,
                Tip = tip is null ? null : new { tip.Line, tip.Commit, tip.Gates },
                Problem = problem,
                Declaration = declaration,
            },
            Verifications = start.Verifications.Select(each => new { each.Id, each.Name }).ToArray(),
        };
    }

    /// <summary>
    /// The tip of a repository's line in its checkout here, as the registry holds it, and its declaration's blob; or why it could not be
    /// read: no checkout here, no line known, or git's refusal (<see cref="LandedGates.ReadAsync"/>).
    /// </summary>
    private async Task<(string? Root, LandedTip? Tip, string? Problem)> TipAsync(DriverConfig config, string repository, string? workspace, CancellationToken ct)
    {
        if (_loop.Service is not { } service) return (null, null, "the driver is still coming up.");
        var root = LinesRoutes.Checkouts(await service.RegistryAsync(ct).ConfigureAwait(false), repository, null)
            .Select(row => row.Root).FirstOrDefault(path => Directory.Exists(path));
        if (root is null) return (null, null, $"`{repository}` has no checkout on this machine to read.");
        var line = await CanonicalLine.ResolveAsync(root, repository, workspace, config, ct).ConfigureAwait(false);
        if (line.Branch is not { } branch) return (root, null, $"`{repository}`'s line is not known here.");
        var (tip, problem) = await LandedGates.ReadAsync(root, branch, ct).ConfigureAwait(false);
        return (root, tip, problem is null ? null : $"`{repository}`: {problem}.");
    }

    /// <summary>
    /// The person's press on the landed route (FABRIC-PIPELINE1a, D192 point 2; Setup's route row and the Workflow tab's <i>Accept the
    /// gates</i>, D156's door): <c>{repository}</c> or <c>{workspace}</c>, and <c>workflow</c>, a verification pipeline's id, <c>current</c>
    /// for no verification, or null to clear. For a repository naming a verification pipeline it reads the <c>daoris.gates.json</c> at its
    /// line's tip and accepts it by its blob, as <c>daoris driver workflow use … --landed</c> does; where the tip cannot be read it keeps
    /// what was accepted for the same pipeline. Judged by the twins' edit first (<see cref="WorkflowSelection.Apply"/>), so a development
    /// pipeline is refused in its words before anything is written. Answers the start node as it now stands, and what the change lowers
    /// (<see cref="InvolvementRoutes.LowersRoute"/>), said: the person's press applies it (D157 point 10).
    /// </summary>
    [DriverRoute("WORKFLOW_LANDED")]
    private async Task<object?> WorkflowLandedAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = Optional(request, "repository");
        var workspace = Optional(request, "workspace");
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a landed route is set for a `repository` or a `workspace` — name one of them.");
        }

        var workflow = Optional(request, "workflow");
        var config = DriverConfig.Load(_loop.ConfigPath);
        var inWorkspace = workspace;
        if (repository is not null)
        {
            var service = _loop.Service ?? throw NotReady();
            var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
            inWorkspace = snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
        }

        var named = workflow is { } id && id != WorkflowSelection.Current ? id : null;
        var found = named is not null && WorkflowNamed.IsId(named) ? WorkflowStore.Load(_loop.Home, named) : null;
        if (named is not null && found is null) throw new DriverException($"no workflow `{named}` is saved here — `daoris driver workflow list` names them.");

        string? gates = null;
        if (repository is not null && found?.Read.Verifies == true)
        {
            var (_, tip, _) = await TipAsync(config, repository, inWorkspace, cancellationToken).ConfigureAwait(false);
            var own = config.Workflows.TryGetValue(repository.Trim(), out var held) ? held.Landed : null;
            gates = tip?.Gates ?? (own?.Workflow == named ? own.Gates : null);
        }

        var use = new WorkflowUse(workflow, repository, repository is null ? workspace : null, null)
        {
            Landed = true,
            Gates = gates,
            Pipeline = found?.Read.Pipeline,
        };
        var before = WorkflowSelection.ResolveLanded(config, repository, inWorkspace);
        // Judged before anything is written: a refusal is the twins' sentence.
        var after = WorkflowSelection.Apply(config, use);
        _context.Change(each => WorkflowSelection.Apply(each, use));

        NodeGraph? Newest(string id) =>
            WorkflowStore.Load(_loop.Home, id) is { Read: { Problem: null, Versions: [.., var last] } } ? last.Graph : null;
        var now = WorkflowSelection.ResolveLanded(after, repository, inWorkspace);
        var lowered = InvolvementRoutes.LowersRoute(new WorkflowLanded(now.Workflow, now.Gates), new WorkflowLanded(before.Workflow, before.Gates), Newest);
        return new
        {
            Start = await StartAsync(DriverConfig.Load(_loop.ConfigPath), repository, inWorkspace, cancellationToken).ConfigureAwait(false),
            Lowered = lowered,
        };
    }

    /// <summary>
    /// The answer: each step as the shared table's cell, its settings in the order the version reads them, and each limit's
    /// sentence by its code, for a page with no words of its own for one; and the start node (FABRIC-PIPELINE1a).
    /// </summary>
    private static object Answer(
        CurrentWorkflow drawn, string? repository, string workspace, bool? registered, IReadOnlyList<object>? repositories, object start)
    {
        var limits = new JsonObject();
        foreach (var code in drawn.Steps.Select(step => step.Limit).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            limits[code] = WorkflowLimits.Says(code);
        }

        return new
        {
            Repository = repository,
            Workspace = workspace,
            Registered = registered,
            drawn.StartHolds,
            Steps = drawn.Steps.Select(step => JsonDocument.Parse(WorkflowCurrent.ToJson(step)).RootElement.Clone()).ToArray(),
            drawn.Version,
            Limits = limits,
            Repositories = repositories,
            Start = start,
        };
    }

    /// <summary>
    /// What a repository's process is, apart from the words handed to its sessions: its steps without the work's standing
    /// answer, which only a repository has. A repository whose shape is its workspace's sets no rule of its own.
    /// </summary>
    private static string Shape(CurrentWorkflow drawn) =>
        string.Join('\n', drawn.Steps.Select(step => WorkflowCurrent.ToJson(step.Kind == WorkflowKinds.Work ? step with { Settings = [] } : step)));

    // Where a piece of work stands in its workflow (WORKFLOW1c; D157 point 11, design §5.1–§5.2, §7): the runs
    // `WorkflowRunReader` reads from this machine's records for `{session}`, `{quest}` or `{ask}`, exactly one, each step
    // Current's cell beside where it stands. The records are the service's and this machine's, so it waits for the driver.
    // What names nothing that runs (a chat, a quest not here) is answered as no run with the driver's sentence. Read-only,
    // and a run is never kept as a copy.
    [DriverRoute("WORKFLOW_RUN")]
    private async Task<object?> WorkflowRunAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var asked = new WorkflowRunAsk(Optional(request, "session"), Optional(request, "quest"), Optional(request, "ask"));
        if (new[] { asked.Session, asked.Quest, asked.Ask }.Count(each => each is not null) != 1)
        {
            throw new DriverException("a run is read for a `session`, a `quest` or an `ask` — name one of them.");
        }

        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var config = DriverConfig.Load(_loop.ConfigPath);
        var plugins = WorkflowCurrent.PluginsOf(PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names));
        // A repository's workspace is the registry's, read as `WORKFLOW_CURRENT` reads it; one it does not hold is in none.
        string? WorkspaceOf(string repository) => snapshot.Repositories
            .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;

        var read = await WorkflowRunReader
            .ReadAsync(asked, new WorkflowRunSources(service, _loop.Home, config, plugins, WorkspaceOf), cancellationToken)
            .ConfigureAwait(false);
        return new { Runs = read.Runs.Select(RunAnswer).ToArray(), read.Problem };
    }

    // The person's skip or stop of a run's node (FABRIC-CUTOVER1f; D190 points 3 and 6, the cutover design §3, §5.2): the page's half of
    // `daoris-driver workflow skip|stop`. Both doors end in RunNodeWords.Word, which keeps the word on the run's record under its lock with the
    // lease asked inside it; the loop's runtime applies it at its next pass, which the nudge wakes.
    /// <summary>
    /// Keep the person's `act` (`skip` with their `words`, or `stop`, words where given) on `node` of `run`, a run bound to a graph version, for
    /// the next look. Answers `{ run, node, act, done, refused, message }`, a refusal as an answer with its code (`not-run`, `unread-version`,
    /// `not-node`, `not-act`, `ended`, `worded`, the one table's `no-words`, `not-skippable` and `not-in-table`, `not-held`, `unreadable`) and the
    /// terminal's sentence. Asked for its words (`said: true`) by the terminal's verb over the pipe, it runs that verb's own path here.
    /// </summary>
    [DriverRoute("WORKFLOW_NODE")]
    [Publishes("WORKFLOW_NODE", StateScopes.Driver)]
    private async Task<object?> WorkflowNodeAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var run = PayloadHelper.GetRequiredValue<string>(request.Payload, "run");
        var node = PayloadHelper.GetRequiredValue<string>(request.Payload, "node");
        var act = PayloadHelper.GetRequiredValue<string>(request.Payload, "act");
        var words = Optional(request, "words");
        _ = _loop.Service ?? throw NotReady();
        var records = new NodeRecordFiles(_loop.Home);

        // The terminal's verb, confirmed in this window: its own path, so its lines are the alone path's; never a session's terminal here.
        if (Flag(request, "said"))
        {
            var said = await TerminalSaid.RunAsync(output =>
                WorkflowNodeCommand.RunAsync(new WorkflowNodeAsk(act, run, node, words), records, output, instance: null, _ => null, ct: cancellationToken))
                .ConfigureAwait(false);
            _loop.Nudge();
            return said;
        }

        var done = await Task.Run(() => RunNodeWords.Word(records, run, node, act, words), cancellationToken).ConfigureAwait(false);
        if (done.Done) _loop.Nudge();
        return new { done.Run, done.Node, done.Act, done.Done, done.Refused, Message = RunNodeWords.Said(done) };
    }

    /// <summary>
    /// One run as the page draws it: its chosen graph's digest and bound version identity, the plugins that may hold a start,
    /// each limit's sentence, and each step beside where it stands and its facts. A problem has no replacement graph.
    /// </summary>
    private static object RunAnswer(WorkflowRun run)
    {
        var limits = new JsonObject();
        foreach (var code in run.Steps.Select(step => step.Step.Limit).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            limits[code] = WorkflowLimits.Says(code);
        }

        return new
        {
            run.Repository,
            run.Workspace,
            run.Ask,
            run.Quests,
            run.Session,
            run.At,
            run.Problem,
            // The workflow's wait, in the shape the Trees domain's landing gate answers it.
            WorkflowGate = TreesRoutes.WorkflowWaits(run.WorkflowGate),
            Workflow = new { run.Current.Version, run.Current.StartHolds, Limits = limits,
                Id = run.Process?.Named == true ? run.Process.Binding!.Workflow : null,
                BoundVersion = run.Process?.Named == true ? run.Process.Binding!.Version : null,
                // FABRIC-PIPELINE1a: the route that chose its pipeline, as its binding keeps it; the page says it in its own words.
                Route = run.Process?.Binding is { } bound ? new { bound.Level, bound.Kind, bound.Label } : null },
            Steps = run.Steps.Select(step => new
            {
                Step = JsonDocument.Parse(WorkflowCurrent.ToJson(step.Step)).RootElement.Clone(),
                step.State,
                step.Detail,
                step.Added,
                step.Session,
                step.Quest,
                step.Agent,
                step.At,
                step.Environment,
                step.Commit,
                step.Branch,
                step.PullRequest,
                step.Plugin,
                step.Count,
                step.Of,
                step.GoAhead,
                step.Words,
                step.Code,
                // FABRIC-CUTOVER1e: a graph run's node's own kind and the subject it observes; null on a steps run's step.
                step.Node,
                step.On,
                // FABRIC-CUTOVER1f, additive: what the person may say of a graph run's node now (`skip`, `stop`), and their word kept on it and
                // not applied yet; null on a steps run's step and where none.
                step.Acts,
                Asked = step.Asked is { } asked ? new { asked.Node, asked.Act, asked.Words, asked.At } : null,
            }).ToArray(),
            // FABRIC-CUTOVER1e: a run bound to a graph version, drawn from its record: its graph's wires and what its record says of it.
            Graph = run.Graph is { } graph
                ? new
                {
                    graph.Run, graph.Entries, graph.End,
                    Wires = graph.Wires.Select(wire => new { wire.From, wire.To, wire.Kind }).ToArray(),
                    graph.Quest, graph.Branch, graph.Judged, graph.Unread, graph.Ended,
                }
                : null,
        };
    }
}
