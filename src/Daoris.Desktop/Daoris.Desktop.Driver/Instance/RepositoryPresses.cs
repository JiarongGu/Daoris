namespace Daoris.Driver;

/// <summary>
/// <c>daoris repository retire|wire|import</c>, the registry's changes the person makes: each the person's press through the running
/// Daoris's window where one answers, its lines the route's; else the service's own door called from this process, which refuses a keyless
/// call. A refusal of the service is the family's exit (<see cref="RegistryRefusal.Exit"/>), a service that did not answer is exit 2.
/// </summary>
public static class RepositoryPresses
{
    /// <summary>Take <paramref name="repository"/> off the registry, in the service's own sentence.</summary>
    public static Task<int> RetireAsync(string repository, ServiceClient service, TextWriter output, TextWriter error, CancellationToken ct = default) =>
        GuardedAsync(error, async () =>
        {
            output.WriteLine(RegistryWords.Retired(await service.RetireAsync(repository, ct).ConfigureAwait(false)));
            return 0;
        });

    /// <summary>Wire <paramref name="repository"/> to <paramref name="workspace"/>: 0 wired, 1 refused.</summary>
    public static Task<int> WireAsync(string repository, string workspace, ServiceClient service, TextWriter output, TextWriter error, CancellationToken ct = default) =>
        GuardedAsync(error, async () =>
        {
            var (ok, message) = await service.WireRepositoryAsync(repository, workspace, ct).ConfigureAwait(false);
            (ok ? output : error).WriteLine($"daoris: {message}");
            return ok ? 0 : 1;
        });

    /// <summary>
    /// Leave out each glob of <paramref name="add"/> and take back each of <paramref name="remove"/> in the folder department named
    /// <paramref name="repository"/>, then read its manifest again with the new list where this machine's service holds its root: 0 set,
    /// 1 refused.
    /// </summary>
    public static Task<int> ExcludesAsync(
        string repository, IReadOnlyList<string> add, IReadOnlyList<string> remove, string home, ServiceClient service, TextWriter output, TextWriter error,
        CancellationToken ct = default) =>
        GuardedAsync(error, async () =>
        {
            var rows = await service.RegistryAsync(ct).ConfigureAwait(false);
            if (rows.FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase)) is not { } found)
            {
                error.WriteLine($"daoris: `{repository}` is not registered on this machine — use a folder's name as Repositories lists it.");
                return 1;
            }

            if (found.Kind != PlaceKind.Folder)
            {
                error.WriteLine($"daoris: `{found.Repository}` is not a folder: excludes are a folder department's, for the files its manifest passes by.");
                return 1;
            }

            var excludes = ExcludesEdit.Apply(found.Excludes, add, remove);
            var (ok, message) = await service.SetFolderExcludesAsync(found.Repository, excludes, ct).ConfigureAwait(false);
            if (!ok)
            {
                error.WriteLine($"daoris: {message}");
                return 1;
            }

            output.WriteLine($"daoris: {message}");
            if (RegistryConnect.IsLocal(service.BaseUrl) && found.Root is { } root && Directory.Exists(root))
            {
                FolderFirstManifest.Keep(home, RemoteTarget.Workspace(found.Workspace), found.Repository, root, excludes);
                output.WriteLine("  Its manifest was read again with them.");
            }

            return 0;
        });

    /// <summary>Say the excludes of the folder department named <paramref name="repository"/>, beside the defaults every folder has.</summary>
    public static Task<int> ExcludesListAsync(string repository, ServiceClient service, TextWriter output, TextWriter error, CancellationToken ct = default) =>
        GuardedAsync(error, async () =>
        {
            var rows = await service.RegistryAsync(ct).ConfigureAwait(false);
            if (rows.FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase)) is not { Kind: PlaceKind.Folder } found)
            {
                error.WriteLine($"daoris: `{repository}` is not a folder registered on this machine.");
                return 1;
            }

            output.WriteLine(found.Excludes.Count == 0
                ? $"daoris: `{found.Repository}` leaves out nothing of its own."
                : $"daoris: `{found.Repository}` leaves out {string.Join(", ", found.Excludes)}.");
            output.WriteLine($"  Beside the defaults: {string.Join(", ", FolderExcludes.Default)}.");
            return 0;
        });

    /// <summary>Register what <paramref name="folder"/>'s subdirectories propose, or the service's own knowledge root where none is given.</summary>
    public static Task<int> ImportAsync(string? folder, string? workspace, ServiceClient service, TextWriter output, TextWriter error, CancellationToken ct = default) =>
        GuardedAsync(error, async () =>
        {
            output.WriteLine(RegistryWords.Imported(await service.ImportAsync(folder, workspace, ct).ConfigureAwait(false)));
            return 0;
        });

    /// <summary>The person's retire, pressed where Daoris runs and made here where none does.</summary>
    public static Task<int> PressRetireAsync(string repository, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.RegistryRetire, InstanceDoor.Said(("repository", repository)),
            () => RetireAsync(repository, service, output, Error(instance), ct), output, ct);

    /// <summary>The person's re-wire, pressed where Daoris runs and made here where none does.</summary>
    public static Task<int> PressWireAsync(string repository, string workspace, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.RegistryRewire, InstanceDoor.Said(("repository", repository), ("workspace", workspace)),
            () => WireAsync(repository, workspace, service, output, Error(instance), ct), output, ct);

    /// <summary>The person's excludes edit, pressed where Daoris runs and made here where none does.</summary>
    public static Task<int> PressExcludesAsync(
        string repository, IReadOnlyList<string> add, IReadOnlyList<string> remove, string home, ServiceClient service, TextWriter output, InstanceWorld? instance,
        CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.RegistryExcludes, InstanceDoor.Said(("repository", repository), ("add", add.Count > 0 ? add : null), ("remove", remove.Count > 0 ? remove : null)),
            () => ExcludesAsync(repository, add, remove, home, service, output, Error(instance), ct), output, ct);

    /// <summary>The person's import, pressed where Daoris runs and made here where none does; <paramref name="folder"/> is absolute.</summary>
    public static Task<int> PressImportAsync(string? folder, string? workspace, ServiceClient service, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.RegistryImport, InstanceDoor.Said(("folder", folder), ("workspace", workspace)),
            () => ImportAsync(folder, workspace, service, output, Error(instance), ct), output, ct);

    private static TextWriter Error(InstanceWorld? instance) => instance?.Error ?? Console.Error;

    private static async Task<int> GuardedAsync(TextWriter error, Func<Task<int>> run)
    {
        try
        {
            return await run().ConfigureAwait(false);
        }
        catch (RegistryRefusal refused)
        {
            error.WriteLine($"daoris: {refused.Message}");
            return refused.Exit;
        }
        catch (Exception failed) when (failed is DriverException or HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            error.WriteLine($"daoris: {failed.Message}");
            return 2;
        }
    }
}
