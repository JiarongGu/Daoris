namespace Daoris.Driver.Host;

/// <summary>
/// Which checks a repository's sessions run under Current, from a terminal (FABRIC-PIPELINE1b, D192 point 4, D50):
/// <c>daoris-driver checks</c>. The words and the edit are the library's (<see cref="SessionChecksCommand"/>), on the driver's file alone.
/// </summary>
internal static class SessionChecksConsole
{
    public static int Run(string[] args)
    {
        // The usage before anything is read: a mistyped line needs no file to be told so.
        if (SessionChecksCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"checks: {problem}");
            Console.Error.WriteLine(SessionChecksCommand.Usage);
            return 2;
        }

        try
        {
            return SessionChecksCommand.Run(ask, DriverConfig.ResolvePath(), Console.Out);
        }
        catch (DriverConfigUnreadableException unreadable)
        {
            Console.Error.WriteLine($"checks: {unreadable.Message}");
            return 2;
        }
    }
}
