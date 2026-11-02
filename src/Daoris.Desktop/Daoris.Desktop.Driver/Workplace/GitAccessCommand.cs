using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <see cref="GitAccessCommand"/> is handed from the world.</summary>
/// <param name="Home">The home the verbs act on.</param>
/// <param name="WindowRuns">Whether a Daoris with a window runs on this home, asked only before a widening or an answer.</param>
/// <param name="Environment">The variables, read for a session's marker.</param>
/// <param name="Workspaces">The workspace a repository belongs to (the registry's), asked only to say a repository's resolved values.</param>
public sealed record GitAccessWorld(string Home, Func<Task<bool>> WindowRuns, Func<string, string?> Environment, Func<string, Task<string?>> Workspaces);

/// <summary>
/// The terminal's git access and git go-aheads (GITAUTH1f, D198 points 8, 10 and 12; the git-auth design §8): <c>git access</c>,
/// <c>git asks</c> and <c>git answer</c>, each change one typed operation (<see cref="GitAccessActs"/>, <see cref="GitAsks"/>), one text
/// per verb, a refusal one line. From a session's environment every change is refused (D194 point 5): an agent changes no access,
/// its own included, and answers no go-ahead. While the window runs a widening, or an answer, is refused saying so, as
/// <see cref="GitIdentityCommand"/>'s are: the window confirms them.
/// </summary>
public static class GitAccessCommand
{
    public const string Usage =
        """
        usage: daoris-driver git access [<repository>|--workspace <workspace>|--machine] [read=<v>] [write=<v>] [--one-value <v>] [--clear] [--json]
               daoris-driver git asks [--json]
               daoris-driver git answer <ask> allow|refuse
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool Answers(IReadOnlyList<string> args) => args.Count > 0 && args[0] is "access" or "asks" or "answer";

    /// <summary>Whether the words change the home, so the verb holds its lease: an edit or an answer.</summary>
    public static bool Changes(IReadOnlyList<string> args) =>
        Answers(args) && !args.Any(arg => arg is "--help" or "-h")
        && (args[0] == "answer" || (args[0] == "access" && args.Skip(1).Any(arg => arg.Contains('=') || arg is "--clear" or "--one-value")));

    public const string WindowFirst =
        "this change is confirmed in Daoris's window first while it runs: close Daoris and run it again here, or press it in Settings → Git, and nothing was done (window-first)";

    public static async Task<int> RunAsync(IReadOnlyList<string> args, GitAccessWorld world, TextWriter output, TextWriter error)
    {
        if (!Answers(args))
        {
            await error.WriteLineAsync($"git: `{(args.Count == 0 ? "" : args[0])}` is not a git verb.\n{Usage}").ConfigureAwait(false);
            return 2;
        }

        if (Changes(args) && world.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            await error.WriteLineAsync($"git: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
            return 1;
        }

        try
        {
            var json = args.Contains("--json");
            return args[0] switch
            {
                "asks" => Asks(world.Home, json, output),
                "answer" when args is [_, var id, "allow" or "refuse"] => await AnswerAsync(world, id, args[2] == "allow", output).ConfigureAwait(false),
                "answer" => Refuse(error, $"git: those words are not a git verb.\n{Usage}", 2),
                _ => await AccessAsync(world, args, json, output).ConfigureAwait(false),
            };
        }
        catch (DriverException refused)
        {
            await error.WriteLineAsync(refused.Message.StartsWith("git: ", StringComparison.Ordinal) ? refused.Message : $"git: {refused.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static int Refuse(TextWriter error, string text, int code)
    {
        error.WriteLine(text);
        return code;
    }

    private static int Asks(string home, bool json, TextWriter output)
    {
        var asks = GitAsks.List(home, DateTimeOffset.UtcNow);
        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(asks.Select(each => new
            {
                each.Id, each.Repository, each.Workspace, each.Remote, each.Host, each.Class, each.Session, At = each.At.ToString("O"), each.Gone,
            }), JsonOptions));
            return 0;
        }

        if (asks.Count == 0) output.WriteLine("git: no git go-ahead waits.");
        foreach (var ask in asks)
        {
            output.WriteLine($"{ask.Id}  {Said(ask)}{(ask.Gone ? " (its command has stopped: an allow is kept for its retry)" : "")}");
        }

        return 0;
    }

    /// <summary>A go-ahead in a line: <i>a push to origin</i>, the repository and the session where known.</summary>
    public static string Said(GitAsk ask) =>
        $"{(ask.Class == GitAccessKeys.Write ? "a push to" : "a fetch of")} {ask.Remote ?? ask.Host ?? "a remote"}"
        + (ask.Repository is null ? "" : $" in {ask.Repository}") + (ask.Session is null ? "" : $" by session {ask.Session}");

    private static async Task<int> AnswerAsync(GitAccessWorld world, string id, bool allow, TextWriter output)
    {
        if (await world.WindowRuns().ConfigureAwait(false)) throw new DriverException(WindowFirst);
        var waiting = GitAsks.Answer(world.Home, id, allow, DateTimeOffset.UtcNow);
        await output.WriteLineAsync(allow
            ? waiting ? "git: allowed; the act goes on." : "git: allowed; its command has stopped, so the allow is kept for its identical retry for 15 minutes."
            : "git: refused.").ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> AccessAsync(GitAccessWorld world, IReadOnlyList<string> args, bool json, TextWriter output)
    {
        var edit = new GitAccessEdit();
        var set = new List<KeyValuePair<string, string?>>();
        string? oneValue = null;
        var clear = false;
        for (var at = 1; at < args.Count; at++)
        {
            var arg = args[at];
            if (arg == "--json") continue;
            if (arg == "--machine") edit = edit with { Machine = true };
            else if (arg == "--clear") clear = true;
            else if (arg == "--workspace" && at + 1 < args.Count) edit = edit with { Workspace = args[++at] };
            else if (arg == "--one-value" && at + 1 < args.Count) oneValue = args[++at];
            else if (arg.Contains('=') && !arg.StartsWith("--", StringComparison.Ordinal))
            {
                var split = arg.IndexOf('=');
                set.Add(new(arg[..split], arg[(split + 1)..]));
            }
            else if (!arg.StartsWith("--", StringComparison.Ordinal) && edit.Repository is null) edit = edit with { Repository = arg };
            else throw new DriverException($"those words are not a git verb.\n{Usage}");
        }

        var changing = set.Count > 0 || oneValue is not null || clear;
        edit = edit with { Set = set.Count > 0 ? set : null, OneValue = oneValue, Clear = clear };
        var levels = GitAccessKeys.Load(world.Home);
        if (!changing)
        {
            var workspace = edit.Workspace ?? (edit.Repository is null ? null : await world.Workspaces(edit.Repository).ConfigureAwait(false));
            var resolved = GitAccessKeys.Resolve(levels, edit.Repository, workspace);
            if (json) await output.WriteLineAsync(JsonSerializer.Serialize(resolved.Classes.Select(each => new { each.Class, each.Value, each.Source }), JsonOptions)).ConfigureAwait(false);
            else
            {
                foreach (var each in resolved.Classes) await output.WriteLineAsync($"{each.Class,-10} {Said(each)}").ConfigureAwait(false);
            }

            return 0;
        }

        var widened = GitAccessActs.Widened(levels, edit);
        if (widened.Count > 0 && await world.WindowRuns().ConfigureAwait(false)) throw new DriverException(WindowFirst);
        GitAccessActs.Apply(world.Home, edit);
        await output.WriteLineAsync("git: git access changed.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>A class in a phrase: its value, and where it came from.</summary>
    public static string Said(GitAccessClass each) =>
        (each.Value == GitAccessKeys.AsToday ? "as today (the landing pushes on your yes; sessions do not push)" : each.Value)
        + (each.Source == GitAccessKeys.DefaultSource ? "" : $"  (set for the {each.Source})");
}
