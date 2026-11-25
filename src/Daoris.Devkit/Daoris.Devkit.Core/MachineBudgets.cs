using System.Text.Json;

namespace Daoris.Devkit;

/// <summary>
/// What the Daoris home keeps for a repository that the devkit reads: the devkit version this machine runs, the place of the private
/// patterns file and the machine-local inputs (<c>data/check-budgets.json</c>). A member the home does not keep is null, so the older
/// declaration's own member is read in its place.
/// </summary>
/// <param name="Devkit">The version this machine runs.</param>
/// <param name="PatternsFile">The private patterns file's place, repository-relative.</param>
/// <param name="LocalInputs">The machine-local files bound into the repository's trees; an empty list says there are none.</param>
public sealed record MachineBudgets(string? Devkit, string? PatternsFile, IReadOnlyList<string>? LocalInputs)
{
    /// <summary>Nothing kept: a read with no home, no file or no record for the repository.</summary>
    public static MachineBudgets None { get; } = new(null, null, null);

    private const string EntryPath = "data/check-budgets.json";

    /// <summary>
    /// The record the home at <paramref name="home"/> keeps for the repository checked out at <paramref name="repositoryRoot"/>, by the
    /// folder of its main checkout. The devkit has no service: it reads the file directly.
    /// </summary>
    /// <exception cref="DevkitException">The file does not read, named.</exception>
    public static MachineBudgets Read(string? home, string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(home)) return None;
        var file = Path.Combine(home, EntryPath);
        if (!File.Exists(file)) return None;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("repositories", out var all) || all.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("it has no repositories object");
            }

            if (!all.TryGetProperty(RepositoryName(repositoryRoot), out var kept) || kept.ValueKind != JsonValueKind.Object) return None;
            List<string>? bound = null;
            if (kept.TryGetProperty("localInputs", out var inputs) && inputs.ValueKind == JsonValueKind.Array)
            {
                bound = [.. inputs.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!.Replace('\\', '/'))
                    .Where(path => path.Length > 0).Distinct(StringComparer.Ordinal)];
            }

            return new(Text(kept, "devkit"), Text(kept, "patternsFile"), bound);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new DevkitException($"{file} does not read ({error.Message}): the machine's check budgets are not used until it does");
        }
    }

    /// <summary>
    /// The name the home keeps a repository's record under: the folder of its main checkout. A linked tree's git folder names the common one
    /// (<c>commondir</c>), whose parent is that folder; the root's own folder where it is no linked tree.
    /// </summary>
    public static string RepositoryName(string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var git = Path.Combine(root, ".git");
        if (File.Exists(git) && File.ReadAllText(git).Trim() is var pointer && pointer.StartsWith("gitdir:", StringComparison.Ordinal))
        {
            var linked = Path.GetFullPath(Path.Combine(root, pointer["gitdir:".Length..].Trim()));
            var common = Path.Combine(linked, "commondir");
            if (File.Exists(common))
            {
                var main = Path.GetFullPath(Path.Combine(linked, File.ReadAllText(common).Trim()));
                if (Path.GetDirectoryName(main) is { } folder) return Path.GetFileName(folder);
            }
        }

        return Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text.Trim() : null;
}
