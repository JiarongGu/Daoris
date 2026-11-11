namespace Daoris.Driver.Host;

/// <summary>
/// A department made from a terminal: <c>daoris repository create &lt;name&gt; --purpose "…" [--plan]</c>, <c>REGISTRY_CREATE</c>'s twin. The words
/// are the library's (<see cref="RepositoryCreateCommand"/>); this wires the real world: the service, the home's choices and identities, and
/// the set-up press's world. <c>--plan</c> reads here and writes nothing; the apply is the person's press through the running Daoris.
/// </summary>
internal static class RepositoryCreateConsole
{
    public static async Task<int> RunAsync(string[] words, string folder, ServiceClient service)
    {
        var (parsed, problem) = RepositoryCreateCommand.Parse(words, folder);
        if (parsed is null)
        {
            Console.Error.WriteLine($"daoris: {problem}");
            Console.Error.WriteLine(RepositoryCreateCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var home = DriverConfig.HomeOf(configPath);
        // An apply alone edits the driver's file as the window's press does: read, edited and written under its own lock.
        var doors = new CreateDoors(
            new RepositoryCreateWorld(service, home), new SetupWorld(service, home), home, SetupWorld.Unadopted(DriverConfig.Load(configPath), home),
            () => DriverConfig.Load(configPath), change => change(DriverConfig.Load(configPath)).Save(configPath), DateOnly.FromDateTime(DateTime.Now));
        return parsed.Plan
            ? await RepositoryCreateCommand.PlanAsync(parsed.Ask, doors, Console.Out).ConfigureAwait(false)
            : await RepositoryCreateCommand.PressAsync(parsed.Ask, doors, InstanceHost.World(home, "repository"), Console.Out).ConfigureAwait(false);
    }
}
