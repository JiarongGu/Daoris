using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A family file as the driver reads it, by the shape the doctrine tool's check reads (<c>daoris.family.json</c>): the members by layer, lowest
/// first, the pairs the family denies, and each edge's lag bound. The driver judges nothing the check judges; it fans a landing out only along
/// edges the check would pass, so a refused graph is the check's to say and never the fan-out's to act on.
/// </summary>
public sealed record FamilyDeclaration(
    IReadOnlyList<IReadOnlyList<string>> Layers, IReadOnlyList<PinEdge> Denied, IReadOnlyList<(PinEdge Edge, TimeSpan Lag)> Lags)
{
    /// <summary>The family file's name in the folder the members' checkouts share.</summary>
    public const string FileName = "daoris.family.json";

    /// <summary>Every member, lowest layer first, by the name the file writes.</summary>
    public IReadOnlyList<string> Members => [.. Layers.SelectMany(layer => layer)];

    private int? LayerOf(string member)
    {
        for (var each = 0; each < Layers.Count; each++)
        {
            if (Layers[each].Any(name => Same(name, member))) return each;
        }

        return null;
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool SameEdge(PinEdge left, PinEdge right) => Same(left.Consumer, right.Consumer) && Same(left.Producer, right.Producer);

    /// <summary>The lag bound the family declares for the edge, or null for none: nothing is then told about it.</summary>
    public TimeSpan? LagOf(PinEdge edge) => Lags.Where(each => SameEdge(each.Edge, edge)).Select(each => (TimeSpan?)each.Lag).FirstOrDefault();

    /// <summary>
    /// The edges whose producer is <paramref name="producer"/>: each member of a higher layer whose own <c>uses</c> names it and that the family
    /// does not deny, in the order of the layers. A <c>uses</c> of an equal or lower layer, or of a non-member, is no edge here.
    /// </summary>
    /// <param name="usesOf">A member's <c>uses</c>, by the registry's rule.</param>
    public IReadOnlyList<PinEdge> ConsumersOf(string producer, Func<string, IReadOnlyList<string>> usesOf)
    {
        if (LayerOf(producer) is not { } lowest) return [];
        var edges = new List<PinEdge>();
        foreach (var consumer in Members)
        {
            if (LayerOf(consumer) is not { } layer || layer <= lowest) continue;
            if (!usesOf(consumer).Any(name => Same(name, producer))) continue;
            var edge = new PinEdge(consumer, Members.First(name => Same(name, producer)));
            if (Denied.Any(pair => SameEdge(pair, edge))) continue;
            edges.Add(edge);
        }

        return edges;
    }

    /// <summary>The family file read whole, or null where it is not the shape the check reads: a wrong file is never read as a family.</summary>
    public static FamilyDeclaration? Read(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("layers", out var layered) || layered.ValueKind != JsonValueKind.Array) return null;
            var layers = new List<IReadOnlyList<string>>();
            foreach (var layer in layered.EnumerateArray())
            {
                if (Names(layer) is not { Count: > 0 } names) return null;
                layers.Add(names);
            }

            var members = layers.SelectMany(layer => layer).ToList();
            if (layers.Count == 0 || members.Select(name => name.ToLowerInvariant()).Distinct().Count() != members.Count) return null;
            if (Pairs(root, "denied", withLag: false) is not { } denied || Pairs(root, "edges", withLag: true) is not { } edges) return null;
            return new FamilyDeclaration(layers, [.. denied.Select(each => each.Edge)], [.. edges.Select(each => (each.Edge, each.Lag!.Value))]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string>? Names(JsonElement layer)
    {
        if (layer.ValueKind != JsonValueKind.Array) return null;
        var names = new List<string>();
        foreach (var name in layer.EnumerateArray())
        {
            if (name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())) return null;
            names.Add(name.GetString()!.Trim());
        }

        return names;
    }

    /// <summary>A list of <c>{ consumer, producer }</c> pairs, each with a lag where <paramref name="withLag"/>; absent is none, and a wrong one is null.</summary>
    private static List<(PinEdge Edge, TimeSpan? Lag)>? Pairs(JsonElement root, string property, bool withLag)
    {
        var pairs = new List<(PinEdge, TimeSpan?)>();
        if (!root.TryGetProperty(property, out var listed)) return pairs;
        if (listed.ValueKind != JsonValueKind.Array) return null;
        foreach (var pair in listed.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Object || Text(pair, "consumer") is not { } consumer || Text(pair, "producer") is not { } producer) return null;
            TimeSpan? lag = null;
            if (withLag)
            {
                if (FamilyLag.Parse(Text(pair, "lag")) is not { } bound) return null;
                lag = bound;
            }

            pairs.Add((new PinEdge(consumer, producer), lag));
        }

        return pairs;
    }

    private static string? Text(JsonElement pair, string name) =>
        pair.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!.Trim() : null;
}

/// <summary>An edge's lag bound as the family file writes it: a count of days or hours, <c>3d</c> or <c>12h</c>.</summary>
public static partial class FamilyLag
{
    /// <summary>The span the text says, or null for anything else: no count, a leading zero, a count past six digits, another unit.</summary>
    public static TimeSpan? Parse(string? text)
    {
        if (text is null || !Shape().IsMatch(text)) return null;
        var count = int.Parse(text[..^1], System.Globalization.CultureInfo.InvariantCulture);
        return text[^1] == 'd' ? TimeSpan.FromDays(count) : TimeSpan.FromHours(count);
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[1-9][0-9]{0,5}[dh]$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Shape();
}

/// <summary>A consumer's <c>daoris.family.lock</c> as the driver reads and writes it: the commit it pins of each producer.</summary>
public static class FamilyLock
{
    /// <summary>The consumer's lock file's name at its root.</summary>
    public const string FileName = "daoris.family.lock";

    /// <summary>The commit pinned of each producer, by the producer's name in lower case; null where the file is not a lock.</summary>
    public static IReadOnlyDictionary<string, string>? Read(string text)
    {
        if (Entries(text) is not { } entries) return null;
        var pins = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (producer, commit) in entries) pins.TryAdd(producer.ToLowerInvariant(), commit);
        return pins;
    }

    /// <summary>Each pin in the order the file writes it, the producer's name as written; null where the file is not a lock.</summary>
    public static IReadOnlyList<(string Producer, string Commit)>? Entries(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("pins", out var listed) || listed.ValueKind != JsonValueKind.Array) return null;
            var pins = new List<(string, string)>();
            foreach (var pin in listed.EnumerateArray())
            {
                if (pin.ValueKind != JsonValueKind.Object || !pin.TryGetProperty("producer", out var producer) || !pin.TryGetProperty("commit", out var commit)
                    || producer.ValueKind != JsonValueKind.String || commit.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(producer.GetString()) || string.IsNullOrWhiteSpace(commit.GetString())) return null;
                pins.Add((producer.GetString()!.Trim(), commit.GetString()!.Trim()));
            }

            return pins;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The lock's text with <paramref name="producer"/> pinned at <paramref name="commit"/>, in the shape the doctrine tool writes it.</summary>
    public static string With(IReadOnlyList<(string Producer, string Commit)> pins, string producer, string commit)
    {
        var next = pins.ToList();
        var at = next.FindIndex(pin => string.Equals(pin.Producer, producer, StringComparison.OrdinalIgnoreCase));
        if (at < 0) next.Add((producer, commit));
        else next[at] = (next[at].Producer, commit);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("pins");
            foreach (var (name, pinned) in next)
            {
                writer.WriteStartObject();
                writer.WriteString("producer", name);
                writer.WriteString("commit", pinned);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }
}
