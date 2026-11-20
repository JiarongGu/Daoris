using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A word waiting on a run's record, written only while one waits and read whole with an act this build applies. The person's names no
/// author; a supervisor's names its role, its decision and its attempt, and the person's that took one's place names the decision replaced.
/// </summary>
internal static class NodeWordFiles
{
    public static void Write(Utf8JsonWriter writer, NodeWord word)
    {
        writer.WriteStartObject();
        writer.WriteString("node", word.Node);
        writer.WriteString("act", word.Act);
        if (word.Words is { } words) writer.WriteString("words", words);
        writer.WriteString("at", Moments.Write(word.At));
        if (word.By is { } by)
        {
            writer.WriteStartObject("by");
            writer.WriteString("role", by.Role);
            writer.WriteString("decision", by.Decision);
            writer.WriteEndObject();
        }

        if (word.Attempt is { } attempt) writer.WriteNumber("attempt", attempt);
        if (word.Supersedes is { } supersedes) writer.WriteString("supersedes", supersedes);
        if (word.Kept) writer.WriteBoolean("kept", true);
        if (word.Tip is { } tip) writer.WriteString("tip", tip);
        if (word.Revision is { } revision) writer.WriteString("revision", revision);
        if (word.Review is { } review) writer.WriteString("review", review);
        if (word.Part is { } part) writer.WriteString("part", part);
        if (word.Outcome is { } outcome) writer.WriteString("outcome", outcome);
        if (word.Holder is { } holder) writer.WriteString("holder", holder);
        if (word.Decider is { } decider) writer.WriteString("decider", decider);
        if (word.GaveUp) writer.WriteBoolean("gaveUp", true);
        if (word.Epoch is { } held)
        {
            writer.WriteStartObject("epoch");
            writer.WriteNumber("attempt", held.Attempt);
            writer.WriteNumber("number", held.Number);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>The word <paramref name="each"/> is, or null where it is not whole or its act is not one this build applies.</summary>
    public static NodeWord? Read(JsonElement each)
    {
        if (each.ValueKind != JsonValueKind.Object || Text(each, "node") is not { } node || Text(each, "act") is not { } act
            || !NodeWordActs.All.Contains(act, StringComparer.Ordinal) || Moments.Read(each, "at") is not { } at
            || (each.TryGetProperty("words", out var said) && said.ValueKind != JsonValueKind.String)
            || (each.TryGetProperty("supersedes", out var replaced) && replaced.ValueKind != JsonValueKind.String))
        {
            return null;
        }

        NodeWordBy? by = null;
        if (each.TryGetProperty("by", out var author))
        {
            if (author.ValueKind != JsonValueKind.Object || Text(author, "role") is not { } role || Text(author, "decision") is not { } decision) return null;
            by = new NodeWordBy(role, decision);
        }

        int? attempt = null;
        if (each.TryGetProperty("attempt", out var number))
        {
            if (number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var value)) return null;
            attempt = value;
        }

        // A word whose epoch does not read is not whole: applied without it, it would skip the pass's check of its holder.
        NodeWordEpoch? held = null;
        if (each.TryGetProperty("epoch", out var tenure))
        {
            if (tenure.ValueKind != JsonValueKind.Object || Whole(tenure, "attempt") is not { } heldAttempt || Whole(tenure, "number") is not { } epoch) return null;
            held = new NodeWordEpoch(heldAttempt, epoch);
        }

        var kept = each.TryGetProperty("kept", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new NodeWord(node, act, Text(each, "words"), at)
        {
            By = by, Attempt = attempt, Supersedes = Text(each, "supersedes"), Kept = kept, Tip = Text(each, "tip"),
            Revision = Text(each, "revision"), Review = Text(each, "review"), Part = Text(each, "part"),
            Outcome = Text(each, "outcome"), Holder = Text(each, "holder"), Decider = Text(each, "decider"), Epoch = held,
            GaveUp = each.TryGetProperty("gaveUp", out var gave) && gave.ValueKind == JsonValueKind.True,
        };
    }

    private static int? Whole(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
