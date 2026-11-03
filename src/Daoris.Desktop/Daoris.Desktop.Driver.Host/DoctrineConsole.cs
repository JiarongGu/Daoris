using System.ComponentModel;
using System.Diagnostics;

namespace Daoris.Driver.Host;

/// <summary>
/// A doctrine command handed to the CLI this install carries (ONEWAY1ai-i; the oneway design §11): <c>node &lt;app/cli/…/daoris.mjs&gt;
/// &lt;words&gt;</c> on the tools' Node.js, with the terminal's own streams, marked as handed so the CLI never hands it back. Its exit is the
/// run's; a start that fails is 2, said.
/// </summary>
internal static class DoctrineConsole
{
    public static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (CommandWords.CarriedCli(AppContext.BaseDirectory) is not { } cli)
        {
            await Console.Error.WriteLineAsync($"daoris: {CommandWords.NoCli(args[0])}").ConfigureAwait(false);
            return 2;
        }

        var home = DaorisHome.Resolve(Environment.GetEnvironmentVariable, AppContext.BaseDirectory);
        var (node, why) = Node(home);
        if (node is null)
        {
            await Console.Error.WriteLineAsync($"daoris: {CommandWords.NoNode(why)}").ConfigureAwait(false);
            return 2;
        }

        var info = new ProcessStartInfo(node)
        {
            UseShellExecute = false,
            // inherits its console: the doctrine command writes to the person's terminal, as this program does.
            CreateNoWindow = false,
        };
        info.ArgumentList.Add(cli);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        Tools.Hand(info, home);
        info.Environment[CommandWords.HandedVariable] = "driver";

        Process? process;
        try
        {
            process = await ProcessStarts.Shared.StartAsync(info, StartClass.Person, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Win32Exception error)
        {
            await Console.Error.WriteLineAsync($"daoris: {CommandWords.NoNode(error.Message)}").ConfigureAwait(false);
            return 2;
        }

        if (process is null)
        {
            await Console.Error.WriteLineAsync($"daoris: {CommandWords.NoNode(null)}").ConfigureAwait(false);
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

    /// <summary>The tools' Node.js (TOOLS5): the home's choice where there is a home, else the one <c>PATH</c> finds; or why none runs.</summary>
    private static (string? File, string? Why) Node(string? home)
    {
        if (home is null) return (CommandPresence.Resolve("node", startable: true), null);
        try
        {
            var resolved = Tools.Resolve(home, "node");
            return (resolved.Refused ? null : resolved.File, resolved.Problem);
        }
        catch (DriverException error)
        {
            return (null, error.Message);
        }
    }
}
