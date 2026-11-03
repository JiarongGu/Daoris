using System.ComponentModel;
using System.Diagnostics;

namespace Daoris.Driver.Host;

/// <summary>
/// A doctrine command handed to the CLI this install carries (ONEWAY1ai-i; the oneway design §11): <c>node &lt;app/cli/…/daoris.mjs&gt;
/// &lt;words&gt;</c> on the node PATH finds, with the terminal's own streams, marked as handed so the CLI never hands it back. Its exit is the
/// run's; a start that fails is 2, said.
/// </summary>
internal static class DoctrineConsole
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (CommandTree.CarriedCli(AppContext.BaseDirectory) is not { } cli)
        {
            await Console.Error.WriteLineAsync($"daoris: {CommandTree.NoCli(args[0])}").ConfigureAwait(false);
            return 2;
        }

        // inherits its console: the doctrine command writes to the person's terminal, as this program does.
        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = false };
        info.ArgumentList.Add(cli);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment[CommandTree.HandedVariable] = "driver";

        Process? process;
        try
        {
            process = await ProcessStarts.Shared.StartAsync(info, StartClass.Person, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            process = null;
        }

        if (process is null)
        {
            await Console.Error.WriteLineAsync($"daoris: {CommandTree.NoNode}").ConfigureAwait(false);
            return 2;
        }

        using (process)
        {
            // Ctrl+C reaches node too, which ends its command; this process waits for that end rather than ending first.
            Console.CancelKeyPress += (_, press) => press.Cancel = true;
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode is 0 or 1 or 2 ? process.ExitCode : 2;
        }
    }
}
