namespace Daoris.Driver;

/// <summary>
/// <c>daoris &lt;plugin&gt; &lt;command&gt; [args…] [--stdin]</c>: a command the plugin declares, run round by round in its own process
/// (<see cref="PluginCommandRun"/>), its lines said, an address and a code shown, and each value it asks typed here, hidden where it
/// says so, or read line by line from standard input with <c>--stdin</c>. The person's own: a session's terminal is refused. Exit: the
/// command's own · 1 a session's terminal · 2 a refusal or a plugin that never ends.
/// </summary>
public static class PluginCommandConsole
{
    /// <summary>The words a person's terminal reads and writes; a test's stand-in.</summary>
    public sealed record Terminal(TextWriter Out, TextWriter Error, Func<bool, string?> ReadLine, Func<string, string?> Environment);

    /// <summary>Whether <paramref name="word"/> names an installed plugin that runs commands, so the command is its.</summary>
    public static bool Names(string home, string word) =>
        PluginCatalog.Load(home).Plugins.Any(each => string.Equals(each.Manifest.Id, word, StringComparison.OrdinalIgnoreCase)
            && each.Manifest.Interfaces.Commands.Count > 0);

    public static async Task<int> RunAsync(string home, string[] args, Terminal terminal, CancellationToken ct, PluginCommandRun.Starter? start = null)
    {
        if (terminal.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            await terminal.Error.WriteLineAsync($"daoris {args[0]}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
            return 1;
        }

        var stdin = args.Contains("--stdin", StringComparer.Ordinal);
        var words = args.Where(arg => arg != "--stdin").ToList();
        try
        {
            if (words.Count < 2) throw new DriverException(Usage(home, words[0]));
            var (plugin, command) = PluginCommandRun.Find(home, words[0], words[1]);
            var answers = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var round = 0; round < CommandWire.Rounds; round++)
            {
                var said = await PluginCommandRun.RoundAsync(home, plugin, command.Name, words[2..], answers, CommandWire.Terminal, ct, start).ConfigureAwait(false);
                foreach (var line in said.Said) await terminal.Out.WriteLineAsync(line).ConfigureAwait(false);
                if (said.Open is { } open) await terminal.Out.WriteLineAsync($"Open: {open}").ConfigureAwait(false);
                if (said.Code is { } code) await terminal.Out.WriteLineAsync($"Code: {code}").ConfigureAwait(false);
                if (said.Ask is not { } ask) return said.Exit;
                if (!stdin) await terminal.Out.WriteAsync($"{ask.Prompt}: ").ConfigureAwait(false);
                var typed = terminal.ReadLine(ask.Masked && !stdin);
                if (typed is null) throw new DriverException($"`{command.Name}` asked for {ask.Prompt}, and none came.");
                answers[ask.Name] = typed.Trim();
            }

            throw new DriverException($"`{command.Name}` asked {CommandWire.Rounds} times without ending, so it was stopped.");
        }
        catch (DriverException refused)
        {
            await terminal.Error.WriteLineAsync($"daoris {args[0]}: {refused.Message}").ConfigureAwait(false);
            return 2;
        }
    }

    private static string Usage(string home, string id) =>
        PluginCatalog.Load(home).Plugins.FirstOrDefault(each => string.Equals(each.Manifest.Id, id, StringComparison.OrdinalIgnoreCase)) is { } plugin
            ? $"name a command: {string.Join("; ", plugin.Manifest.Interfaces.Commands.Select(each => $"`daoris {plugin.Manifest.Id} {each.Name}` {each.About}"))}"
            : $"no plugin `{id}` is installed.";

    /// <summary>The person's own terminal: a hidden line where asked, typed keys never echoed.</summary>
    public static Terminal Console() => new(System.Console.Out, System.Console.Error, Hidden, System.Environment.GetEnvironmentVariable);

    private static string? Hidden(bool masked)
    {
        if (!masked || System.Console.IsInputRedirected) return System.Console.ReadLine();
        var typed = new System.Text.StringBuilder();
        while (true)
        {
            var key = System.Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (typed.Length > 0) typed.Length--; continue; }
            if (!char.IsControl(key.KeyChar)) typed.Append(key.KeyChar);
        }

        System.Console.WriteLine();
        return typed.ToString();
    }
}
