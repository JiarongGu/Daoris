using System.Text.Json;
using static Daoris.Driver.PluginInterfaceFields;

namespace Daoris.Driver;

/// <summary>One argument of a command: its name, its type, the words the window labels it with, and whether it must be given.</summary>
/// <param name="Choices">The values a <c>choice</c> takes; none for another type.</param>
public sealed record PluginCommandArg(string Name, string Type, AuthoredText Title, bool Required, IReadOnlyList<string> Choices);

/// <summary>A command entry read: the command, or the problem that refuses it alone, by its name where it has one.</summary>
internal sealed record PluginCommandRead(PluginCommandDeclared? Command, string? Name, string? Problem);

/// <summary>Reads one entry of <c>interfaces.commands</c>, each field as the plugin UI design's §3.3 declares it.</summary>
internal static class PluginCommandReader
{
    /// <summary>The records whose menus may offer a command (<c>on</c>), the record's id passed as the argument of its kind.</summary>
    public static readonly IReadOnlyList<string> Records = ["session", "quest", "run", "repository", "workspace"];

    /// <summary>The types an argument takes.</summary>
    public static readonly IReadOnlyList<string> Types = ["text", "number", "boolean", "choice"];

    public const int TitleMost = 80;

    public const int AboutMost = 2000;

    public const int ConfirmMost = 200;

    /// <param name="before">The commands read before it, which its name and its adoption are judged against.</param>
    public static PluginCommandRead Read(JsonElement row, IReadOnlyList<PluginCommandDeclared> before)
    {
        var name = Text(row, "name");
        if (name is null || !PluginIds.NameShape.IsMatch(name))
            return new(null, null, "a declared command needs a `name` — lowercase letters, digits and dashes, at most 40.");
        PluginCommandRead Refused(string problem) => new(null, name, problem);
        if (before.Any(command => command.Name == name)) return Refused($"command `{name}` is declared twice.");
        if (!Words(row, "about", AboutMost, oneLine: false, null, out var about) || about is null)
            return Refused($"command `{name}` needs an `about`: what it does, in a sentence.");
        if (!Words(row, "title", TitleMost, oneLine: true, AuthoredText.Of(name), out var title))
            return Refused($"command `{name}`'s `title` must be words on one line of at most {TitleMost}: a string, or an object of language tags to text.");
        if (Flag(row, "interactive") is not { } interactive)
            return Refused($"command `{name}`'s `interactive` must be true or false: whether it asks the person.");
        if (Flag(row, "tool") is not { } tool) return Refused($"command `{name}`'s `tool` must be true or false: whether an agent may call it.");
        if (tool && interactive) return Refused($"command `{name}` asks the person, so it is never an agent's tool.");
        if (Flag(row, "writes") is not { } writes)
            return Refused($"command `{name}`'s `writes` must be true or false: whether it changes something outside the plugin.");
        if (!Words(row, "confirm", ConfirmMost, oneLine: true, null, out var confirm))
            return Refused($"command `{name}`'s `confirm` must be words on one line of at most {ConfirmMost}: the question the window asks once before it runs.");
        if (Closed(row, "on", Records, [], atLeastOne: false) is not { } on)
            return Refused($"command `{name}`'s `on` must list the records whose menus offer it, each once: {Listed(Records)}.");
        if (Args(row, name, out var args) is { } wrong) return Refused(wrong);

        PluginAdoption? adopts = null;
        if (row.TryGetProperty("adopts", out var adopted))
        {
            var hosts = adopted.ValueKind == JsonValueKind.Object && adopted.TryGetProperty("hosts", out var named) && named.ValueKind == JsonValueKind.Array
                ? named.EnumerateArray().Select(host => host.ValueKind == JsonValueKind.String ? host.GetString()!.Trim().ToLowerInvariant() : "").ToList()
                : [];
            if (hosts.Count == 0 || hosts.Any(host => host.Length == 0) || Text(adopted, "answer") is not { } answer || !PluginIds.NameShape.IsMatch(answer))
                return Refused($"command `{name}`'s `adopts` must name its `hosts` and the `answer` the token is sent as.");
            if (before.Any(command => command.Adopts is not null)) return Refused("only one command may adopt a git identity's API token.");
            adopts = new(hosts, answer);
        }

        return new(new(name, about, interactive, tool, adopts) { Title = title!, Args = args, On = on, Writes = writes, Confirm = confirm }, name, null);
    }

    /// <summary>Its arguments, or the problem with the first that does not read.</summary>
    private static string? Args(JsonElement row, string command, out IReadOnlyList<PluginCommandArg> args)
    {
        var read = new List<PluginCommandArg>();
        args = read;
        if (!row.TryGetProperty("args", out var declared)) return null;
        if (declared.ValueKind != JsonValueKind.Array) return $"command `{command}`'s `args` must be a list of its arguments.";
        foreach (var each in declared.EnumerateArray())
        {
            var name = Text(each, "name");
            if (name is null || !PluginIds.NameShape.IsMatch(name))
                return $"command `{command}`'s arguments each need a `name` — lowercase letters, digits and dashes, at most 40.";
            if (read.Any(arg => arg.Name == name)) return $"command `{command}` declares the argument `{name}` twice.";
            var type = Text(each, "type");
            if (type is null || !Types.Contains(type, StringComparer.Ordinal))
                return $"command `{command}`'s argument `{name}` needs a `type`: `text`, `number`, `boolean` or `choice`.";
            if (!Words(each, "title", TitleMost, oneLine: true, AuthoredText.Of(name), out var title))
                return $"command `{command}`'s argument `{name}`'s `title` must be words on one line of at most {TitleMost}.";
            if (Flag(each, "required") is not { } required) return $"command `{command}`'s argument `{name}`'s `required` must be true or false.";
            IReadOnlyList<string> choices = [];
            if (type == "choice")
            {
                var values = each.TryGetProperty("choices", out var listed) && listed.ValueKind == JsonValueKind.Array
                    ? listed.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : "").ToList()
                    : [];
                if (values.Count == 0 || values.Any(value => value.Length is 0 or > 40) || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
                    return $"command `{command}`'s argument `{name}` is a `choice`, so it needs `choices`: a list of at least one value, each once.";
                choices = values;
            }
            else if (each.TryGetProperty("choices", out _))
            {
                return $"command `{command}`'s argument `{name}` has `choices`, which only a `choice` takes.";
            }

            read.Add(new(name, type, title!, required, choices));
        }

        return null;
    }
}
