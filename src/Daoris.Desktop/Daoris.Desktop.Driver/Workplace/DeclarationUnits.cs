using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A declaration's units: the parts an acceptance is given, and a change is held, one at a time. A unit is
/// each gate (by its name), <c>generated</c>, <c>reached</c>, <c>doctrine</c> and each other part of the file by its key, so a part nobody
/// named fails safe; the gates' <c>order</c> is one more, kept where the file is one. A unit's digest is the SHA-256 of its canonical form
/// (members sorted, no whitespace) with only the inert members set aside: a member whose name begins <c>_</c> directly inside a unit
/// (words); a gate's <c>paths</c> (its reach); and, until the check budgets move to the home (a later change deletes them from the file), a
/// gate's <c>timeoutMinutes</c>, <c>timeoutWhy</c> and <c>memoryGb</c>. The top-level <c>tests</c>, <c>tiers</c>, <c>docs</c> and
/// <c>verifyBudget</c> are no unit: their files and patterns are read by the repository's own tools inside an accepted command.
/// </summary>
/// <remarks>
/// Every read is strict (<see cref="DeclarationCommands.Strict"/>): a text that does not read has no units (null), so it keeps only an
/// identical text and waits for the person. What a unit being accepted decides is the policy's; this says
/// only which units differ, and which runs a differing unit holds (<see cref="Run"/>).
/// </remarks>
public static class DeclarationUnits
{
    /// <summary>The gates' order, as the names of their units joined by a line break; a reorder of the gates both texts hold changes it, a gate added or removed does not.</summary>
    public const string Order = "order";

    public const string Generated = "generated";
    public const string Reached = "reached";
    public const string Doctrine = "doctrine";

    private const string GatePrefix = "gate:";
    private const string PartPrefix = "part:";
    private const string GatesKey = "gates";

    private static readonly HashSet<string> InertParts = new(StringComparer.Ordinal) { "tests", "tiers", "docs", "verifyBudget" };

    private static readonly HashSet<string> InertGateParts = new(StringComparer.Ordinal) { "paths", "timeoutMinutes", "timeoutWhy", "memoryGb" };

    /// <summary>The unit of the gate named <paramref name="name"/>.</summary>
    public static string Gate(string name) => GatePrefix + name;

    /// <summary>Whether <paramref name="member"/> of the top of a declaration is no unit: words (<c>_</c>) or one of the inert parts.</summary>
    internal static bool IsInertPart(string member) => InertParts.Contains(member) || member.StartsWith('_');

    /// <summary>Whether <paramref name="member"/> directly inside a gate is set aside: words, its reach, or its machine's budget.</summary>
    internal static bool IsInertGatePart(string member) => InertGateParts.Contains(member) || member.StartsWith('_');

    /// <summary>
    /// The units of the declaration <paramref name="text"/>, each by its key with its digest (<see cref="Order"/> holds the gates' names, not a
    /// digest), in the order written; null where it is empty, does not read strictly or is no object.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? Of(string? text) => DeclarationCommands.Read(text) is { } declaration ? Of(declaration) : null;

    private static Dictionary<string, string> Of(JsonObject declaration)
    {
        var units = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in declaration)
        {
            if (IsInertPart(key)) continue;
            if (key == GatesKey)
            {
                Gates(value, units);
                continue;
            }

            switch (key)
            {
                case Generated or Reached or Doctrine:
                    units[key] = Digest(value is JsonArray list
                        ? new JsonArray([.. list.Select(each => Words(each))])
                        : Words(value));
                    break;
                default:
                    units[PartPrefix + key] = Digest(value);
                    break;
            }
        }

        return units;
    }

    /// <summary>The gates: each object by its name, one with no name or a name written for another by its place, a member that is no object with the part <c>gates</c>.</summary>
    private static void Gates(JsonNode? value, Dictionary<string, string> units)
    {
        if (value is not JsonArray gates)
        {
            units[PartPrefix + GatesKey] = Digest(value);
            return;
        }

        var order = new List<string>();
        var rest = new JsonArray();
        for (var index = 0; index < gates.Count; index++)
        {
            if (gates[index] is not JsonObject gate)
            {
                rest.Add(gates[index]?.DeepClone());
                continue;
            }

            var key = DeclarationCommands.NameOf(gate) is { } name ? Gate(name) : GatePrefix + "#" + index;
            if (units.ContainsKey(key)) key = $"{key}#{index}";
            units[key] = Digest(Filtered(gate, IsInertGatePart));
            order.Add(key);
        }

        if (rest.Count > 0) units[PartPrefix + GatesKey] = Digest(rest);
        if (order.Count > 0) units[Order] = string.Join('\n', order);
    }

    /// <summary>The unit's words set aside: a member whose name begins <c>_</c>, directly inside it.</summary>
    private static JsonNode? Words(JsonNode? node) => node is JsonObject obj ? Filtered(obj, member => member.StartsWith('_')) : node?.DeepClone();

    private static JsonObject Filtered(JsonObject obj, Func<string, bool> drop) =>
        new(obj.Where(member => !drop(member.Key)).Select(member => new KeyValuePair<string, JsonNode?>(member.Key, member.Value?.DeepClone())));

    /// <summary>The units that differ between two readings, in the order of <paramref name="now"/> and then those only <paramref name="accepted"/> holds; <see cref="Order"/> last.</summary>
    public static IReadOnlyList<string> Differing(IReadOnlyDictionary<string, string> accepted, IReadOnlyDictionary<string, string> now)
    {
        var changed = now.Where(unit => unit.Key != Order && (!accepted.TryGetValue(unit.Key, out var before) || before != unit.Value)).Select(unit => unit.Key).ToList();
        changed.AddRange(accepted.Keys.Where(key => key != Order && !now.ContainsKey(key)));

        // A gate added or removed is its own unit; the order that remains is what the gates both hold are in.
        var wasNames = Names(accepted);
        var nowNames = Names(now);
        if (!wasNames.Where(nowNames.Contains).SequenceEqual(nowNames.Where(wasNames.Contains), StringComparer.Ordinal)) changed.Add(Order);
        return changed;
    }

    private static List<string> Names(IReadOnlyDictionary<string, string> units) =>
        units.TryGetValue(Order, out var order) ? [.. order.Split('\n')] : [];

    /// <summary>
    /// The units that differ between the declarations <paramref name="accepted"/> and <paramref name="now"/>, as <see cref="Differing"/> says;
    /// null where either does not read, which differs from everything but its own text.
    /// </summary>
    public static IReadOnlyList<string>? Changed(string? accepted, string? now) =>
        Of(accepted) is { } before && Of(now) is { } after ? Differing(before, after) : null;

    /// <summary>
    /// The units a run depends on, as a test of a unit's key: the gates it runs (<paramref name="gates"/>, or every gate where null, with their
    /// order), the <c>reached</c> unit where it is the hand-back run, and what every run shares: <c>generated</c> and each part nobody
    /// named. A unit no run reads (<c>doctrine</c>) holds none.
    /// </summary>
    public static Func<string, bool> Run(IReadOnlyCollection<string>? gates, bool reached) => unit =>
        unit.StartsWith(GatePrefix, StringComparison.Ordinal) ? gates is null || gates.Contains(unit[GatePrefix.Length..], StringComparer.Ordinal)
        : unit == Order ? gates is null
        : unit == Reached ? reached
        : unit == Doctrine ? false
        : true;

    /// <summary>
    /// Whether the change from <paramref name="accepted"/> to <paramref name="now"/> alters a unit <paramref name="runs"/> names: what a run
    /// holds for. A text that does not read holds every run unless it is the other's own text; both blank hold none, one blank holds all.
    /// </summary>
    public static bool Holds(string? accepted, string? now, Func<string, bool> runs)
    {
        if (string.IsNullOrWhiteSpace(accepted) || string.IsNullOrWhiteSpace(now)) return !(string.IsNullOrWhiteSpace(accepted) && string.IsNullOrWhiteSpace(now));
        return Changed(accepted, now) is { } changed ? changed.Any(runs) : !string.Equals(accepted, now, StringComparison.Ordinal);
    }

    /// <summary>The key of the top-level part a unit belongs to: the gates' own units and their order are <c>gates</c>, a part its key.</summary>
    internal static string PartOf(string unit) =>
        unit.StartsWith(PartPrefix, StringComparison.Ordinal) ? unit[PartPrefix.Length..]
        : unit.StartsWith(GatePrefix, StringComparison.Ordinal) || unit == Order ? GatesKey
        : unit;

    /// <summary>The unit in plain words: <c>`build`</c> for a gate, <c>the part `integration`</c>, <c>the gates' order</c>.</summary>
    public static string Said(string unit) =>
        unit.StartsWith(GatePrefix, StringComparison.Ordinal) ? $"`{unit[GatePrefix.Length..]}`"
        : unit == Order ? "the gates' order"
        : unit.StartsWith(PartPrefix, StringComparison.Ordinal) ? $"the part `{unit[PartPrefix.Length..]}`"
        : $"the part `{unit}`";

    // The narrowing: what a change drops from a reach.

    /// <summary>
    /// What <paramref name="now"/> drops from the reach <paramref name="accepted"/> declares: a gate's <c>paths</c>, and each test set's
    /// <c>files</c> and <c>whole</c>, a gate or a set that is gone dropping all its patterns. A pattern added or kept narrows nothing; none
    /// where either text does not read. A narrowing owes nothing here; it is said (<see cref="DeclaredNarrowing.Said"/>).
    /// </summary>
    public static IReadOnlyList<DeclaredNarrowing> Narrowed(string? accepted, string? now)
    {
        if (DeclarationCommands.Read(accepted) is not { } before || DeclarationCommands.Read(now) is not { } after) return [];
        var found = new List<DeclaredNarrowing>();
        var gates = DeclarationCommands.Checks(after).Where(gate => DeclarationCommands.NameOf(gate) is not null)
            .GroupBy(gate => DeclarationCommands.NameOf(gate)!).ToDictionary(group => group.Key, group => group.First());
        foreach (var gate in DeclarationCommands.Checks(before))
        {
            if (DeclarationCommands.NameOf(gate) is not { } name) continue;
            Dropped(Patterns(gate["paths"]), gates.TryGetValue(name, out var kept) ? Patterns(kept["paths"]) : [], $"the paths of `{name}`", found);
        }

        var sets = Sets(after).ToDictionary(set => set.Name, set => set.Set);
        foreach (var (name, set) in Sets(before))
        {
            sets.TryGetValue(name, out var kept);
            Dropped(Patterns(set["files"]), Patterns(kept?["files"]), $"the test files of the set `{name}`", found);
            Dropped(Patterns(set["whole"]), Patterns(kept?["whole"]), $"the whole-run files of the set `{name}`", found);
        }

        return found;
    }

    private static IEnumerable<(string Name, JsonObject Set)> Sets(JsonObject declaration) =>
        declaration["tests"] is JsonObject { } tests && tests["sets"] is JsonArray sets
            ? sets.OfType<JsonObject>().Where(set => DeclarationCommands.NameOf(set) is not null).Select(set => (DeclarationCommands.NameOf(set)!, set))
            : [];

    private static List<string> Patterns(JsonNode? node) => node switch
    {
        JsonValue value when value.TryGetValue<string>(out var one) => [one],
        JsonArray list => [.. list.OfType<JsonValue>().Select(each => each.TryGetValue<string>(out var text) ? text : null).OfType<string>()],
        _ => [],
    };

    private static void Dropped(List<string> accepted, List<string> now, string where, List<DeclaredNarrowing> found)
    {
        var lost = accepted.Where(pattern => !now.Contains(pattern, StringComparer.Ordinal)).ToList();
        if (lost.Count > 0) found.Add(new DeclaredNarrowing(where, lost));
    }

    // The canonical form.

    private static string Digest(JsonNode? node)
    {
        var text = new StringBuilder();
        Canonical(node, text);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static void Canonical(JsonNode? node, StringBuilder text)
    {
        switch (node)
        {
            case null:
                text.Append("null");
                break;
            case JsonObject obj:
                text.Append('{');
                var first = true;
                foreach (var (key, value) in obj.OrderBy(member => member.Key, StringComparer.Ordinal))
                {
                    if (!first) text.Append(',');
                    first = false;
                    text.Append(JsonSerializer.Serialize(key)).Append(':');
                    Canonical(value, text);
                }

                text.Append('}');
                break;
            case JsonArray list:
                text.Append('[');
                for (var index = 0; index < list.Count; index++)
                {
                    if (index > 0) text.Append(',');
                    Canonical(list[index], text);
                }

                text.Append(']');
                break;
            default:
                text.Append(node.ToJsonString());
                break;
        }
    }
}

/// <summary>What a change drops from a reach: where (<c>the paths of `build`</c>) and the patterns it lost.</summary>
public sealed record DeclaredNarrowing(string Where, IReadOnlyList<string> Dropped)
{
    public bool Equals(DeclaredNarrowing? other) => other is not null && Where == other.Where && Dropped.SequenceEqual(other.Dropped);

    public override int GetHashCode() => HashCode.Combine(Where, Dropped.Count);

    /// <summary>The narrowings in one line: <c>narrowed: the paths of `build` lost `src/`; the test files of the set `a` lost `x`, `y`</c>; empty where there are none.</summary>
    public static string Said(IReadOnlyList<DeclaredNarrowing> narrowed) =>
        narrowed.Count == 0 ? "" : "narrowed: " + string.Join("; ", narrowed.Select(each => $"{each.Where} lost {string.Join(", ", each.Dropped.Select(pattern => $"`{pattern}`"))}"));
}
