using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Registration following its line, the repository row's *Refresh*: the screen's half of
/// <c>daoris repository register --repository &lt;name&gt;</c>. The page's bridge file for
/// this domain is <c>bridge/registry.ts</c>.
/// </summary>
public sealed class RegistryRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;
    private readonly OpenFolder? _openFolder = context.OpenFolder;

    // *Open* on a department's page: the folder the registry names for the repository, through the window kit's launcher. The module
    // names the folder, never the page, and nothing machine-local comes back but whether it opened.
    [DriverRoute("REGISTRY_OPEN_FOLDER", PipeClass.Window)]
    [Owner("REGISTRY_OPEN_FOLDER", None = OwnerNone.ActsOnNothing)]
    private async Task<object?> RegistryOpenFolderAsync(RegistryOpenFolder press, CancellationToken cancellationToken)
    {
        var repository = (press.Repository ?? "").Trim();
        var service = _loop.Service ?? throw NotReady();
        var folder = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken).ConfigureAwait(false), repository, null)
            .Select(row => row.Root).FirstOrDefault();
        if (folder is null) throw new DriverException($"`{repository}` has no folder here, so there is none to open.");
        if (!Directory.Exists(folder)) throw new DriverException($"the folder of `{repository}` is not there now.");
        if (_openFolder is null) return new { Opened = false };
        try
        {
            await _openFolder.OpenAsync(folder, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new DriverException($"the folder of `{repository}` would not open: {error.Message}");
        }

        return new { Opened = true };
    }

    // One repository registered from what its line declares, as `connect` would send it, through the loop's own client, so
    // its outcome reaches this machine's log as every follow's does. A refusal is an answer, as a landing's is: what the
    // row says, and why nothing is registered. Either way it declares the registry.
    [DriverRoute("REGISTRY_REFRESH", PipeClass.Open)]
    [Publishes("REGISTRY_REFRESH", StateScopes.Registry)]
    private async Task<object?> RegistryRefreshAsync(RegistryRefresh press, CancellationToken cancellationToken)
    {
        var repository = press.Repository;
        var service = _loop.Service ?? throw NotReady();
        var config = DriverConfig.Load(_loop.ConfigPath);

        var report = await RegistrationFollow.FollowAsync(new RegistrationWorld(service, _loop.Home, config), [repository], cancellationToken)
            .ConfigureAwait(false);
        var followed = report.Followed.Single();
        _loop.Nudge();
        // The record `daoris repository register` prints from, read back out of this answer by `RegistrationFollowed.Read`.
        return followed.ToWire(report.Refresh);
    }
}
