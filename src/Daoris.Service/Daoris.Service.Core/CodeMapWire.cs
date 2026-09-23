using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A repository's code map as a remote holds it, read off the wire (MAP3e): the file it was read from
/// and its canonical text — both null for a commit that keeps no map — and where it came from.
/// </summary>
public sealed record FedCodeMap(string? File, string? Body, FeedProvenance Fed);

/// <summary>
/// The one shape of `GET /api/code-map/{repository}` (MAP3a, MAP3e): what the door answers, what a
/// machine's host reads when it brings a teammate's map down, and what the page and the driver's feed
/// read — two hands, one shape, as the quest doors have <see cref="QuestWire"/>.
/// </summary>
/// <remarks>
/// A field the answer does not have is LEFT OUT rather than written null, which is the rule the host
/// already kept for every answer and which the page reads by (a map with no file had drawn nothing
/// at all when a test sent null where the host sent nothing).
/// </remarks>
public static class CodeMapWire
{
    /// <summary>The door's answer for one repository.</summary>
    public static string Answer(string repository, CodeMapRead read) => Json(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("repository", repository);
        if (read.File is not null) writer.WriteString("file", read.File);
        if (read.Problem is not null) writer.WriteString("problem", read.Problem);

        writer.WriteStartArray("modules");
        foreach (var module in read.Map?.Modules ?? [])
        {
            writer.WriteStartObject();
            writer.WriteString("id", module.Id);
            writer.WriteString("path", module.Path);
            writer.WriteString("summary", module.Summary);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("dependencies");
        foreach (var dependency in read.Map?.Dependencies ?? [])
        {
            writer.WriteStartObject();
            writer.WriteString("from", dependency.From);
            writer.WriteString("to", dependency.To);
            writer.WriteString("kind", dependency.Kind);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        if (read.Fed is { } fed)
        {
            writer.WriteStartObject("fed");
            writer.WriteString("commit", fed.Commit);
            writer.WriteString("shortCommit", fed.ShortCommit);
            writer.WriteString("committedAt", fed.CommittedAt.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("branch", fed.Branch);
            if (fed.Origin is not null) writer.WriteString("origin", fed.Origin);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    });

    /// <summary>
    /// A remote's answer as a fed map this machine may hold, judged whole again by
    /// <see cref="CodeMapReader"/> — or null when it is not one: not an answer, no provenance, a
    /// refusal, or a map that breaks a rule. A map this machine holds is one it has checked.
    /// </summary>
    public static FedCodeMap? Read(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "problem") is not null) return null;
            if (!root.TryGetProperty("fed", out var fed) || fed.ValueKind != JsonValueKind.Object
                || Text(fed, "commit") is not { Length: > 0 } commit
                || Text(fed, "branch") is not { Length: > 0 } branch
                || Text(fed, "committedAt") is not { } at
                || !DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var committedAt))
            {
                return null;
            }

            var provenance = new FeedProvenance(commit, committedAt, branch, Text(fed, "origin"));
            if (Text(root, "file") is not { } file) return new FedCodeMap(null, null, provenance);

            var modules = new List<CodeModule>();
            foreach (var module in Items(root, "modules"))
            {
                if (Text(module, "id") is not { } id || Text(module, "path") is not { } path
                    || Text(module, "summary") is not { } summary)
                {
                    return null;
                }

                modules.Add(new CodeModule(id, path, summary));
            }

            var dependencies = new List<CodeDependency>();
            foreach (var dependency in Items(root, "dependencies"))
            {
                if (Text(dependency, "from") is not { } from || Text(dependency, "to") is not { } to
                    || Text(dependency, "kind") is not { } kind)
                {
                    return null;
                }

                dependencies.Add(new CodeDependency(from, to, kind));
            }

            var (map, problem) = CodeMapReader.Parse(CodeMapReader.Write(new CodeMap(modules, dependencies)), file);
            return problem is null ? new FedCodeMap(file, CodeMapReader.Write(map!), provenance) : null;
        }
    }

    // An element that is not an object has no fields — asked as one, TryGetProperty would throw.
    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<JsonElement> Items(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToList()
            : [];

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
