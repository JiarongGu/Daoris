using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Whether a <c>daoris.gates.json</c> runs nothing the one accepted does not: the rule that keeps an acceptance across a change, and lets a
/// check run under the accepted declaration where a work's own change alters no command. Two declarations run the same where no unit
/// differs (<see cref="DeclarationUnits"/>): every gate, <c>generated</c>, <c>reached</c>, <c>doctrine</c> and every other part is a unit
/// with its inert members (words, a gate's reach and its machine's budget) set aside, and the top-level
/// <c>tests</c>, <c>tiers</c>, <c>docs</c> and <c>verifyBudget</c> are none (patterns read by the repository's own tools inside an
/// accepted command). Everything else is what runs or what grants: a gate's name, command, folder, hand-back and flags, a gate added,
/// removed or moved, a member of any name deeper inside a part, and every other part, one the rule does not know included, waits for the person.
/// </summary>
/// <remarks>
/// Every read of a declaration is strict (<see cref="Strict"/>): a member written twice, a comment or a trailing comma makes the text no
/// declaration, so it keeps only an identical text and waits for the person, and never throws.
/// </remarks>
public static class DeclarationCommands
{
    /// <summary>The one way a declaration's text is read, by every reader of it: no member written twice, no comment, no trailing comma.</summary>
    internal static readonly JsonDocumentOptions Strict = new() { AllowDuplicateProperties = false };

    private const string ChecksKey = "gates";

    /// <summary>
    /// Whether <paramref name="now"/> runs only what <paramref name="accepted"/> runs: no unit differs between them. A text that does not
    /// read as a JSON object keeps only an identical text; nothing accepted keeps only nothing.
    /// </summary>
    public static bool Keeps(string? accepted, string? now)
    {
        if (string.IsNullOrWhiteSpace(accepted) || string.IsNullOrWhiteSpace(now)) return string.IsNullOrWhiteSpace(accepted) && string.IsNullOrWhiteSpace(now);
        return DeclarationUnits.Changed(accepted, now) is { } changed ? changed.Count == 0 : string.Equals(accepted, now, StringComparison.Ordinal);
    }

    /// <summary>
    /// The inert parts that differ between <paramref name="accepted"/> and <paramref name="now"/>, the top's first and then each check's by
    /// name, in the order written, each kind once: what a keep set aside. None where either does not read.
    /// </summary>
    public static IReadOnlyList<DeclarationSetAside> SetAside(string? accepted, string? now)
    {
        if (Read(accepted) is not { } was || Read(now) is not { } then) return [];
        var found = new List<DeclarationSetAside>();
        void Add(DeclarationSetAside part)
        {
            if (!found.Contains(part)) found.Add(part);
        }

        foreach (var name in Names(was, then).Where(name => DeclarationUnits.IsInertPart(name) && !JsonNode.DeepEquals(was[name], then[name])))
        {
            Add(new DeclarationSetAside(null, DeclarationSetAside.Of(name)));
        }

        var before = Checks(was).Where(check => NameOf(check) is not null).GroupBy(check => NameOf(check)!).ToDictionary(group => group.Key, group => group.First());
        foreach (var check in Checks(then))
        {
            if (NameOf(check) is not { } name || !before.TryGetValue(name, out var old)) continue;
            foreach (var part in Names(old, check).Where(part => DeclarationUnits.IsInertGatePart(part) && !JsonNode.DeepEquals(old[part], check[part])))
            {
                Add(new DeclarationSetAside(name, DeclarationSetAside.Of(part)));
            }
        }

        return found;
    }

    /// <summary>The declaration <paramref name="text"/> read strictly as an object; null where it is empty, does not read, or is no object.</summary>
    internal static JsonObject? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return JsonNode.Parse(text, null, Strict) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The checks of <paramref name="declaration"/> that are objects, in the order written.</summary>
    internal static IEnumerable<JsonObject> Checks(JsonObject? declaration) =>
        declaration?[ChecksKey] is JsonArray checks ? checks.OfType<JsonObject>() : [];

    /// <summary>The name a check declares; null where it names none as a string.</summary>
    internal static string? NameOf(JsonObject check) => check["name"] is JsonValue name && name.TryGetValue<string>(out var text) ? text : null;

    private static IEnumerable<string> Names(JsonObject was, JsonObject now) =>
        was.Select(member => member.Key).Concat(now.Select(member => member.Key)).Distinct(StringComparer.Ordinal);
}

/// <summary>An inert part a keep set aside: the check it belongs to (null for the top of the file) and its kind, one of the constants.</summary>
public sealed record DeclarationSetAside(string? Check, string Part)
{
    public const string Words = "words";
    public const string Paths = "paths";
    public const string Time = "time";
    public const string Memory = "memory";
    public const string Tests = "tests";
    public const string Tiers = "tiers";
    public const string Docs = "docs";
    public const string Budget = "budget";

    /// <summary>The kind of the inert member <paramref name="name"/>.</summary>
    internal static string Of(string name) => name switch
    {
        "paths" => Paths,
        "timeoutMinutes" or "timeoutWhy" => Time,
        "memoryGb" => Memory,
        "tests" => Tests,
        "tiers" => Tiers,
        "docs" => Docs,
        "verifyBudget" => Budget,
        _ => Words,
    };

    /// <summary>The part in plain words: <c>the paths of `unit`</c>, <c>the test sets</c>.</summary>
    public string Said()
    {
        var what = Part switch
        {
            Paths => "the paths",
            Time => "the time limit",
            Memory => "the memory",
            Tests => "the test sets",
            Tiers => "the tiers",
            Docs => "the document patterns",
            Budget => "the verification budget",
            _ => "the words",
        };
        return Check is null ? what : $"{what} of `{Check}`";
    }

    /// <summary>The parts in one phrase: <c>the words, the paths of `unit` and the memory of `unit`</c>; empty where there are none.</summary>
    public static string Said(IReadOnlyList<DeclarationSetAside> parts) => parts.Count switch
    {
        0 => "",
        1 => parts[0].Said(),
        _ => $"{string.Join(", ", parts.Take(parts.Count - 1).Select(part => part.Said()))} and {parts[^1].Said()}",
    };
}
