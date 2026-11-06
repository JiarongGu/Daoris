namespace Daoris.Driver.Host;

/// <summary>
/// Sessions from a terminal (SESSUX1g, D126 §7.1, D50): <c>daoris session list</c>. The words are the library's
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
        var unadopted = Unadopted(config);

        return await SessionsCommand.RunAsync(ask, new SessionsWorld(service, home, config, unadopted, log)
        {
            // RPCOPEN1: the list is the running Daoris's own look (SESSION_GROUPS) where one answers, in the same words.
            Instance = InstanceHost.World(home, "sessions"),
            // MSG1f3, RPC1g3 (D238): *Start a conversation with these words* opens it in the running Daoris and holds it, as `chat` does.
            StartFrom = (id, _) => ChatConsole.StartFromAsync(id),
            // MIXAGENT1a-i (D163), RPC1g3: a conversation goes on with another agent in the running Daoris, and this terminal holds the new one.
            HandOver = (id, agent, account, _) => ChatConsole.HandOverAsync(id, agent, account),
        }, Console.Out).ConfigureAwait(false);
    }

    /// <summary>Whether the configured agent may drive a repository that has not adopted Daoris, as the loop plans by it (D70); a door this build does not know is refused (<see cref="UnknownDoorException"/>).</summary>
    internal static bool Unadopted(DriverConfig config)
    {
        try
        {
            return Adoption.Unadopted(ActorChoices.BuildAgent(config), ProviderRegistry.Built().AbilitiesOf);
        }
        catch (DriverException refused) when (refused is not UnknownDoorException)
        {
            return false;
        }
    }
}
