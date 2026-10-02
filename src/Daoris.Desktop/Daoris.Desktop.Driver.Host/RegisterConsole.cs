namespace Daoris.Driver.Host;

/// <summary>
/// Following a line from a terminal (WSSETUP5, D124 §3.1, D50): <c>daoris-driver register [--repository &lt;name&gt;]</c>. The
/// words are the library's (<see cref="RegisterCommand"/>); this wires the real world: the service, the home's choices,
/// each repository's line, and this host's machine log, where each outcome is a <c>registry.followed</c> line.
/// </summary>
internal static class RegisterConsole
{
    public static async Task<int> RunAsync(string[] args, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (RegisterCommand.Problem(args) is { } problem)
        {
            Console.Error.WriteLine($"register: {problem}");
            Console.Error.WriteLine(RegisterCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        service.RegistryFollowed += followed => SessionLog.WriteFollowed(log, followed);

        return await RegisterCommand.RunAsync(args, Console.Out, new RegistrationWorld(service, home, config)).ConfigureAwait(false);
    }
}
