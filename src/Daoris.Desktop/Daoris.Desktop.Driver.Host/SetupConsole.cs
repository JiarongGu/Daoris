namespace Daoris.Driver.Host;

/// <summary>
/// A set-up from a terminal (LAYOUT7, D117 §6.1, D50): <c>daoris-driver setup &lt;repository&gt; [--plan]</c>. The words
/// are the library's (<see cref="SetupCommand"/>); this wires the real world: the service, the home's choices and
/// rules, the repository's line, and the tools a child of Daoris finds.
/// </summary>
internal static class SetupConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
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
}
