using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The index of where things are that a repository declares in its manifest (<c>daoris.json</c>'s <c>documents.index</c>), which
/// every checkout writes again from its own files: output an unforced removal takes with a tree, never the tree's own work.
/// </summary>
/// <remarks>
/// Read from the line's tip, never the tree's copy, so a session cannot declare its own scratch disposable. Where the line commits
/// nothing in the index's folder, the folder is the index (a folder of generated files, as <c>docs/index/</c>); where it commits
/// anything there, or the index lies at the root, the index file alone is, so a declaration never reaches a folder of authored
/// files. The pattern rules of declared output (<see cref="TreeScratch.Globs"/>) refuse one that leaves the repository.
/// </remarks>
internal static class GeneratedIndex
{
    /// <summary>
    /// The index a manifest's text declares, as a repository-relative path with forward slashes: <c>documents.index</c>, a path or
    /// an object's <c>path</c>. Null where the text is not a JSON object or declares none.
    /// </summary>
    internal static string? Declared(string? manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest)) return null;
        try
        {
            using var document = JsonDocument.Parse(manifest);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("documents", out var documents) || documents.ValueKind != JsonValueKind.Object
                || !documents.TryGetProperty("index", out var index))
            {
                return null;
            }

            var path = index.ValueKind switch
            {
                JsonValueKind.String => index.GetString(),
                JsonValueKind.Object when index.TryGetProperty("path", out var named) && named.ValueKind == JsonValueKind.String => named.GetString(),
                _ => null,
            };
            var said = path?.Trim().Replace('\\', '/');
            return string.IsNullOrEmpty(said) ? null : said;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The output glob the declared <paramref name="index"/> makes: its folder and all under it where the line commits nothing there
    /// (<paramref name="folderCommitted"/> false), else the index file alone; the file alone too for an index at the root.
    /// </summary>
    internal static string Output(string index, bool folderCommitted)
    {
        var slash = index.LastIndexOf('/');
        return slash <= 0 || folderCommitted ? index : $"{index[..slash]}/**";
    }

    /// <summary>
    /// The output glob of the index the manifest at <paramref name="line"/>'s tip declares, read in the checkout at <paramref name="root"/>;
    /// null where the line holds no manifest, it declares no index, or git cannot say whether the index's folder is committed.
    /// </summary>
    internal static async Task<string?> AtAsync(string root, string line, CancellationToken ct)
    {
        if (!BranchName.IsValid(line)) return null;
        var tip = $"refs/heads/{line}";
        var manifest = await MachinePorts.Repository.TextAtAsync(root, tip, DeclarationNames.Manifest, ct).ConfigureAwait(false);
        if (Declared(manifest) is not { } index) return null;
        var slash = index.LastIndexOf('/');
        if (slash <= 0) return Output(index, folderCommitted: false);
        if (await MachinePorts.Repository.EntriesAsync(root, tip, [index[..(slash + 1)]], false, ct).ConfigureAwait(false) is not { } folder) return null;
        return Output(index, folderCommitted: folder.Count > 0);
    }
}
