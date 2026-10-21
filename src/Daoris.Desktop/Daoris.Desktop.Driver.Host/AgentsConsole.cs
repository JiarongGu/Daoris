namespace Daoris.Driver.Host;

/// <summary>
/// The agents' rules, trust and settings from a terminal (PUSH1f2d, D194, D50): <c>daoris-driver agent</c>, the words
/// <c>daoris agent rules|trust|settings …</c> hands this program. The grammar, the switch and the words are the library's
/// (<see cref="AgentsCommand"/>); this hands it the process's terminal and the home the host resolves.
/// </summary>
internal static class AgentsConsole
{
    /// <summary>The verb's run, stopped by <paramref name="closing"/>: the person's Ctrl+C, which the families' one handler cancels.</summary>
    public static async Task<int> RunAsync(string[] args, CancellationToken closing)
    {
        // Said under `daoris`, as the CLI said each of these verbs before it handed them here: the person typed `daoris agent …`. The
        // rules are DAORIS_HOME's whatever DAORIS_HARNESS_CONFIG names, so the instance is looked for on that home.
        var world = new AgentsWorld { Instance = InstanceHost.World(DaorisHome.Resolve(), "daoris") };

        try
        {
            return await AgentsCommand.RunAsync(args, world, closing).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested)
        {
            // The token stopped a wait on the pipe; a write under way completed, being atomic.
            await Console.Error.WriteLineAsync(
                "daoris: stopped at your Ctrl+C. A change already sent to Daoris's window may still be confirmed there, and "
                + "`daoris-driver result <id>` asks how it ended; nothing more was done here.").ConfigureAwait(false);
            return 2;
        }
    }
}
