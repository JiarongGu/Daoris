using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// What a <see cref="QuestOperationKind.Revised"/> operation sets on its quest: the words and the scope of the part as a plan revision now says it.
/// </summary>
/// <param name="Title">The quest's title.</param>
/// <param name="Body">The quest's body.</param>
/// <param name="Weight">The weight the part is staffed by, or null for none.</param>
/// <param name="Paths">The path globs the part claims.</param>
public sealed record QuestRevision(string Title, string Body, string? Weight, IReadOnlyList<string> Paths)
{
    /// <summary>The paths as a published quest keeps them: each once, in order.</summary>
    public IReadOnlyList<string> NormalizedPaths => Paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>Whether <paramref name="quest"/> already says all of this.</summary>
    internal bool Says(Quest quest) =>
        quest.Title == Title && quest.Body == Body && quest.Weight == Weight && quest.Paths.SequenceEqual(NormalizedPaths);

    /// <summary>The revision in its one shape, in the log's payload and on the wire.</summary>
    internal void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("title", Title);
        writer.WriteString("body", Body);
        if (Weight is not null) writer.WriteString("weight", Weight);
        writer.WritePropertyName("paths");
        QuestRecordCodec.WriteLinks(writer, Paths);
        writer.WriteEndObject();
    }

    /// <summary>A revision read from JSON, or null where it is not whole: a title and a body that are text, a weight that is text or absent, paths that are a list of text.</summary>
    internal static QuestRevision? Judged(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(title.GetString())) return null;
        if (!element.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.String) return null;
        string? weight = null;
        if (element.TryGetProperty("weight", out var kept))
        {
            if (kept.ValueKind != JsonValueKind.String) return null;
            if (QuestWeights.Judge(kept.GetString()) is not (var judged, null) || judged is null) return null;
            weight = judged;
        }

        var paths = new List<string>();
        if (element.TryGetProperty("paths", out var listed))
        {
            if (listed.ValueKind != JsonValueKind.Array) return null;
            foreach (var path in listed.EnumerateArray())
            {
                if (path.ValueKind != JsonValueKind.String || !QuestPaths.Fits(path.GetString())) return null;
                paths.Add(path.GetString()!);
            }
        }

        return new QuestRevision(title.GetString()!, body.GetString()!, weight, paths);
    }
}
