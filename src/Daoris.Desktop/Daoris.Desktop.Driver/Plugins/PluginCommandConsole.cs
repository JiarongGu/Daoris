namespace Daoris.Driver;

/// <summary>
/// <c>daoris &lt;plugin&gt; &lt;command&gt; [args…] [--stdin]</c>: a command the plugin declares, run round by round in its own process
/// (<see cref="PluginCommandRun"/>), its lines said, an address and a code shown, and each value it asks typed here, hidden where it
/// says so, or read line by line from standard input with <c>--stdin</c>. Exit: the command's own · 2 a refusal or a plugin that never ends.
/// </summary>
public static class PluginCommandConsole
{
    /// <summary>The words a person's terminal reads and writes; a test's stand-in.</summary>
    public sealed record Terminal(TextWriter Out, TextWriter Error, Func<bool, string?> ReadLine, Func<string, string?> Environment);

    /// <summary>Whether <paramref name="word"/> names an installed plugin that runs commands, by its id or its id's tail (<see cref="PluginTerminalWord"/>).</summary>
    public static bool Names(string home, string word) =>
        PluginTerminalWord.Named(PluginCatalog.Load(home), word).Count > 0;

    public static async Task<int> RunAsync(string home, string[] args, Terminal terminal, CancellationToken ct, PluginCommandRun.Starter? start = null)
    {
        var stdin = args.Contains("--stdin", StringComparer.Ordinal);
        var words = args.Where(arg => arg != "--stdin").ToList();
        try
        {
            var id = PluginTerminalWord.IdOf(PluginCatalog.Load(home), words[0]);
            if (words.Count < 2) throw new DriverException(Usage(home, id));
            var (plugin, command) = PluginCommandRun.Find(home, id, words[1]);
            var given = CommandArgs.FromWords(command.Name, command.Args, words[2..]);
            var values = new Dictionary<string, object?>(given.Values, StringComparer.Ordinal);
            foreach (var missing in given.Missing)
            {
                if (!stdin) await terminal.Out.WriteAsync($"{missing.Title.AsWritten}: ").ConfigureAwait(false);
                values[missing.Name] = CommandArgs.Typed(command.Name, missing, terminal.ReadLine(false)?.Trim()
                    ?? throw new DriverException($"`{command.Name}` needs `{missing.Name}`, and none came."));
            }

            var answers = new Dictionary<string, string>(StringComparer.Ordinal);
            var call = new CommandCall(command.Name, CommandCall.NewRun(), values, answers, CommandWire.Terminal, PluginPlace.Machine, "en");
            for (var round = 0; round < CommandWire.Rounds; round++)
            {
                var said = await PluginCommandRun.RoundAsync(home, plugin, call with { Answers = new Dictionary<string, string>(answers) }, ct, start).ConfigureAwait(false);
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
            ? $"name a command: {string.Join("; ", plugin.Manifest.Interfaces.Commands.Select(each => $"`daoris {plugin.Manifest.Id} {each.Name}` {each.About.AsWritten}"))}"
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
