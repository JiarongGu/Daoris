using System.Xml;
using System.Xml.Linq;

namespace Daoris.Driver;

/// <summary>A base top's producer at a commit: the one project file beside it and the configurations it declares; null with why where it has none.</summary>
internal sealed record BaseProducer(string? Project, IReadOnlyList<string> Configurations, string? Why)
{
    internal string Builds { get; init; } = "";
}

/// <summary>
/// What the take's plan reads of a <c>base</c> top's producer from git's objects at the new start (the tree-pool design §37.3, 2, §38.2,
/// 1): the project file beside the top, and its declared <c>Configurations</c>, read from the project file and else from the nearest
/// <c>Directory.Build.props</c> above it, as MSBuild imports that one alone; Debug and Release where neither names any. A
/// configuration named only under a condition or through a property reads as undeclared, so its folder is set aside, costing a build.
/// </summary>
internal static class BaseProducers
{
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    internal static async Task<BaseProducer> ReadAsync(PoolGit git, string slot, string commit, string top, CancellationToken ct)
    {
        var folder = FolderOf(top);
        if (await git.Repository.EntriesAsync(slot, commit, folder.Length == 0 ? [] : [folder], false, ct).ConfigureAwait(false) is not { } listed) return new(null, [], $"git could not list {Shown(folder)} at the start");
        var projects = listed.Select(entry => entry.Path)
            .Where(path => ProjectExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))).ToList();
        if (projects.Count != 1)
        {
            return new(null, [], projects.Count == 0
                ? $"{top} has no project file beside it, so no build of the door's produces it"
                : $"{top} is beside {projects.Count} project files, so it has no one producer");
        }

        var declared = Declared(await FileGit.ShowAsync(slot, commit, projects[0], ct).ConfigureAwait(false));
        for (var above = folder; declared is null; above = FolderOf(above))
        {
            if (await FileGit.ShowAsync(slot, commit, above + "Directory.Build.props", ct).ConfigureAwait(false) is { } props)
            {
                declared = Declared(props);
                break;
            }

            if (above.Length == 0) break;
        }

        return new(projects[0], declared ?? ["Debug", "Release"], null);
    }

    /// <summary>The configurations an unconditional <c>Configurations</c> property names, the last one read winning; null where none does.</summary>
    internal static IReadOnlyList<string>? Declared(string? project)
    {
        if (project is null) return null;
        try
        {
            var document = XDocument.Parse(project);
            var named = document.Root?.Elements().Where(group => group.Name.LocalName == "PropertyGroup" && group.Attribute("Condition") is null)
                .SelectMany(group => group.Elements().Where(property => property.Name.LocalName == "Configurations" && property.Attribute("Condition") is null))
                .LastOrDefault();
            return named?.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(name => !name.Contains("$(", StringComparison.Ordinal)).ToList();
        }
        catch (XmlException)
        {
            return null;
        }
    }


    /// <summary>The folder holding <paramref name="path"/>, repository-relative and ending <c>/</c>; empty for the top.</summary>
    private static string FolderOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        return slash < 0 ? "" : trimmed[..(slash + 1)];
    }

    private static string Shown(string folder) => folder.Length == 0 ? "the repository's top" : folder;
}
