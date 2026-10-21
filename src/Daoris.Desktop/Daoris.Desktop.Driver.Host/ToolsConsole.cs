namespace Daoris.Driver.Host;

/// <summary>
/// The programs Daoris runs beside its agents from a terminal (PUSH1f2c, D194, D50): <c>daoris-driver tool</c>, the words
/// <c>daoris tool …</c> hands this program. The grammar, the switch, the download and the words are the library's
/// (<see cref="ToolsCommand"/>); this hands it the process's terminal, the home the host resolves, and its machine log.
/// </summary>
internal static class ToolsConsole
{
    /// <summary>The verb's run, stopped by <paramref name="closing"/>: the person's Ctrl+C, which the families' one handler cancels.</summary>
    public static async Task<int> RunAsync(string[] args, MachineLog log, CancellationToken closing)
    {
        var world = new ToolsWorld
        {
            // Said under `daoris`, as the CLI said each of these verbs before it handed them here: the person typed `daoris tool …`.
            Instance = InstanceHost.World(DaorisHome.Resolve(), "daoris"),
            Log = log,
        };

        try
        {
            return await ToolsCommand.RunAsync(args, world, closing).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            // The token stopped a wait on the pipe or a look; a write under way completed, being atomic. A download says its own stop.
            await Console.Error.WriteLineAsync(
                "daoris: stopped at your Ctrl+C. A change already sent to Daoris's window may still be confirmed there, and "
                + "`daoris-driver result <id>` asks how it ended; nothing more was done here.").ConfigureAwait(false);
            return 2;
        }
    }
}
