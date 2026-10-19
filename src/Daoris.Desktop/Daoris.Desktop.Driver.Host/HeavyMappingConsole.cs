namespace Daoris.Driver.Host;

/// <summary>
/// This machine's heavy gates for a repository that marks none of its own, from a terminal (HEAVYRUN1b2a, D180's HEAVYRUN1b2a note,
/// D50): <c>daoris-driver heavy</c>. The words and the edit are the library's (<see cref="HeavyMappingCommand"/>), on the driver's
/// file alone; the repository is never touched.
/// </summary>
internal static class HeavyMappingConsole
{
    public static int Run(string[] args)
    {
        // The usage before anything is read: a mistyped line needs no file to be told so.
        if (HeavyMappingCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"heavy: {problem}");
            Console.Error.WriteLine(HeavyMappingCommand.Usage);
            return 2;
        }

        try
        {
            return HeavyMappingCommand.Run(ask, DriverConfig.ResolvePath(), Console.Out);
        }
        catch (DriverConfigUnreadableException unreadable)
        {
            Console.Error.WriteLine($"heavy: {unreadable.Message}");
            return 2;
        }
    }
}
