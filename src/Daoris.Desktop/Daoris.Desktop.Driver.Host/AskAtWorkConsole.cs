namespace Daoris.Driver.Host;

/// <summary>
/// Who is at work on an ask from a terminal:<c>daoris ask at-work &lt;id&gt; [--json]</c>. The words and the doors are the
/// library's (<see cref="AskAtWorkVerb"/>); this wires the real world as <c>sessions</c> does, the running Daoris's read where one answers.
/// </summary>
internal static class AskAtWorkConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (AskAtWorkVerb.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"ask: {problem}");
            Console.Error.WriteLine($"usage: daoris {AskAtWorkVerb.Usage}");
            return 2;
        }

        try
        {
            var configPath = DriverConfig.ResolvePath();
            var config = DriverConfig.Load(configPath);
            var home = DriverConfig.HomeOf(configPath);
            using var service = ServiceClient.FromEnvironment();
            var world = new SessionsWorld(service, home, config, SetupWorld.Unadopted(config, home), null) { Instance = InstanceHost.World(home, "ask") };
            return await AskAtWorkVerb.RunAsync(world, ask, Console.Out, CancellationToken.None).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            Console.Error.WriteLine($"ask: {error.Message}");
            return 2;
        }
        catch (HttpRequestException error)
        {
            Console.Error.WriteLine($"ask: could not reach the service — {error.Message}");
            return 2;
        }
    }
}
