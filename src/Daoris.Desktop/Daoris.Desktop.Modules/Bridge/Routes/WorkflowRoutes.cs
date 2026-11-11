using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;
using static Daoris.Desktop.WorkflowAnswers;

namespace Daoris.Desktop;

/// <summary>
/// How work moves in a repository or a workspace, the page's `bridge/workflow.ts`: the routes its start node takes and the graph each follows, read from this machine's driver file, its plugins
/// and its registry exactly as `daoris workflow show` reads them; and where a piece of work stands in it, the runs <see cref="WorkflowRunReader"/> reads. Read-only but three: <c>WORKFLOW_LANDED</c>, the person's press that sets
/// the landed route and accepts the gates it runs unasked; <c>INTEGRATION_GATES</c>, their acceptance of a
/// repository's integration declaration by its blob; <c>WORKFLOW_NODE</c>, their skip or stop of a graph run's
/// node; and <c>WORKFLOW_SAVE</c>, <c>WORKFLOW_USE</c> and <c>WORKFLOW_KIND</c>, their saving, choosing and kind-setting of
/// workflows.
/// </summary>
public sealed class WorkflowRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    // A repository's routes, or a workspace's: `{repository}` or `{workspace}`, exactly one. A repository's workspace is the
    // registry's, so it waits for the driver as the lines do; one the registry does not hold is read as in no workspace, and
    // the answer says so. Beside a workspace's, each repository the registry holds there, with whether a choice of its own makes it
    // follow something else, or none where the registry is not up yet. An optional `route` (`default`, `kind:<name>` or `landed`) adds
    // `effective`: the graph that route follows, read and never written.
    [DriverRoute("WORKFLOW_CURRENT", PipeClass.Open)]
    private async Task<object?> WorkflowCurrentAsync(WorkflowCurrent press, CancellationToken cancellationToken)
    {
        var (repository, workspace, route) = press;
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a workflow is drawn for a `repository` or a `workspace` — name one of them.");
        }

        var config = DriverConfig.Load(_loop.ConfigPath);
        // The plugins beside the file, switched on and sound, as the landing's and the terminal's doors read them.
        var plugins = WorkflowSteps.PluginsOf(PluginCatalog.Load(_loop.Home, ProviderRegistry.Built().Names));
        var drawn = new CurrentWorkflow(WorkflowSteps.StartHolds(plugins), [], "");

        if (repository is not null)
        {
            var service = _loop.Service ?? throw NotReady();
            var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
            var known = snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
            var start = await StartAsync(config, repository, known?.Workspace, cancellationToken).ConfigureAwait(false);
            var effective = route is null ? null : WorkflowEffectives.Read(config, new WorkflowFiles(_loop.Home), repository, known?.Workspace, route, plugins);
            return Answer(drawn, repository, RemoteTarget.Workspace(known?.Workspace), registered: known is not null, repositories: null, start, effective);
        }

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
                    Own = config.Workflows.TryGetValue(each.Repository, out var own) && own.NamesDevelopment,
                })];
        }

        var shared = drawn;

        return Answer(shared, null, workspace!, registered: null, repositories: here,
            await StartAsync(config, null, workspace, cancellationToken).ConfigureAwait(false),
            route is null ? null : WorkflowEffectives.Read(config, new WorkflowFiles(_loop.Home), null, workspace, route, plugins));
    }

    /// <summary>
    /// The start node a Workflow tab draws first: <see cref="WorkflowStart.Read"/>'s routes,
    /// each with the pipeline it names and what chose it; the landed route with its pipeline's name, the gates accepted and where they
    /// stand against the line's tip (<see cref="LandedGates.StateOf"/>), the tip and the declaration there where it is to be accepted, or
    /// why the tip could not be read; and the verification pipelines saved here. A workspace has no line, so its route's gates are
    /// accepted repository by repository, and its tip is never read.
    /// </summary>
    /// <remarks>
    /// a repository's start node also says where its integration's own acceptance
    /// stands, <c>integration</c>, in the landed route's shape: the blob accepted (<c>gates</c>), its state against the line's tip
    /// (<see cref="IntegrationAcceptance.StateOf"/>), the tip, the declaration's text where it is to be accepted, or why the tip could not be
    /// read. So git is asked for every repository, once, and both acceptances are judged against that one read of the tip; the landed
    /// route's fields stay as they were where it names no pipeline. A workspace's <c>integration</c> is null, since a workspace has no line.
    /// </remarks>
    private async Task<object> StartAsync(DriverConfig config, string? repository, string? workspace, CancellationToken ct)
    {
        var start = WorkflowStart.Read(config, repository, workspace, WorkflowStore.List(_loop.Home));
        var (root, read, readProblem) = repository is not null
            ? await TipAsync(config, repository, workspace, ct).ConfigureAwait(false)
            : (null, null, null);
        var verifies = start.Landed.Workflow != WorkflowSelection.Current;
        var (tip, problem) = verifies ? (read, readProblem) : (null, null);
        var state = repository is null && verifies ? LandedGates.Unread : LandedGates.StateOf(start.Landed, tip);
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
            Integration = repository is null ? null : await IntegrationAsync(config, repository, root, read, readProblem, ct).ConfigureAwait(false),
        };
    }

    /// <summary>
    /// Where a repository's integration acceptance stands, from the tip read once for the start node: the blob accepted, its
    /// state, the tip, the declaration's text where it is changed or unaccepted, and why the tip could not be read.
    /// </summary>
    private static async Task<object> IntegrationAsync(
        DriverConfig config, string repository, string? root, LandedTip? tip, string? problem, CancellationToken ct)
    {
        var accepted = IntegrationAcceptance.Of(config, repository);
        var state = IntegrationAcceptance.StateOf(accepted, tip?.Gates);
        var declaration = root is not null && tip is not null && state is IntegrationAcceptance.Changed or IntegrationAcceptance.Unaccepted
            ? await IntegrationAcceptance.TextAsync(root, tip.Gates, ct).ConfigureAwait(false)
            : null;
        return new
        {
            Gates = accepted,
            State = state,
            Tip = tip is null ? null : new { tip.Line, tip.Commit, tip.Gates },
            Problem = problem,
            Declaration = declaration,
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
    /// The page's <i>Verify…</i> <c>daoris gate verify</c>'s twin: <c>{repository}</c> and one of <c>branch</c> or
    /// <c>commit</c>, or neither for its checkout's HEAD. It binds the person's run through <see cref="VerifyCommand.BindAsync"/> in the first
    /// checkout of the repository on this disk, so the library's refusals are the page's: answers <c>{ repository, done, refused, message }</c>,
    /// <c>refused</c> being <c>no-checkout</c>, <c>refused</c> (no pipeline, gates not accepted, no version that reads) or <c>unread</c> (the commit).
    /// </summary>
    [DriverRoute("WORKFLOW_VERIFY", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WORKFLOW_VERIFY", Repository = "repository")]
    [GroundRepository("WORKFLOW_VERIFY", "repository")]
    [GroundRead("WORKFLOW_VERIFY", nameof(PipeReads.VerifiedRoute))]
    [Says("WORKFLOW_VERIFY", "repository:repository")]
    [NotAlready("WORKFLOW_VERIFY", "starts a run of the accepted gates beside the route, and sets no value")]
    [Publishes("WORKFLOW_VERIFY", StateScopes.Driver)]
    private async Task<object?> WorkflowVerifyAsync(WorkflowVerify press, CancellationToken cancellationToken)
    {
        var repository = press.Repository?.Trim();
        if (string.IsNullOrEmpty(repository)) throw new DriverException("a verification is asked for a `repository` — name it.");
        var branch = press.Branch?.Trim();
        var commit = press.Commit?.Trim();
        if (!string.IsNullOrEmpty(branch) && !string.IsNullOrEmpty(commit)) throw new DriverException("`verify` verifies a branch or a commit, not both.");

        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var workspace = snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;
        var root = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, null)
            .Select(row => row.Root).FirstOrDefault(path => Directory.Exists(path));
        if (root is null)
        {
            return new { repository, done = false, refused = "no-checkout", message = $"`{repository}` has no checkout on this machine to verify in." };
        }

        var route = WorkflowSelection.ResolveLanded(DriverConfig.Load(_loop.ConfigPath), repository, workspace);
        using var said = new StringWriter();
        var ask = new VerifyAsk(repository) { Branch = string.IsNullOrEmpty(branch) ? null : branch, Commit = string.IsNullOrEmpty(commit) ? null : commit };
        var code = await VerifyCommand.BindAsync(_loop.Home, root, ask, route, workspace, said, cancellationToken).ConfigureAwait(false);
        if (code == 0) _loop.Nudge();
        return new { repository, done = code == 0, refused = code switch { 0 => null, 1 => "refused", _ => "unread" }, message = said.ToString().Trim() };
    }

    /// <summary>
    /// The person's press on the landed route: <c>workflow</c>, a verification pipeline's id, <c>current</c> for none, or null to clear.
    /// A verification pipeline accepts the <c>daoris.gates.json</c> at the line's tip by its blob; an optional <c>gates</c> names the blob
    /// the person read, refused <c>moved</c> where the tip moved and <c>unread</c> where it cannot be read, with nothing written.
    /// </summary>
    [DriverRoute("WORKFLOW_LANDED", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WORKFLOW_LANDED", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("WORKFLOW_LANDED", "repository", "workspace")]
    [GroundRead("WORKFLOW_LANDED", nameof(PipeReads.LandedRoute))]
    [Says("WORKFLOW_LANDED", "repository:repository")]
    [NotAlready("WORKFLOW_LANDED", "reads the gates at the line's tip and accepts them by their blob")]
    private async Task<object?> WorkflowLandedAsync(WorkflowLandedPress press, CancellationToken cancellationToken)
    {
        var repository = press.Repository;
        var workspace = press.Workspace;
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a landed route is set for a `repository` or a `workspace` — name one of them.");
        }

        var workflow = press.Workflow;
        var expected = press.Gates;
        if (expected is not null && !IntegrationGatesKey.IsAcceptance(expected)) throw new DriverException(IntegrationGatesKey.Problem);
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
        if (named is not null && found is null) throw new DriverException($"no workflow `{named}` is saved here — `daoris workflow list` names them.");

        string? gates = null;
        if (repository is not null && found?.Read.Verifies == true)
        {
            var (_, tip, problem) = await TipAsync(config, repository, inWorkspace, cancellationToken).ConfigureAwait(false);
            if (expected is not null)
            {
                if (tip is null) throw new DriverException($"{problem ?? $"`{repository}`'s line could not be read here."} Nothing was accepted (unread).");
                if (expected != tip.Gates)
                {
                    throw new DriverException($"moved: `{repository}`'s line holds another declaration than the one you read, so nothing was accepted: "
                        + "read it again, then accept it.");
                }
            }

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
    /// The person's press accepting a repository's integration declaration by its blob, or <c>clear: true</c> to withdraw it, kept in
    /// <c>driver.json</c>'s <c>integrationGates</c> apart from the landed route's, so integrating needs no verification pipeline. Refused
    /// <c>moved</c> or <c>unread</c> as <c>WORKFLOW_LANDED</c> is. Only the person's: an agent never accepts what it would run under.
    /// </summary>
    [DriverRoute("INTEGRATION_GATES", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("INTEGRATION_GATES", Repository = "repository")]
    [GroundRepository("INTEGRATION_GATES", "repository")]
    [GroundRead("INTEGRATION_GATES", nameof(PipeReads.IntegrationDeclaration))]
    [Says("INTEGRATION_GATES", "repository:repository")]
    [NotAlready("INTEGRATION_GATES", "reads the declaration at the line's tip and accepts it by its blob")]
    [Publishes("INTEGRATION_GATES", StateScopes.Driver)]
    private async Task<object?> IntegrationGatesAsync(IntegrationGates press, CancellationToken cancellationToken)
    {
        var repository = press.Repository?.Trim();
        if (string.IsNullOrEmpty(repository))
        {
            throw new DriverException("an integration's declaration is accepted for a `repository` — name it.");
        }

        var named = press.Gates;
        var clear = press.Clear;
        if (clear && named is not null) throw new DriverException("an integration's acceptance takes the blob read or withdraws it — name `gates` or `clear`, not both.");
        if (named is not null && !IntegrationGatesKey.IsAcceptance(named)) throw new DriverException(IntegrationGatesKey.Problem);

        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var workspace = snapshot.Repositories.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;

        string? refused = null;
        string? message = null;
        if (clear)
        {
            _context.Change(each => each.WithIntegrationGates(repository, null));
        }
        else
        {
            var (_, tip, problem) = await TipAsync(DriverConfig.Load(_loop.ConfigPath), repository, workspace, cancellationToken).ConfigureAwait(false);
            if (tip is null)
            {
                (refused, message) = ("unread", $"{problem ?? $"`{repository}`'s line could not be read here."} Nothing was accepted.");
            }
            else if (named is not null && named != tip.Gates)
            {
                (refused, message) = ("moved", $"`{repository}`'s line holds another declaration than the one you read, so nothing was accepted: "
                    + "read it again, then accept it.");
            }
            else
            {
                _context.Change(each => each.WithIntegrationGates(repository, tip.Gates));
            }
        }

        return new
        {
            Start = await StartAsync(DriverConfig.Load(_loop.ConfigPath), repository, workspace, cancellationToken).ConfigureAwait(false),
            Done = refused is null,
            Refused = refused,
            Message = message,
        };
    }

    // the terminal's `workflow new|edit|use|kind` as routes, the person's alone. Each runs the typed operation the
    // verb runs (`WorkflowOperations`) and says its result as data; a refusal is an answer `{ done: false, refused, message }` with nothing written.
    // Every answer says its reach: per repository, the graph a combination of choices and landing rule would run,
    // `cannots` where that combination cannot start and `noGates` where its line integrates with none.

    private WorkflowOperations.RegistryRead Registry => async ct =>
    {
        if (_loop.Service is not { } service) return new ChoicesRegistry(null, "the driver is still coming up");
        var rows = await service.RegistryAsync(ct).ConfigureAwait(false);
        return ChoicesRegistry.Of(rows);
    };

    private static object[] ReachRows(IReadOnlyList<WorkflowReach> reach) =>
        [.. reach.Select(each => new { each.Repository, each.Workflow, each.Cannots, each.NoGates, each.Said })];

    private static object Refused(string code, string message, IReadOnlyList<WorkflowReach>? reach = null) =>
        new { Done = false, Refused = code, Message = message, Changed = false, Reach = ReachRows(reach ?? []), Start = (object?)null };

    private static JsonNode? NodeOf(JsonElement? sent) =>
        sent is { ValueKind: JsonValueKind.Array or JsonValueKind.Object } value ? JsonNode.Parse(value.GetRawText()) : null;

    /// <summary>
    /// A version saved: <c>{id}</c> and one of <c>from</c> (a preset, a saved
    /// workflow or <c>current:&lt;repository&gt;</c>, for an id not held, with <c>withOpinion</c>, <c>withLook</c> and <c>name</c>), <c>steps</c> or
    /// <c>graph</c>. Under a fresh id it makes v1. Onto a saved one it appends, against <c>base</c>, the version the caller read: a newer one is
    /// refused <c>stale</c>; and since a new start binds the newest version, it first answers every place that chooses the workflow, and writes
    /// only when <c>authorize</c> lists exactly those places' keys (refused <c>reach</c> otherwise, also where the graph's reach cannot start).
    /// <c>plan: true</c> says all of it and writes nothing. Answers <c>{ id, done, refused, message, version, written, appended, scopes, reach,
    /// problems }</c>, the problems a refused graph's, each by its code at its step or wire.
    /// </summary>
    [DriverRoute("WORKFLOW_SAVE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [ClassedBy("WORKFLOW_SAVE", "plan", PipeClass.Open, "true")]
    [Owner("WORKFLOW_SAVE", None = OwnerNone.MachineSetting)]
    [GroundFiles("WORKFLOW_SAVE", "workflow-file", "id")]
    [Says("WORKFLOW_SAVE", "id:form", "from:form", "name:words")]
    [NotAlready("WORKFLOW_SAVE", "adds a version beside the ones kept, or makes a workflow")]
    [Publishes("WORKFLOW_SAVE", StateScopes.Driver)]
    private async Task<object?> WorkflowSaveAsync(WorkflowSavePress press, CancellationToken cancellationToken)
    {
        var id = press.Id?.Trim();
        if (string.IsNullOrEmpty(id)) throw new DriverException("a workflow is saved under an `id` — name it.");
        var saved = await WorkflowOperations.SaveAsync(
            DriverConfig.Load(_loop.ConfigPath), _loop.Home, Registry, id, press.From, NodeOf(press.Steps), NodeOf(press.Graph),
            press.WithOpinion, press.WithLook, press.Name,
            press.Base is { } baseVersion ? baseVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            press.Authorize?.ToList(), press.Plan, DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
            cancellationToken, press.Updates).ConfigureAwait(false);
        return new
        {
            saved.Id,
            saved.Done,
            saved.Refused,
            saved.Message,
            saved.Version,
            saved.Written,
            saved.Appended,
            Scopes = saved.Scopes.Select(each => new { each.Level, each.Name, each.Role, each.Kind, each.Key }).ToArray(),
            Reach = ReachRows(saved.Reach),
            Problems = saved.Problems.Select(each => new { each.Code, each.Wire, each.Node, each.Words }).ToArray(),
        };
    }

    /// <summary>
    /// The person's choice of a workflow for a place (<c>daoris workflow use</c>'s twin, the landed route apart, which is
    /// <c>WORKFLOW_LANDED</c>): <c>{repository}</c> or <c>{workspace}</c>, <c>workflow</c> (a saved id, <c>current</c>, or absent to clear), and
    /// <c>kind</c> for a kind of task instead of the default; <c>version</c>, the newest the caller read, is refused <c>stale</c> when it moved.
    /// One change, judged first as the terminal judges it: a graph whose reach cannot start somewhere is refused <c>reach</c>, nothing written.
    /// Answers <c>{ done, refused, message, changed, reach, start }</c>, the start node as it now stands. <c>plan: true</c> reads the choice as a
    /// plan of one and writes nothing (<see cref="PlanAnswer"/>); <c>expected</c>, a plan's token, is refused <c>moved</c> where anything moved
    /// since, and <c>stale</c> stays for a <c>version</c> that did. A press with neither acts as it always did.
    /// </summary>
    [DriverRoute("WORKFLOW_USE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WORKFLOW_USE", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("WORKFLOW_USE", "repository", "workspace")]
    [GroundRead("WORKFLOW_USE", nameof(PipeReads.PlaceRoutes))]
    [Says("WORKFLOW_USE", "repository:repository", "workflow:form", "kind:form")]
    [NotAlready("WORKFLOW_USE", "merges into the place's choice, which keeps its other kinds and its landed route")]
    [Publishes("WORKFLOW_USE", StateScopes.Driver)]
    private async Task<object?> WorkflowUseAsync(WorkflowUsePress press, CancellationToken cancellationToken)
    {
        var repository = press.Repository;
        var workspace = press.Workspace;
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a workflow is chosen for a `repository` or a `workspace` — name one of them.");
        }

        var workflow = press.Workflow;
        var kind = press.Kind;
        var config = DriverConfig.Load(_loop.ConfigPath);
        var expected = press.Expected;
        WorkflowUsed used;
        try
        {
            if (workflow is { } named && named != WorkflowSelection.Current && press.Version is { } read
                && WorkflowOperations.NewestOf(_loop.Home, named) is { } newest && newest != read)
            {
                return Refused("stale", $"`{named}` is at v{newest} now, not v{read}: read it again and choose that. Nothing was written.");
            }

            if (press.Plan || expected is not null)
            {
                var planned = await WorkflowOperations.PlanUseAsync(config, _loop.Home, Registry, workflow, repository, workspace, kind, cancellationToken).ConfigureAwait(false);
                if (press.Plan) return PlanAnswer(planned);
                if (planned.Expected != expected)
                {
                    return Refused("moved", "What this choice does has moved since you read its plan: read the plan again and choose then. Nothing was written.");
                }

                used = planned.Used;
            }
            else
            {
                used = await WorkflowOperations.UseAsync(config, _loop.Home, Registry, workflow, repository, workspace, kind, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (ChoiceWords refused)
        {
            return Refused("refused", refused.Message);
        }

        if (used.Refusal is not null) return Refused(used.Reach.Any(each => each.Cannots.Count > 0) ? "reach" : "refused", used.Refusal, used.Reach);
        if (used.Changed) _context.Change(each => WorkflowSelection.Apply(each, used.Edit!));
        var inWorkspace = workspace ?? (await Registry(cancellationToken).ConfigureAwait(false)).Checkouts
            ?.FirstOrDefault(each => WorkflowOperations.InScope(each.Repository, repository!)).Workspace;
        return new
        {
            Done = true,
            Refused = (string?)null,
            Message = string.Join('\n', used.Lines),
            used.Changed,
            Reach = ReachRows(used.Reach),
            Start = await StartAsync(DriverConfig.Load(_loop.ConfigPath), repository, inWorkspace, cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>
    /// A choice read as a plan of one and nothing written: <c>{ done: false, plan: true, refused, message, changed, reach, lowers,
    /// places, keeps, expected }</c>. A press carrying <c>expected</c> is refused <c>moved</c> where anything the plan says has changed.
    /// </summary>
    private static object PlanAnswer(WorkflowUsePlan plan) => new
    {
        Done = false,
        Plan = true,
        Refused = plan.Used.Refusal is null ? null : plan.Used.Reach.Any(each => each.Cannots.Count > 0) ? "reach" : "refused",
        Message = plan.Used.Refusal ?? string.Join('\n', plan.Used.Lines),
        plan.Used.Changed,
        Reach = ReachRows(plan.Used.Reach),
        plan.Lowers,
        Places = plan.Places.Select(each => new { each.Repository, each.Was, each.Now, each.Lowers }).ToArray(),
        Keeps = plan.Keeps is null ? null : new { plan.Keeps.Said, Kinds = plan.Keeps.Kinds.Select(each => new { Kind = each.Key, Workflow = each.Value }).ToArray() },
        plan.Expected,
    };

    /// <summary>
    /// The person's declaration of a kind of task for a workspace: <c>{workspace, kind,
    /// label, paths}</c>, or <c>drop: true</c> with no label. Declared again, the label and the paths are replaced; the workflow a kind maps to is
    /// <c>WORKFLOW_USE</c>'s. Answers <c>{ done, refused, message, changed, reach, start }</c>, the reach being what the workspace's repositories
    /// now run.
    /// </summary>
    [DriverRoute("WORKFLOW_KIND", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WORKFLOW_KIND", Workspace = "workspace")]
    [GroundRepository("WORKFLOW_KIND", "workspace")]
    [GroundRead("WORKFLOW_KIND", nameof(PipeReads.WorkspaceKinds))]
    [Says("WORKFLOW_KIND", "kind:form", "label:words")]
    [NotAlready("WORKFLOW_KIND", "merges into the workspace's kinds, keeping the others")]
    [Publishes("WORKFLOW_KIND", StateScopes.Driver)]
    private async Task<object?> WorkflowKindAsync(WorkflowKind press, CancellationToken cancellationToken)
    {
        var workspace = press.Workspace;
        var kind = press.Kind;
        if (workspace is null || kind is null) throw new DriverException("a kind is declared for a `workspace` and a `kind` — name both.");
        var label = press.Label;
        var drop = press.Drop;
        if (drop == (label is not null)) throw new DriverException("a kind is declared with a `label`, or dropped with `drop: true` — one of them.");
        var paths = (press.Paths ?? []).Select(each => each.Trim()).Where(each => each.Length > 0).ToList();
        if (drop && paths.Count > 0) throw new DriverException("a dropped kind takes no `paths`.");

        var config = DriverConfig.Load(_loop.ConfigPath);
        var set = WorkflowOperations.KindSet(config, workspace, kind, label, paths, drop);
        if (set.Refusal is not null) return Refused("refused", set.Refusal);
        _context.Change(each => WorkflowSelection.Apply(each, set.Edit!));
        var after = DriverConfig.Load(_loop.ConfigPath);
        return new
        {
            Done = true,
            Refused = (string?)null,
            Message = string.Join('\n', set.Lines),
            Changed = true,
            Reach = ReachRows(await WorkflowOperations.PlaceReachAsync(after, _loop.Home, Registry, null, workspace, cancellationToken).ConfigureAwait(false)),
            Start = await StartAsync(after, null, workspace, cancellationToken).ConfigureAwait(false),
        };
    }

    // Where a piece of work stands in its workflow: the runs
    // `WorkflowRunReader` reads from this machine's records for `{session}`, `{quest}` or `{ask}`, or a repository's `{line}`
    // (its newest verification runs, each with its verdict), exactly one, each step
    // Current's cell beside where it stands. The records are the service's and this machine's, so it waits for the driver.
    // What names nothing that runs (a chat, a quest not here) is answered as no run with its code, the id and the workflow its
    // words name, and the driver's English sentence: the page words a code it knows in the
    // reader's language and shows the sentence for one it does not. Read-only, and a run is never kept as a copy.
    [DriverRoute("WORKFLOW_RUN", PipeClass.Open)]
    private async Task<object?> WorkflowRunAsync(WorkflowRunRead press, CancellationToken cancellationToken)
    {
        var asked = new WorkflowRunAsk(press.Session, press.Quest, press.Ask, press.Line);
        if (new[] { asked.Session, asked.Quest, asked.Ask, asked.Line }.Count(each => each is not null) != 1)
        {
            throw new DriverException("a run is read for a `session`, a `quest`, an `ask` or a repository's `line` — name one of them.");
        }

        var service = _loop.Service ?? throw NotReady();
        var snapshot = await service.SnapshotAsync(cancellationToken).ConfigureAwait(false);
        var config = DriverConfig.Load(_loop.ConfigPath);
        var plugins = WorkflowSteps.PluginsOf(PluginCatalog.Load(_loop.Home, ProviderRegistry.Built().Names));
        // A repository's workspace is the registry's, read as `WORKFLOW_CURRENT` reads it; one it does not hold is in none.
        string? WorkspaceOf(string repository) => snapshot.Repositories
            .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;

        var read = await WorkflowRunReader
            .ReadAsync(asked, new WorkflowRunSources(service, _loop.Home, config, plugins, WorkspaceOf), cancellationToken)
            .ConfigureAwait(false);
        return new { Runs = read.Runs.Select(RunAnswer).ToArray(), read.Problem, read.Code, read.Id, read.Workflow };
    }

    // The person's skip or stop of a run's node: the page's half of
    // `daoris workflow skip|stop`. Both doors end in RunNodeWords.Word, which keeps the word on the run's record under its lock with the
    // lease asked inside it; the loop's runtime applies it at its next pass, which the nudge wakes.
    /// <summary>
    /// Keep the person's `act` (`skip` with their `words`, or `stop`, words where given; a review's `changes`, with their words; a task's `done`,
    /// or an approval's `approve` or `reject`, with a note where given) on `node` of `run`, a run bound to a graph version, for the next look.
    /// Answers `{ run, node, act, done, refused, message }`, a refusal as an answer with its code (`not-run`, `unread-version`, `not-node`,
    /// `not-act`, `not-review`, `not-task`, `not-waiting`, `ended`, `worded`, the one table's `no-words`,
    /// `not-skippable` and `not-in-table`, `not-held`, `unreadable`, an Accept's `moved` with the `revision` standing and `none-unchecked`) and
    /// the terminal's sentence; a kept Accept answers the `revision` it is bound to, and `early` where its review has not begun. Asked for its words (`said: true`) by the
    /// terminal's verb over the pipe, it runs that verb's own path here.
    /// </summary>
    [DriverRoute("WORKFLOW_NODE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("WORKFLOW_NODE", None = OwnerNone.NamesNothing)]
    [GroundWait("WORKFLOW_NODE", "workflow-node", "run", "node")]
    [Says("WORKFLOW_NODE", "run:quest", "act:form", "words:words")]
    [Publishes("WORKFLOW_NODE", StateScopes.Driver)]
    private async Task<object?> WorkflowNodeAsync(WorkflowNodePress press, CancellationToken cancellationToken)
    {
        var (run, node, act, words) = (press.Run, press.Node, press.Act, press.Words);
        _ = _loop.Service ?? throw NotReady();
        var records = new NodeRecordFiles(_loop.Home);

        // The terminal's verb, confirmed in this window: its own path, so its lines are the alone path's; never a session's terminal here.
        if (press.Said)
        {
            var said = await TerminalSaid.RunAsync(output =>
                WorkflowNodeCommand.RunAsync(new WorkflowNodeAsk(act, run, node, words, press.Revision), records, output, instance: null, _ => null, ct: cancellationToken))
                .ConfigureAwait(false);
            _loop.Nudge();
            return said;
        }

        // An Accept that named the run's unchecked dones passes them in its own edit; a stop reads the integration record first, so one whose
        // member promoted is refused here, as at the terminal.
        var done = press is { Act: NodeWordActs.Accept, Unchecked: true, Revision: { } drawn }
            ? UncheckedAccepts.Keep(records, run, node, drawn)
            : await Task.Run(() => RunNodeWords.WordAsync(
                records, NodeRuntime.HomeDoor(_loop.Home), run, node, act, words, ct: cancellationToken, attempt: press.Attempt is { } named ? (int)Math.Clamp(named, 0, int.MaxValue) : null, revision: press.Revision,
                    part: press.Part),
                cancellationToken)
                .ConfigureAwait(false);
        if (done.Done) _loop.Nudge();
        var revision = done.Done || done.Refused == ReviewAccepts.Moved ? done.Detail : null;
        return new { done.Run, done.Node, done.Act, done.Done, done.Refused, done.Early, Revision = revision, Message = RunNodeWords.Said(done) };
    }
}
