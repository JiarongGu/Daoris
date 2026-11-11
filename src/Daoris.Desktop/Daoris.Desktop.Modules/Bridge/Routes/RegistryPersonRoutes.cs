using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// The registry's retire, re-wire and import as the person's presses of the pipe: <c>daoris repository retire|wire|import</c> run their own
/// path (<c>said</c>) in this Daoris, whose service client holds the person key, so the terminal's press is confirmed in the window and the
/// service is called as the person's, never keyless. A folder to import is an absolute machine path, which only this machine's service takes.
/// </summary>
public sealed class RegistryPersonRoutes(DriverContext context)
{
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

    private Task<object?> SaidAsync(bool said, Func<TextWriter, TextWriter, Task<int>> run) => TerminalSaid.PressAsync(_loop, said, run);

    private ServiceClient Service() => _loop.Service ?? throw NotReady();
}
