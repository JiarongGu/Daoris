namespace Daoris.Driver.Host;

/// <summary>
/// A repository's heavy gates from a terminal:<c>daoris gate run [--gate &lt;name&gt;]</c>,
/// run in a checkout. The words, the slot and the run are the library's (<see cref="HeavyRun"/>); this asks the running Daoris to run
/// them as its own (<see cref="HeavyAppRun"/>), and where none takes them runs them here, handing the run the home, this process's
/// folder and environment; Ctrl+C is the run's stop either way.
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
                "run-heavy: the heavy-run slot is kept under Daoris's home, and none is named here: run the installed daoris, or name the home with DAORIS_HOME.");
            return 2;
        }

        // A look reads the slot's records and runs nothing, so it has nothing to stop.
        if (ask.Look) return HeavyRun.Look(HeavyRunWorld.OfThisProcess(home), Console.Out);

        // Ctrl+C stops the run, never this process outright: what the gates started is ended and waited for, then the slot is let go.
        using var stopping = new CancellationTokenSource();
        Action interrupt = stopping.Cancel;
        ConsoleCancelEventHandler stop = (_, press) =>
        {
            press.Cancel = true;
            interrupt();
        };
        Console.CancelKeyPress += stop;
        try
        {
            // The running Daoris runs it as its own where it takes it, and this only waits; Ctrl+C then asks it to stop.
            IDisposable Hook(Action told)
            {
                interrupt = told;
                return new Unhooked(() => interrupt = stopping.Cancel);
            }

            var asked = await HeavyAppRun.AskAsync(args, HeavyTerminal.OfThisProcess(), InstanceHost.World(home, "run-heavy"), Hook).ConfigureAwait(false);
            if (asked is { } exit) return exit;
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

    private sealed class Unhooked(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
