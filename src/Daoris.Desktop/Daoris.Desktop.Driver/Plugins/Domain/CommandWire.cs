using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a command asks the person for before it can go on.</summary>
/// <param name="Name">The answer's name, sent back in the next round's <c>answers</c>.</param>
/// <param name="Prompt">The plugin's words for what to type.</param>
/// <param name="Masked">Whether what is typed is hidden as it is typed and never shown again.</param>
public sealed record CommandAsk(string Name, string Prompt, bool Masked);

/// <summary>One round of a command, as the plugin answered it.</summary>
/// <param name="Said">The plugin's lines for the person, in order.</param>
/// <param name="Ask">What it asks before it can go on; null where the command ended.</param>
/// <param name="Open">An address the person is to open, such as a sign-in page.</param>
/// <param name="Code">A code the person is to enter there.</param>
/// <param name="Exit">How it ended where it ended: 0 done, otherwise not.</param>
public sealed record CommandRound(IReadOnlyList<string> Said, CommandAsk? Ask, string? Open, string? Code, int Exit);

/// <summary>
/// A declared command's wire: <c>hook/command/run</c> asks one round, <c>{ command, args, answers, from }</c>, of a process started for
/// it; the answer says lines, may ask for one value, an address to open and a code, and ends with an <c>exit</c> where it asks nothing.
/// Each round is a process of its own, the answers given so far sent whole, so the plugin keeps nothing between rounds but what it
/// writes to its own storage.
/// </summary>
public static class CommandWire
{
    public const string Method = "hook/command/run";

    /// <summary>The most rounds one run takes before it is stopped as a command that never ends.</summary>
    public const int Rounds = 8;

    /// <summary>How long a round may take, a call to the plugin's platform among it.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    /// <summary>Who runs it, as the frame's <c>from</c> says: a terminal, the window, or an adoption of a git identity's token.</summary>
    public const string Terminal = "terminal";

    public const string Window = "window";

    public const string Adoption = "adoption";

    public static object Frame(string command, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> answers, string from) =>
        new { command, args, answers, from };

    /// <summary>A round's answer, read by its shape.</summary>
    /// <exception cref="DriverException">An answer of another shape: nothing of it is said.</exception>
    public static CommandRound Read(string plugin, JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object) throw Unread(plugin, "it is not an object");
        var said = new List<string>();
        if (answer.TryGetProperty("said", out var lines))
        {
            if (lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() > 40) throw Unread(plugin, "`said` is not a list of at most 40 lines");
            foreach (var line in lines.EnumerateArray())
            {
                if (line.ValueKind != JsonValueKind.String || line.GetString()!.Length > 2000) throw Unread(plugin, "a line of `said` is not a text");
                said.Add(line.GetString()!);
            }
        }

        CommandAsk? ask = null;
        if (answer.TryGetProperty("ask", out var asked) && asked.ValueKind != JsonValueKind.Null)
        {
            var name = Text(asked, "name");
            if (name is null || !PluginIds.NameShape.IsMatch(name) || Text(asked, "prompt") is not { } prompt)
                throw Unread(plugin, "`ask` needs a `name` and a `prompt`");
            ask = new(name, prompt, asked.TryGetProperty("masked", out var masked) && masked.ValueKind == JsonValueKind.True);
        }

        var open = Text(answer, "open");
        if (open is not null && (!Uri.TryCreate(open, UriKind.Absolute, out var address) || address.Scheme is not ("https" or "http")))
            throw Unread(plugin, "`open` is not a web address");
        var code = Text(answer, "code");
        if (code is { Length: > 64 }) throw Unread(plugin, "`code` is longer than 64");
        var exit = answer.TryGetProperty("exit", out var ended) && ended.ValueKind == JsonValueKind.Number && ended.TryGetInt32(out var number) ? number : 0;
        return new(said, ask, open, code, ask is null ? exit : 0);
    }

    private static string? Text(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString()!.Trim() is { Length: > 0 } text
            ? text
            : null;

    private static DriverException Unread(string plugin, string why) =>
        new($"plugin `{plugin}`'s answer to `command/run` is not one Daoris reads: {why}.");
}
