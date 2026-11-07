namespace Daoris.Driver.Host;

/// <summary>
/// Sessions from a terminal:<c>daoris session list</c>. The words are the library's
/// (<see cref="SessionsCommand"/>); this wires the real world: the service, the home's choices and files, the configured
/// agent's wire, and this host's machine log, where an archive and a delete say the terminal's door.
/// </summary>
internal static class SessionsConsole
{
    public static async Task<int> RunAsync(string[] args, MachineLog log)
    {
        // The usage before the service is asked for: a mistyped line needs no service to be told so.
        if (SessionsCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"sessions: {problem}");
            Console.Error.WriteLine(SessionsCommand.Usage);
            return 2;
        }

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        var home = DriverConfig.HomeOf(configPath);
        using var service = ServiceClient.FromEnvironment();
        var unadopted = SetupWorld.Unadopted(config, home);

        return await SessionsCommand.RunAsync(ask, new SessionsWorld(service, home, config, unadopted, log)
        {
            // The list is the running Daoris's own look (SESSION_GROUPS) where one answers, in the same words.
            Instance = InstanceHost.World(home, "sessions"),
            // *Start a conversation with these words* opens it in the running Daoris and holds it, as `chat` does.
            StartFrom = (id, _) => ChatConsole.StartFromAsync(id),
            // A conversation goes on with another agent in the running Daoris, and this terminal holds the new one.
            HandOver = (id, agent, account, _) => ChatConsole.HandOverAsync(id, agent, account),
        }, Console.Out).ConfigureAwait(false);
    }
}
