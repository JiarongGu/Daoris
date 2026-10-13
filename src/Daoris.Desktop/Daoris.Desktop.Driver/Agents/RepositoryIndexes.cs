using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The indexes a repository keeps for its own documents (KNOWUSE1c, D135 §4), found in the tree a session runs in so the
/// look before asking can name them: the router its manifest declares, then every file named as an index in the folders
/// where documents and doctrine are kept. Read to frame the instruction, never written (D32).
/// </summary>
/// <remarks>
/// <para><b>Generic on purpose.</b> The instruction travels to repositories that know nothing of this one, so nothing here
/// names one. An index is found by its name (<see cref="IsIndex"/>), one level deep in <see cref="Folders"/>: never a walk
/// of the tree, which a start would pay for in a repository of any size, and never the code's own folders.</para>
///
/// <para><b>The router is a pointer, not a twin.</b> <c>daoris.json</c>'s <c>documents.router</c>, a path or an object's
/// <c>path</c>, is named where it is a plain repository-relative path to a file this tree holds. The CLI's
/// <c>checkDocuments</c> and the service's <c>RepositoryDocuments</c> read the same field as a gate and an indexer; this
/// refuses nothing, gates nothing and writes nothing, and names only a file the repository wrote and declared, so a rule
/// of theirs it does not apply can at worst name such a file once more.</para>
/// </remarks>
public static class RepositoryIndexes
{
    /// <summary>
    /// Where an index is looked for, in the order they are named: the root and the documents folder, where a repository's
    /// documents are kept, then the two roots agent doctrine is written under, each with its rules and knowledge tiers.
    /// </summary>
    public static readonly IReadOnlyList<string> Folders =
        ["", "docs", ".claude", ".claude/rules", ".claude/knowledge", ".agents", ".agents/knowledge"];

    private const string Manifest = "daoris.json";

    /// <summary>
    /// The repository-relative paths, with forward slashes, of the indexes the tree at <paramref name="root"/> keeps: the
    /// declared router first, then each file named as an index, folder by folder in <see cref="Folders"/>' order and by
    /// name within one. Each is named once, in the spelling first found; empty where the tree keeps none.
    /// </summary>
    public static IReadOnlyList<string> Find(string root)
    {
        var found = new List<string>();
        void Add(string path)
        {
            if (!found.Contains(path, StringComparer.OrdinalIgnoreCase)) found.Add(path);
        }

        if (Router(root) is { } router) Add(router);
        foreach (var folder in Folders)
        {
            foreach (var name in Named(Path.Combine(root, folder)))
            {
                Add(folder.Length == 0 ? name : $"{folder}/{name}");
            }
        }

        return found;
    }

    /// <summary>
    /// Whether a file's name makes it an index: a markdown file with <c>index</c> as a word of its name, in any case, the
    /// words split at an underscore, a dash or a dot (<c>INDEX.md</c>, <c>RULES_INDEX_CROSS.md</c>, <c>knowledge-index.md</c>);
    /// never inside another word (<c>reindex.md</c>, <c>indexing.md</c>).
    /// </summary>
    public static bool IsIndex(string name)
    {
        if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return false;
        return name[..^3].Split('_', '-', '.', ' ').Any(word => word.Equals("index", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The names of the index files directly in <paramref name="folder"/>, in ordinal order; none where it is not there or cannot be listed.</summary>
    private static IEnumerable<string> Named(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        try
        {
            return Directory.EnumerateFiles(folder)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(IsIndex)
                .Order(StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// The router the manifest declares, where it is a plain path to a file this tree holds: forward slashes, no leading
    /// <c>./</c>, not rooted, no drive, and no <c>..</c> anywhere in it. Null for anything else, a manifest that does not
    /// read included: a pointer that might lead out of the tree is not one to hand a session.
    /// </summary>
    private static string? Router(string root)
    {
        var manifest = Path.Combine(root, Manifest);
        if (!File.Exists(manifest)) return null;

        string? declared;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest).TrimStart('﻿'));
            declared = Declared(document.RootElement);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (declared is null) return null;
        var path = declared.Replace('\\', '/');
        while (path.StartsWith("./", StringComparison.Ordinal)) path = path[2..];
        if (path.Length == 0 || path.StartsWith('/') || path.Contains(':') || path.Split('/').Contains("..")) return null;
        return File.Exists(Path.Combine(root, path)) ? path : null;
    }

    /// <summary><c>documents.router</c> as text, or an object's <c>path</c> as text; null for any other shape.</summary>
    private static string? Declared(JsonElement manifest)
    {
        if (manifest.ValueKind != JsonValueKind.Object
            || !manifest.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Object
            || !documents.TryGetProperty("router", out var router))
        {
            return null;
        }

        var value = router.ValueKind == JsonValueKind.Object && router.TryGetProperty("path", out var path) ? path : router;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
