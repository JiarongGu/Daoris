using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// The registry's retire, re-wire, import and create as the person's presses of the pipe: <c>daoris repository retire|wire|import|create</c> run their own
/// path (<c>said</c>) in this Daoris, whose service client holds the person key, so the terminal's press is confirmed in the window and the
/// service is called as the person's, never keyless. A folder to import is an absolute machine path, which only this machine's service takes.
/// </summary>
public sealed class RegistryPersonRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>A repository taken off the registry, in the service's own sentence, which says what retiring does not do.</summary>
    [DriverRoute("REGISTRY_RETIRE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("REGISTRY_RETIRE", Repository = "repository")]
    [GroundRepository("REGISTRY_RETIRE", "repository")]
    [Says("REGISTRY_RETIRE", "repository:repository")]
    [Publishes("REGISTRY_RETIRE", StateScopes.Registry)]
    private Task<object?> RegistryRetireAsync(RegistryRetirePress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, error) => RepositoryPresses.RetireAsync(press.Repository.Trim(), Service(), output, error, cancellationToken));

    /// <summary>A repository wired to another workspace.</summary>
    [DriverRoute("REGISTRY_REWIRE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("REGISTRY_REWIRE", Repository = "repository", Workspace = "workspace")]
    [GroundRepository("REGISTRY_REWIRE", "repository", "workspace")]
    [Says("REGISTRY_REWIRE", "repository:repository")]
    [Publishes("REGISTRY_REWIRE", StateScopes.Registry)]
    private Task<object?> RegistryRewireAsync(RegistryRewirePress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, error) => RepositoryPresses.WireAsync(press.Repository.Trim(), press.Workspace.Trim(), Service(), output, error, cancellationToken));

    /// <summary>A folder department's excludes changed, and its manifest read again with them.</summary>
    [DriverRoute("REGISTRY_EXCLUDES", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("REGISTRY_EXCLUDES", Repository = "repository")]
    [GroundRepository("REGISTRY_EXCLUDES", "repository")]
    [Says("REGISTRY_EXCLUDES", "repository:repository")]
    [Publishes("REGISTRY_EXCLUDES", StateScopes.Registry)]
    private Task<object?> RegistryExcludesAsync(RegistryExcludesPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, error) => RepositoryPresses.ExcludesAsync(
            press.Repository.Trim(), press.Add ?? [], press.Remove ?? [], _loop.Home, Service(), output, error, cancellationToken));

    /// <summary>Every repository an absolute folder's subdirectories propose registered, into the workspace named where one is.</summary>
    [DriverRoute("REGISTRY_IMPORT", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("REGISTRY_IMPORT", Workspace = "workspace")]
    [GroundFiles("REGISTRY_IMPORT", "import-folder", "folder")]
    [Says("REGISTRY_IMPORT", "folder:form", "workspace:form")]
    [Publishes("REGISTRY_IMPORT", StateScopes.Registry)]
    private Task<object?> RegistryImportAsync(RegistryImportPress press, CancellationToken cancellationToken) =>
        SaidAsync(press.Said, (output, error) => RepositoryPresses.ImportAsync(
            string.IsNullOrWhiteSpace(press.Folder) ? null : press.Folder.Trim(), string.IsNullOrWhiteSpace(press.Workspace) ? null : press.Workspace.Trim(),
            Service(), output, error, cancellationToken));

    // A department made (`daoris repository create`): its folder under the workspace's repositories' parent, an empty history with one commit, its
    // registration unadopted, its drive and its set-up quest, the plan's receipt written before the first effect (`RepositoryCreates`). A person's
    // press that no mode grants as a family; the plan is judged again here, so a name taken, a folder not empty or no identity is refused in the
    // verb's words and nothing is made. `digest` is the plan the person read, none plans again and applies that plan. Answers the verb's lines
    // (`said`), or `{ done, message }`.
    [DriverRoute("REGISTRY_CREATE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("REGISTRY_CREATE", Workspace = "workspace")]
    [GroundRepository("REGISTRY_CREATE", "name")]
    [GroundRepository("REGISTRY_CREATE", "workspace")]
    [GroundRead("REGISTRY_CREATE", nameof(PipeReads.CreatePlan))]
    [Says("REGISTRY_CREATE", "name:form", "workspace:form")]
    [Publishes("REGISTRY_CREATE", StateScopes.Registry | StateScopes.Quests | StateScopes.Asks | StateScopes.Driver | StateScopes.Rules)]
    private Task<object?> RegistryCreateAsync(RegistryCreatePress press, CancellationToken cancellationToken)
    {
        var ask = CreateAskOf(press.Name, press.Purpose, press.Kind, press.Workspace, press.Under, press.Line, press.Work);
        var doors = CreateDoorsOf();
        CreateResult? made = null;
        return TerminalSaid.PressAsync(
            _loop, press.Said,
            (output, _) => RepositoryCreateCommand.ApplyAsync(
                ask, string.IsNullOrWhiteSpace(press.Digest) ? null : press.Digest.Trim(), doors, output, cancellationToken, result => made = result),
            () => made is { Name: not null } ? PressReceipt.Of(made) : null);
    }

    // A creation's plan, `daoris repository create … --plan`'s twin: the ask as it resolves on this machine, the folder, line and identity, each step in
    // order, every refusal at once, its digest, and the first lines of each quest the press publishes. The press's own ground (the Confirm reads it
    // again) and the view's one judge at the ask. It reads the registry, the identities and the folder, and writes and asks nothing.
    [DriverRoute("REGISTRY_CREATE_PLAN", PipeClass.Open)]
    [ByEvent("REGISTRY_CREATE_PLAN", Untold = "the service's registry, driver.json, the saved workflows, the workspace's checkouts and the folder the department would be made in")]
    private async Task<object?> RegistryCreatePlanAsync(RegistryCreatePlanRead read, CancellationToken cancellationToken)
    {
        var plan = await RepositoryCreatePlans.PlanAsync(
            CreateDoorsOf(), CreateAskOf(read.Name, read.Purpose, read.Kind, read.Workspace, read.Under, read.Line, read.Work), cancellationToken).ConfigureAwait(false);
        return CreatePlanWire.Of(plan, DateOnly.FromDateTime(DateTime.Now));
    }

    /// <summary>The ask the press's words make: each trimmed, a blank one none, the kind a repository where none is said.</summary>
    private static CreateAsk CreateAskOf(string? name, string? purpose, string? kind, string? workspace, string? under, string? line, string? work)
    {
        static string? Said(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return new CreateAsk((name ?? "").Trim(), (purpose ?? "").Trim())
        {
            Kind = Said(kind) ?? PlaceKind.Git,
            Workspace = Said(workspace),
            Under = Said(under),
            Line = Said(line),
            Work = Said(work),
        };
    }

    private CreateDoors CreateDoorsOf()
    {
        var service = Service();
        var config = DriverConfig.Load(_loop.ConfigPath);
        return new CreateDoors(
            new RepositoryCreateWorld(service, _loop.Home), _context.SetupWorldOf(service), _loop.Home, _context.Unadopted(config),
            () => DriverConfig.Load(_loop.ConfigPath), _context.Change, DateOnly.FromDateTime(DateTime.Now));
    }

    private Task<object?> SaidAsync(bool said, Func<TextWriter, TextWriter, Task<int>> run) => TerminalSaid.PressAsync(_loop, said, run);

    private ServiceClient Service() => _loop.Service ?? throw NotReady();
}
