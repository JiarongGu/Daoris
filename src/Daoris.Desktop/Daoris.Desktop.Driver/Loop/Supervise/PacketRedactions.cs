namespace Daoris.Driver;

/// <summary>This machine's <see cref="PacketRedaction"/>: the secrets the wiring reader meets, the registry's checkouts that read, and the person's home folder.</summary>
internal static class PacketRedactions
{
    public static async Task<PacketRedaction> OfAsync(string home, ServiceClient service, CancellationToken ct)
    {
        var secrets = SystemWiring.Secrets(new WiringWorld
        {
            Home = home,
            BaseDirectory = AppContext.BaseDirectory,
            Process = Environment.GetEnvironmentVariable,
            Account = OperatingSystem.IsWindows() ? name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) : null,
            Machine = OperatingSystem.IsWindows() ? name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) : null,
        });
        var checkouts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var repository in await RefWatch.RepositoriesAsync(home, service.RegistryAsync, ct).ConfigureAwait(false)) checkouts.TryAdd(repository.Repository, repository.Root);
        }
        catch (Exception error) when (error is DriverException or IOException or HttpRequestException or System.Text.Json.JsonException)
        {
            // A registry that does not answer names no checkout; the home folder is still cut.
        }

        return new PacketRedaction(secrets, checkouts, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }
}
