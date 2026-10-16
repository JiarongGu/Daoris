using System.Text;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// `daoris-driver wiring [--json] [--measure]` (WIRING1b, the this-Daoris design §6, D50): the terminal's twin of Settings →
/// This Daoris, reading the same facts through <see cref="SystemWiring"/>. What only the window knows is said as
/// <i>the window knows</i>; <c>--json</c> prints the route's shape, so the twins share one test table; <c>--measure</c> walks the
/// folder's sizes, within the walk's budget, before it answers.
/// </summary>
/// <remarks>
/// The printing lives in the library so a test runs the whole door in-process. Exit codes keep the family contract: 0 said ·
/// 2 a word it does not answer.
/// </remarks>
public static class WiringCommand
{
    public const string Usage = "usage: daoris-driver wiring [--json] [--measure]";

    /// <param name="world">What the reader is handed: <see cref="WiringWorld.OfThisTerminal"/>, or a test's.</param>
    /// <param name="said">Everything the door said, for the caller to print.</param>
    public static int Run(string[] args, WiringWorld world, out string said)
    {
        bool json = false, measure = false;
        foreach (var word in args)
        {
            switch (word)
            {
                case "--json": json = true; break;
                case "--measure": measure = true; break;
                default:
                    said = $"wiring: no option `{word}`.\n{Usage}\n";
                    return 2;
            }
        }

        if (measure && world.Home is { } home)
        {
            world.Sizes.MeasureNow(home, SystemWiring.RootOf(home), WiringBudget.Default, CancellationToken.None, ToolHomeLeftovers.List(world).Folders);
        }

        var wiring = SystemWiring.Read(world);
        said = json ? SystemWiring.Json(wiring) : Text(wiring);
        return 0;
    }

    /// <summary>The answer as a person reads it in a terminal: each section, each fact and its value, a fact not read with why.</summary>
    public static string Text(JsonObject wiring)
    {
        var text = new StringBuilder();
        foreach (var (section, value) in wiring)
        {
            if (section.EndsWith("Why", StringComparison.Ordinal)) continue;
            switch (value)
            {
                case JsonObject facts:
                    text.Append(section).Append('\n');
                    Facts(text, facts, "  ");
                    break;
                case JsonArray rows:
                    text.Append(section).Append(rows.Count == 0 ? ": none\n" : "\n");
                    foreach (var row in rows) text.Append("  ").Append(Line(row)).Append('\n');
                    break;
                default:
                    text.Append(section).Append(": ").Append(Said(value, (string?)wiring[section + "Why"])).Append('\n');
                    break;
            }
        }

        return text.ToString();
    }

    private static void Facts(StringBuilder text, JsonObject facts, string indent)
    {
        foreach (var (name, value) in facts)
        {
            if (name.EndsWith("Why", StringComparison.Ordinal)) continue;
            text.Append(indent).Append(name).Append(": ");
            text.Append(value switch
            {
                JsonObject inner => Line(inner),
                JsonArray rows => rows.Count == 0 ? "none" : string.Join("; ", rows.Select(Line)),
                _ => Said(value, facts.ContainsKey(name + "Why") ? (string?)facts[name + "Why"] : null),
            });
            text.Append('\n');
        }
    }

    private static string Line(JsonNode? row) => row switch
    {
        JsonObject facts => string.Join(", ", facts
            .Where(pair => !pair.Key.EndsWith("Why", StringComparison.Ordinal))
            .Select(pair => $"{pair.Key} {(pair.Value is JsonObject or JsonArray ? Line(pair.Value) : Said(pair.Value, facts.ContainsKey(pair.Key + "Why") ? (string?)facts[pair.Key + "Why"] : null))}")),
        JsonArray rows => $"[{string.Join("; ", rows.Select(Line))}]",
        _ => Said(row, null),
    };

    private static string Said(JsonNode? value, string? why) =>
        value is not null ? value.ToString()
        : why == "window" ? "the window knows"
        : why is null ? "none"
        : $"none ({why})";
}
