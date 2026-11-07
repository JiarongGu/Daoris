using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One reading of a repository: the commit read, when, and the rules found.</summary>
internal sealed record FoundReading(string Commit, DateTimeOffset At, IReadOnlyList<FileRule> Rules);

/// <summary>A repository's last reading in the found layer, or none, and why the file or a rule of it does not read.</summary>
internal sealed record FoundFilesRead(FoundReading? Reading, IReadOnlyList<string> Problems);

/// <summary>
/// The found layer, the home's <c>data/files-found.json</c>: the analysis's file alone, each repository's entry its last reading,
/// replaced whole by the next and never edited by hand, an edit going in the person's layer. A file that does not read is written
/// again at the next reading, since everything in it is the analysis's to read again.
/// </summary>
internal static class FoundFiles
{
    internal const string FileName = "files-found.json";

    internal static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.FilesFound);

    internal static FoundFilesRead Read(string home, string repository)
    {
        var path = PathOf(home);
        if (!File.Exists(path)) return new(null, []);
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path).TrimStart('﻿'));
            if (!document.RootElement.TryGetProperty("repositories", out var listed) || listed.ValueKind != JsonValueKind.Object) return new(null, []);
            var entry = listed.EnumerateObject().FirstOrDefault(each => string.Equals(each.Name, repository, StringComparison.OrdinalIgnoreCase));
            if (entry.Value.ValueKind != JsonValueKind.Object) return new(null, []);

            var problems = new List<string>();
            var read = entry.Value.TryGetProperty("read", out var at) && at.ValueKind == JsonValueKind.Object ? at : default;
            var commit = read.ValueKind == JsonValueKind.Object ? Json.Text(read, "commit") ?? "" : "";
            var moment = read.ValueKind == JsonValueKind.Object && DateTimeOffset.TryParse(Json.Text(read, "at"), out var parsed) ? parsed : DateTimeOffset.MinValue;
            var rules = FileRuleFile.Rules(entry.Value, "", problems);
            return new(new FoundReading(commit, moment, rules), problems);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new(null, [$"{FileName} does not read ({error.Message}); the next reading writes it again"]);
        }
    }

    /// <summary>The repository's entry replaced whole by <paramref name="reading"/>, the other repositories' kept; written beside and renamed over.</summary>
    internal static void Write(string home, string repository, FoundReading reading)
    {
        var path = PathOf(home);
        JsonObject root = [];
        try
        {
            if (File.Exists(path) && JsonNode.Parse(AtomicFile.ReadText(path).TrimStart('﻿')) is JsonObject standing) root = standing;
        }
        catch (JsonException)
        {
            root = [];
        }

        if (root["repositories"] is not JsonObject repositories) root["repositories"] = repositories = [];
        foreach (var stale in repositories.Select(pair => pair.Key).Where(name => string.Equals(name, repository, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            repositories.Remove(stale);
        }

        repositories[repository] = new JsonObject
        {
            ["read"] = new JsonObject { ["commit"] = reading.Commit, ["at"] = reading.At.ToString("O") },
            ["rules"] = new JsonArray([.. reading.Rules.Select(rule => (JsonNode)FileRuleFile.Written(rule))]),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n");
    }
}
