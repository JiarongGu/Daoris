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
