namespace Daoris.Driver.Host;

/// <summary>
/// A place's quick set-up planned or applied from a terminal (AGENTSPLACE1a2, AGENTSPLACE1b3): <c>daoris place &lt;repository&gt;… --preset &lt;id&gt; …
/// (--plan | --apply)</c>, <c>PLACE_PLAN</c>'s and <c>PLACE_APPLY</c>'s twin. The words are the library's (<see cref="PlaceCommand"/>); this wires the real world: the service, the home's
/// choices and plugins, and each repository's line.
/// </summary>
internal static class PlaceConsole
{
    public static async Task<int> RunAsync(string[] args, MachineLog log)
    {
        // A workspace is named by its flag, and asked before the words are read as a place's repositories.
        if (PlaceCommand.AsksWorkspace(args))
        {
            return await WorkspaceAsync(args, log).ConfigureAwait(false);
        }

        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (PlaceCommand.Read(args).Problem is { } problem)
        {
            Console.Error.WriteLine($"place: {problem}");
            Console.Error.WriteLine(PlaceCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        var plugins = PluginCatalog.Load(home, ProviderRegistry.Built().Names);
        var unadopted = SetupWorld.Unadopted(config, home);
        // An apply alone edits the driver's file as the window's press does: read, edited and written under its own lock.
        var apply = new PlaceApplyDoors(
            new PlaceWorld(service), new SetupWorld(service, home), home, plugins, unadopted, () => DriverConfig.Load(configPath),
            change => change(DriverConfig.Load(configPath)).Save(configPath), DateOnly.FromDateTime(DateTime.Now));
        return await PlaceCommand.RunAsync(
            args, Console.Out, new PlaceWorld(service), config, home, plugins, unadopted, InstanceHost.World(home, "place"), apply).ConfigureAwait(false);
    }

    private static async Task<int> WorkspaceAsync(string[] args, MachineLog log)
    {
        if (WorkspaceSetupCommand.Problem(args) is { } problem)
        {
            Console.Error.WriteLine($"place: {problem}");
            Console.Error.WriteLine(WorkspaceSetupCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        service.SetupLined += line => SessionLog.WriteSetup(log, line);

        return await WorkspaceSetupCommand.RunAsync(
            args, Console.Out, new WorkspaceSetupWorld(service, home), config, SetupWorld.Unadopted(config, home), home,
            DateOnly.FromDateTime(DateTime.Now), DateTimeOffset.UtcNow).ConfigureAwait(false);
    }
}
