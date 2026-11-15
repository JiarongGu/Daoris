namespace Daoris.Driver.Host;

/// <summary>
/// Whether Daoris may change a repository's registered checkout's files, from a terminal: <c>daoris place checkout</c>. The words and the
/// edit are the library's (<see cref="CheckoutWritesCommand"/>), on the driver's file alone.
/// </summary>
internal static class CheckoutWritesConsole
{
    public static int Run(string[] args)
    {
        if (CheckoutWritesCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"checkout: {problem}");
            Console.Error.WriteLine(CheckoutWritesCommand.Usage);
            return 2;
        }

        try
        {
            return CheckoutWritesCommand.Run(ask, DriverConfig.ResolvePath(), Console.Out);
        }
        catch (DriverConfigUnreadableException unreadable)
        {
            Console.Error.WriteLine($"checkout: {unreadable.Message}");
            return 2;
        }
    }
}
