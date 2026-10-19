namespace Daoris.Driver.Host;

/// <summary>
/// A repository's heavy gates from a terminal (HEAVYRUN1a, D180's HEAVYRUN1 note): <c>daoris-driver run-heavy [--gate &lt;name&gt;]</c>,
/// run in a checkout. The words, the slot and the run are the library's (<see cref="HeavyRun"/>); this hands it the home, this
/// process's folder and environment, and Ctrl+C as its stop.
/// </summary>
internal static class HeavyConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The usage before anything is read: a mistyped line needs no home to be told so.
        if (HeavyRun.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"run-heavy: {problem}");
            Console.Error.WriteLine(HeavyRun.Usage);
            return 2;
        }

        if (DaorisHome.Resolve() is not { } home)
        {
            Console.Error.WriteLine(
                "run-heavy: the heavy-run slot is kept under Daoris's home, and none is named here: run the installed daoris-driver, or name the home with DAORIS_HOME.");
            return 2;
        }

        // Ctrl+C stops the run, never this process outright: what the gates started is ended and waited for, then the slot is let go.
        using var stopping = new CancellationTokenSource();
        ConsoleCancelEventHandler stop = (_, press) =>
        {
            press.Cancel = true;
            stopping.Cancel();
        };
        Console.CancelKeyPress += stop;
        try
        {
            return await HeavyRun.RunAsync(ask, HeavyRunWorld.OfThisProcess(home), Console.Out, stopping.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"run-heavy: {error.Message}");
            return 2;
        }
        finally
        {
            Console.CancelKeyPress -= stop;
        }
    }
}
