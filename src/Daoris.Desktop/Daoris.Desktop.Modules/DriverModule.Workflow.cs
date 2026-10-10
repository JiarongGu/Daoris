using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// How work moves in a repository or a workspace, the page's `bridge/workflow.ts` (WORKFLOW1b; D157 point 7, the workflow
/// design §2.7, §6.1): its Current workflow, which <see cref="WorkflowCurrent.Derive"/> draws from this machine's driver file,
/// its plugins and its registry exactly as `daoris driver workflow show` reads them; and where a piece of work stands in it
/// (WORKFLOW1c), the runs <see cref="WorkflowRunReader"/> reads. Read-only: nothing here writes, and every gate goes on
/// reading the rules as it did.
/// </summary>
public sealed partial class DriverModule
{
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
            return Answer(drawn, repository, RemoteTarget.Workspace(known?.Workspace), registered: known is not null, repositories: null);
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

        return Answer(shared, null, workspace!, registered: null, repositories: here);
    }

    /// <summary>
    /// The answer: each step as the shared table's cell, its settings in the order the version reads them, and each limit's
    /// sentence by its code, for a page with no words of its own for one.
    /// </summary>
    private static object Answer(CurrentWorkflow drawn, string? repository, string workspace, bool? registered, IReadOnlyList<object>? repositories)
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
            WorkflowGate = WorkflowWaits(run.WorkflowGate),
            Workflow = new { run.Current.Version, run.Current.StartHolds, Limits = limits,
                Id = run.Process?.Named == true ? run.Process.Binding!.Workflow : null,
                BoundVersion = run.Process?.Named == true ? run.Process.Binding!.Version : null },
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
            }).ToArray(),
        };
    }
}
