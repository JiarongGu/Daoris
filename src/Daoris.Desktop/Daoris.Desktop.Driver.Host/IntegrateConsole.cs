namespace Daoris.Driver.Host;

/// <summary>
/// A repository's integration from a terminal (INTEGRATE1h): <c>daoris-driver integrate &lt;repository&gt;</c> reads the record. The words are the
/// library's (<see cref="IntegrateCommand"/>); an act is the owner's card in the window, which this terminal does not answer.
/// </summary>
internal static class IntegrateConsole
{
    public static int Run(string[] args)
    {
        if (IntegrateCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"integrate: {problem}");
            Console.Error.WriteLine(IntegrateCommand.Usage);
            return 2;
        }

        if (ask.Act is not null)
        {
            Console.Out.WriteLine(IntegrateCommand.ActSaid(ask));
            return 1;
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
