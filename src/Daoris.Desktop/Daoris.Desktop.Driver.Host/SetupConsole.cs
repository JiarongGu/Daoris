namespace Daoris.Driver.Host;

/// <summary>
/// A set-up from a terminal (LAYOUT7, D117 §6.1, D50): <c>daoris-driver setup &lt;repository&gt; [--plan]</c>, and a whole
/// workspace's plan (WSSETUP6, D124 §4.5): <c>daoris-driver setup --workspace &lt;name&gt; …</c>. The words are the library's
/// (<see cref="SetupCommand"/>, <see cref="WorkspaceSetupCommand"/>); this wires the real world: the service, the home's
/// choices and rules, each repository's line, the tools a child of Daoris finds, and, for a plan, this host's machine log.
/// </summary>
internal static class SetupConsole
{
    public static async Task<int> RunAsync(string[] args, MachineLog log)
    {
        // A workspace is named by its flag, and asked before the words are read as one repository's.
        if (WorkspaceSetupCommand.Asks(args))
        {
            return await WorkspaceAsync(args, log).ConfigureAwait(false);
        }

        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (SetupCommand.Problem(args) is { } problem)
        {
            Console.Error.WriteLine($"setup: {problem}");
            Console.Error.WriteLine(SetupCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();

        return await SetupCommand.RunAsync(
            args, Console.Out, new SetupWorld(service, home), config, SetupWorld.DoorOf(config, home), home,
            DateOnly.FromDateTime(DateTime.Now)).ConfigureAwait(false);
    }

    private static async Task<int> WorkspaceAsync(string[] args, MachineLog log)
    {
        if (WorkspaceSetupCommand.Problem(args) is { } problem)
        {
            Console.Error.WriteLine($"setup: {problem}");
            Console.Error.WriteLine(WorkspaceSetupCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        service.SetupLined += line => SessionLog.WriteSetup(log, line);

        return await WorkspaceSetupCommand.RunAsync(
            args, Console.Out, new WorkspaceSetupWorld(service, home), config, SetupWorld.DoorOf(config, home), home,
            DateOnly.FromDateTime(DateTime.Now), DateTimeOffset.UtcNow).ConfigureAwait(false);
    }
}
