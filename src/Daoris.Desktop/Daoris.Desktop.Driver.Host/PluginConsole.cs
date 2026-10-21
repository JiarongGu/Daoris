namespace Daoris.Driver.Host;

/// <summary>
/// This machine's plugins from a terminal (PUSH1f2f, D194, D50): <c>daoris-driver plugin</c>, the words <c>daoris plugin …</c> hands this
/// program, beside the host's own <c>plugins</c> (the kit, a package, a plugin's page). The grammar, the switch and the words are the
/// library's (<see cref="PluginCommand"/>); this hands it the process's terminal and the home the host resolves.
/// </summary>
internal static class PluginConsole
{
    /// <summary>The verb's run, stopped by <paramref name="closing"/>: the person's Ctrl+C, which the families' one handler cancels.</summary>
    public static async Task<int> RunAsync(string[] args, CancellationToken closing)
    {
        // Said under `daoris`, as the CLI said each of these verbs before it handed them here: the person typed `daoris plugin …`.
        var world = new PluginWorld { Instance = InstanceHost.World(DaorisHome.Resolve(), "daoris") };

        try
        {
            return await PluginCommand.RunAsync(args, world, closing).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            // The token stopped a wait on the pipe; a copy or a move under way completed, each a folder moved whole.
            await Console.Error.WriteLineAsync(
                "daoris: stopped at your Ctrl+C. A change already sent to Daoris's window may still be confirmed there, and "
                + "`daoris-driver result <id>` asks how it ended; nothing more was done here.").ConfigureAwait(false);
            return 2;
        }
    }
}
