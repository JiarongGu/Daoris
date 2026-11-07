namespace Daoris.Driver;

/// <summary>
/// What an unsealed install wrote outside its rule's paths that git ignores at <c>HEAD</c>, as its record keeps it: a
/// <b>companion</b> where no effective rule names the path, the dependency's own and read with it at every stage; an <b>other
/// write</b> where another rule names it, which keeps that rule's kind, so the placement stands only while each one does.
/// </summary>
internal static class InstallWrites
{
    /// <summary>The paths sorted into companions, each read as it stands now, and other writes.</summary>
    internal static (IReadOnlyList<TreeCompanion> Companions, IReadOnlyList<string> Others) Of(string tree, IReadOnlyList<string> beside, FileRules rules)
    {
        var companions = new List<TreeCompanion>();
        var others = new List<string>();
        foreach (var path in beside)
        {
            if (rules.Rules.Any(each => each.Rule.Matches(path))) others.Add(path);
            else companions.Add(Read(tree, path));
        }

        return (companions, others);
    }

    private static TreeCompanion Read(string tree, string path)
    {
        var full = Path.Combine(tree, path.TrimEnd('/'));
        if (Directory.Exists(full)) return new(path, DependencyManifest.Read(tree, path.TrimEnd('/'), [], out _), null);
        return new(path, null, File.Exists(full) ? DependencyManifest.HashOf(full) : null);
    }

    /// <summary>Why a companion no longer holds as its install left it, or null where every one does.</summary>
    internal static string? CompanionsDiffer(string tree, TreePlacement placement)
    {
        foreach (var companion in placement.Companions)
        {
            var full = Path.Combine(tree, companion.Path.TrimEnd('/'));
            var holds = companion.Manifest is { } folder
                ? folder.Differs(tree, companion.Path.TrimEnd('/'), []) is null
                : companion.Hash is { } hash && File.Exists(full) && DependencyManifest.HashOf(full) == hash;
            if (!holds) return $"`{companion.Path}`, which its install wrote beside it, changed";
        }

        return null;
    }

    /// <summary>The first other write that is gone, its own rule having cleared or moved it, or null where each stands.</summary>
    internal static string? OtherGone(string tree, TreePlacement placement) =>
        placement.Others.FirstOrDefault(other => !Path.Exists(Path.Combine(tree, other.TrimEnd('/'))));
}
