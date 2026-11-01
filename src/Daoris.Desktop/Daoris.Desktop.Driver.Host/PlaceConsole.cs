namespace Daoris.Driver.Host;

/// <summary>
/// A place's quick set-up planned from a terminal (AGENTSPLACE1a2): <c>daoris-driver place &lt;repository&gt;… --preset &lt;id&gt; … --plan</c>,
/// <c>PLACE_PLAN</c>'s twin. The words are the library's (<see cref="PlaceCommand"/>); this wires the real world: the service, the home's
/// choices and plugins, and each repository's line.
/// </summary>
internal static class PlaceConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
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
        return await PlaceCommand.RunAsync(
            args, Console.Out, new PlaceWorld(service), config, home, PluginCatalog.Load(home, AdapterSet.Built().Names), SetupWorld.DoorOf(config, home),
            InstanceHost.World(home, "place")).ConfigureAwait(false);
    }
}
