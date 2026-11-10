using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>The press on a department's page that opens its folder in the system's file manager: what a folder place is, which has no branch to look at.</summary>
public sealed class DepartmentOpenRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;
    private readonly OpenFolder? _openFolder = context.OpenFolder;

    /// <summary>
    /// Open the folder the registry names for <paramref name="press"/>'s repository, through the window kit's launcher. The module names the
    /// folder, never the page; nothing machine-local comes back, only whether it opened.
    /// </summary>
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
}

/// <summary>Open the folder of <paramref name="Repository"/>, as the registry names it.</summary>
public sealed record RegistryOpenFolder(string? Repository);
