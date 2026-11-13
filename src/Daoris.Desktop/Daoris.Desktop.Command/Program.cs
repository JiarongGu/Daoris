using Daoris.Driver;

namespace Daoris.Desktop.Command;

/// <summary>
/// <c>daoris</c>: starts the desktop's program with this command's own arguments, whole (<see cref="CommandLauncher.Run(IReadOnlyList{string}, string, TextWriter)"/>).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // The program ends on the same keys; this command waits for it and ends with its code, never before it.
        Console.CancelKeyPress += (_, press) => press.Cancel = true;
        return CommandLauncher.Run(args, AppContext.BaseDirectory, Console.Error);
    }
}
