using System.Globalization;

namespace Daoris.Driver;

/// <summary>A command's arguments by name, typed, and the required ones given no value, which the door asks.</summary>
public sealed record CommandArgs(IReadOnlyDictionary<string, object?> Values, IReadOnlyList<PluginCommandArg> Missing)
{
    /// <summary>
    /// The terminal's words as the declared arguments: <c>--&lt;name&gt; &lt;value&gt;</c>, a boolean's flag alone meaning true, and the other
    /// words filling the arguments not named, in declared order.
    /// </summary>
    /// <exception cref="DriverException">A word naming no argument, a value not of its type or choices, or more words than arguments.</exception>
    public static CommandArgs FromWords(string command, IReadOnlyList<PluginCommandArg> declared, IReadOnlyList<string> words)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var loose = new List<string>();
        for (var at = 0; at < words.Count; at++)
        {
            if (!words[at].StartsWith("--", StringComparison.Ordinal))
            {
                loose.Add(words[at]);
                continue;
            }

            var name = words[at][2..];
            var arg = declared.FirstOrDefault(each => each.Name == name)
                ?? throw new DriverException($"`{command}` takes no `--{name}`{Takes(declared)}.");
            var next = at + 1 < words.Count ? words[at + 1] : null;
            if (arg.Type == "boolean" && next is not ("true" or "false"))
            {
                values[name] = true;
                continue;
            }

            if (next is null) throw new DriverException($"`--{name}` needs a value.");
            values[name] = Typed(command, arg, next);
            at++;
        }

        var open = declared.Where(each => !values.ContainsKey(each.Name)).ToList();
        if (loose.Count > open.Count) throw new DriverException($"`{command}` takes {declared.Count} argument(s), and more were given{Takes(declared)}.");
        for (var at = 0; at < loose.Count; at++) values[open[at].Name] = Typed(command, open[at], loose[at]);
        return new(values, [.. declared.Where(each => each.Required && !values.ContainsKey(each.Name))]);
    }

    /// <summary>The window's or an agent's object of values by name, each of its declared type; null or absent is none given.</summary>
    /// <exception cref="DriverException">A name no argument has, or a value not of its type or choices.</exception>
    public static CommandArgs FromJson(string command, IReadOnlyList<PluginCommandArg> declared, System.Text.Json.JsonElement? given)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (given is { ValueKind: System.Text.Json.JsonValueKind.Object } named)
        {
            foreach (var each in named.EnumerateObject().Where(each => each.Value.ValueKind != System.Text.Json.JsonValueKind.Null))
            {
                var arg = declared.FirstOrDefault(one => one.Name == each.Name)
                    ?? throw new DriverException($"`{command}` takes no `{each.Name}`{Takes(declared)}.");
                values[arg.Name] = each.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number when arg.Type == "number" => each.Value.GetDouble(),
                    System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False when arg.Type == "boolean" => each.Value.GetBoolean(),
                    System.Text.Json.JsonValueKind.String => Typed(command, arg, each.Value.GetString()!),
                    _ => throw new DriverException($"`{command}`'s `{arg.Name}` is a {arg.Type}."),
                };
            }
        }

        return new(values, [.. declared.Where(each => each.Required && !values.ContainsKey(each.Name))]);
    }

    /// <summary>One value as its argument's type declares it.</summary>
    /// <exception cref="DriverException">A value not of its type, or not one of its choices.</exception>
    public static object? Typed(string command, PluginCommandArg arg, string value) => arg.Type switch
    {
        "number" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number : throw new DriverException($"`{command}`'s `{arg.Name}` is a number, and `{value}` is not one."),
        "boolean" => value switch
        {
            "true" => true,
            "false" => false,
            _ => throw new DriverException($"`{command}`'s `{arg.Name}` is true or false."),
        },
        "choice" => arg.Choices.Contains(value, StringComparer.Ordinal)
            ? value : throw new DriverException($"`{command}`'s `{arg.Name}` is one of {string.Join(", ", arg.Choices)}."),
        _ => value,
    };

    private static string Takes(IReadOnlyList<PluginCommandArg> declared) =>
        declared.Count == 0 ? ": it takes none" : $": {string.Join(", ", declared.Select(each => $"--{each.Name}"))}";
}
