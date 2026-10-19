namespace Daoris.Driver.Host;

/// <summary>
/// A repository's session access setup from a terminal (AUTONOMY1a, D184 point 1, D50): <c>daoris-driver access</c>. The words and the
/// edit are the library's (<see cref="AccessCommand"/>), on the driver's file alone; this asks the service's registry only for the
/// workspace a repository's setup is resolved through, and says so where the service did not answer.
/// </summary>
internal static class AccessConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The usage before anything is read: a mistyped line needs no file to be told so.
        if (AccessCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"access: {problem}");
            Console.Error.WriteLine(AccessCommand.Usage);
            return 2;
        }

        try
        {
            var place = ask is { Repository: { } repository, Edit: null } ? await PlaceAsync(repository).ConfigureAwait(false) : null;
            return AccessCommand.Run(ask, DriverConfig.ResolvePath(), place, Console.Out);
        }
        catch (DriverConfigUnreadableException unreadable)
        {
            Console.Error.WriteLine($"access: {unreadable.Message}");
            return 2;
        }
    }

    /// <summary>The repository's workspace as the registry names it, or that it could not be read: a say is never refused for want of the service.</summary>
    private static async Task<AccessPlace> PlaceAsync(string repository)
    {
        try
        {
            using var service = ServiceClient.FromEnvironment();
            var registry = await service.RegistryAsync().ConfigureAwait(false);
            var known = registry.FirstOrDefault(row => string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase));
            return new AccessPlace(known?.Workspace, Known: true);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or DriverException or InvalidOperationException)
        {
            return new AccessPlace(null, Known: false);
        }
    }
}
