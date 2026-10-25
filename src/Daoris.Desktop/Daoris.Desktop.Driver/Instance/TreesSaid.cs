using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a <c>trees</c> press says in a terminal (RPC1b3, the terminal RPC design §7.3): the lines for the output, the lines for the
/// error, and the exit code. The alone path prints it from the records its core returns; the route that is the press's window twin
/// answers it beside the page's fields (<c>said</c>), so the verb that becomes a client prints the same words from the answer.
/// </summary>
/// <param name="Out">The lines the verb writes to its output, in order.</param>
/// <param name="Error">The lines the verb writes to its error.</param>
/// <param name="Code">The verb's exit code: 0 clean, 1 refused.</param>
public sealed record TreesSaid(IReadOnlyList<string> Out, IReadOnlyList<string> Error, int Code)
{
    /// <summary>Writes the words, the output first, as the alone path does.</summary>
    public void Print(TextWriter output, TextWriter error)
    {
        foreach (var line in Out) output.WriteLine(line);
        foreach (var line in Error) error.WriteLine(line);
    }

    /// <summary>
    /// The words with the lines the registry followed said after them, and its failure on the error (RPC1b5): what a landing's verb
    /// prints once the lines it moved are registered. The code is unchanged: a registry that did not follow is not a refused landing.
    /// </summary>
    public TreesSaid Followed(FollowedSaid? followed) =>
        followed is null || (followed.Out.Count == 0 && followed.Error.Count == 0)
            ? this
            : new([.. Out, .. followed.Out], [.. Error, .. followed.Error], Code);

    /// <summary>The words with a plugin's own lines said before them, as the alone path says them as they come (RPCOPEN1).</summary>
    public TreesSaid After(IReadOnlyList<string> pluginLines) => pluginLines.Count == 0 ? this : new([.. pluginLines, .. Out], Error, Code);

    /// <summary>The words as an answer carries them (<c>said</c>): added beside the page's fields, never in their place.</summary>
    public object ToWire() => new { Out, Error, Code };

    /// <summary>The words an answer carries, or null where it carries none (an instance from before this field).</summary>
    public static TreesSaid? Read(JsonElement answer)
    {
        if (Property(answer, "said") is not { ValueKind: JsonValueKind.Object } said) return null;
        if (Property(said, "code") is not { ValueKind: JsonValueKind.Number } code || !code.TryGetInt32(out var exit)) return null;
        return new(Lines(Property(said, "out")), Lines(Property(said, "error")), exit);
    }

    /// <summary>
    /// A press's answer in the verb's own words, as <see cref="InstanceDoor.PressAsync"/> takes it: the words printed, the exit code
    /// returned. An answer with none is said so, exit 2, and nothing is done again.
    /// </summary>
    public static Func<JsonElement, int> Words(TextWriter output, TextWriter error, string verb = "trees") => answer =>
    {
        if (Read(answer) is not { } said)
        {
            error.WriteLine($"{verb}: Daoris answered in a form this terminal does not read: it carries no words for this press.");
            return 2;
        }

        said.Print(output, error);
        return said.Code;
    };

    /// <summary>
    /// A read's answer in the verb's own words (RPC1b2): printed and its exit code returned where the answer carries them, else null,
    /// so <see cref="InstanceDoor.ReadAsync"/> reads alone, as it does for an instance from before the field.
    /// </summary>
    public static Func<JsonElement, int?> ReadWords(TextWriter output, TextWriter error) => answer =>
    {
        if (Read(answer) is not { } said) return null;
        said.Print(output, error);
        return said.Code;
    };

    private static JsonElement? Property(JsonElement from, string name)
    {
        if (from.ValueKind != JsonValueKind.Object) return null;
        foreach (var each in from.EnumerateObject())
        {
            if (string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase)) return each.Value;
        }

        return null;
    }

    private static IReadOnlyList<string> Lines(JsonElement? array) => array is { ValueKind: JsonValueKind.Array } lines
        ? [.. lines.EnumerateArray().Where(line => line.ValueKind == JsonValueKind.String).Select(line => line.GetString()!)]
        : [];
}

/// <summary>What the registry's following said of the lines a press moved (WSSETUP5, D124 §3.1): the lines, and the failure if it had one.</summary>
/// <param name="Out">The lines said on the output, each already indented as the terminal says them.</param>
/// <param name="Error">The sentence said on the error where the registry did not follow, or none.</param>
public sealed record FollowedSaid(IReadOnlyList<string> Out, IReadOnlyList<string> Error);
