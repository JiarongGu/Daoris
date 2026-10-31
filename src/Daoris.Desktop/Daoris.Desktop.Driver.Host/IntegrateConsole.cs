namespace Daoris.Driver.Host;

/// <summary>
/// A repository's integration from a terminal (INTEGRATE1h): <c>daoris-driver integrate &lt;repository&gt;</c> reads the record. The words are the
/// library's (<see cref="IntegrateCommand"/>); an act is the owner's card in the running window, pressed over the pipe and waited on.
/// </summary>
internal static class IntegrateConsole
{
    public static async Task<int> RunAsync(string[] args, CancellationToken closing)
    {
        if (IntegrateCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"integrate: {problem}");
            Console.Error.WriteLine(IntegrateCommand.Usage);
            return 2;
        }

        if (ask.Act is not null)
        {
            try
            {
                return await IntegrateCommand.PressAsync(ask, InstanceHost.World(DaorisHome.Resolve(), "integrate"), closing).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (closing.IsCancellationRequested)
            {
                await Console.Error.WriteLineAsync(InstanceDoor.StoppedAtCtrlC).ConfigureAwait(false);
                return 2;
            }
        }

        if (DaorisHome.Resolve() is not { } home)
        {
            Console.Error.WriteLine("integrate: the integration is kept under Daoris's home, and none is named here: run the installed daoris-driver, or name the home with DAORIS_HOME.");
            return 2;
        }

        foreach (var line in IntegrateCommand.Said(IntegrationLooks.Of(new IntegrationStore(home), ask.Repository))) Console.Out.WriteLine(line);
        return 0;
    }
}
