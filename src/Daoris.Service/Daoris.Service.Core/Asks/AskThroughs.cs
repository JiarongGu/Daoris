using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The person's through words on an ask: judged, kept with when, the latest standing, and taken back by a keep with none. A
/// person's door, a local host's as every ask route is; whether an agent's press quotes the person is judged where the press is proved.
/// </summary>
internal sealed class AskThroughs(AskStore asks, AskStandings standings)
{
    /// <summary>The most characters through words may hold, a review choice's words' bound.</summary>
    public const int WordsLimit = Reviews.WordsLimit;

    /// <summary>The person sets the ask's through words, or takes them back with none.</summary>
    public async Task<AskOutcome> ChooseAsync(string id, string? words, DateTimeOffset now, CancellationToken ct)
    {
        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null) return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);
        if (ask.State == AskState.Closed)
        {
            return new(AskRefusal.Closed, $"Ask `#{ask.Id}` is closed ({ask.Note}): no run of it waits for your words.", ask);
        }

        var (kept, unfit) = Judge(words, now);
        if (unfit is not null) return new(AskRefusal.BadThrough, unfit, ask);
        if (kept is null)
        {
            if (ask.Through is null) return new(AskRefusal.None, $"Ask `#{ask.Id}` holds no through words: each run of it waits for your Accept.", ask);
            kept = new AskThrough(null, now);
        }

        await asks.RecordThroughAsync(ask.Id, kept, now, ct).ConfigureAwait(false);
        var stands = await standings.FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, kept.Words is null
            ? $"Ask `#{ask.Id}`: your through words are taken back; each run of it waits for your Accept again."
            : $"Ask `#{ask.Id}`: each run of it is accepted on your words where nothing else waits on you, and a departure from a requirement still waits.",
            stands);
    }

    /// <summary>Through words as the ask keeps them, or why not: trimmed and bounded; null for none.</summary>
    public static (AskThrough? Through, string? Refusal) Judge(string? words, DateTimeOffset now)
    {
        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        return said switch
        {
            null => (null, null),
            { Length: > WordsLimit } => (null, $"Through words are at most {WordsLimit} characters; these are {said.Length}. Nothing was kept."),
            _ => (new AskThrough(said, now), null),
        };
    }

    /// <summary>The list as the store keeps it.</summary>
    public static string Written(IReadOnlyList<AskThrough> throughs) => JsonFields.Written(writer =>
    {
        writer.WriteStartArray();
        foreach (var through in throughs) Write(writer, through);
        writer.WriteEndArray();
    });

    /// <summary>One entry as the store keeps it: the words where any, and when.</summary>
    public static void Write(Utf8JsonWriter writer, AskThrough through)
    {
        writer.WriteStartObject();
        if (through.Words is { } words) writer.WriteString("words", words);
        writer.WriteString("at", Moments.Write(through.At));
        writer.WriteEndObject();
    }

    /// <summary>The entries kept, oldest first; one this build cannot read is passed over.</summary>
    public static IReadOnlyList<AskThrough> Read(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.Object && Moments.Read(element, "at") is not null)
            .Select(element => new AskThrough(JsonFields.Text(element, "words"), Moments.Read(element, "at")!.Value))];
    }
}
