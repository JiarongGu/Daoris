namespace Daoris.Driver;

/// <summary>
/// The kit's word on a new plugin's family (<see cref="PluginFamily"/>): an id naming none is refused, and so is one in Daoris's own
/// family made anywhere but this repository's own examples (<c>examples/plugins/</c> of the checkout that holds the canon and the
/// desktop's source), each refusal saying why and naming the person's own family's form of it.
/// </summary>
internal static class PluginKitFamily
{
    /// <summary>Why the kit makes no plugin of <paramref name="id"/> inside <paramref name="parent"/>; null where it may.</summary>
    public static string? Refusal(string id, string parent)
    {
        if (PluginFamily.Of(id) is not { } family)
        {
            return $"`{id}` names no family — a plugin's id is `<family>-<name>`, your own family first (`<your-family>-{id.Trim('-')}`), "
                + "so two people's plugins of one name never meet.";
        }

        if (family != PluginFamily.Daoris || OwnExamples(parent)) return null;
        return $"`{id}` is in Daoris's own family, `{PluginFamily.Daoris}-`, which only Daoris's own plugins use — name it in your own "
            + $"family, as `<your-family>-{PluginFamily.Tail(id)}`.";
    }

    private static bool OwnExamples(string parent)
    {
        var plugins = new DirectoryInfo(Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (plugins is not { Name: "plugins", Parent: { Name: "examples", Parent: { } checkout } }) return false;
        return Directory.Exists(Path.Combine(checkout.FullName, "canon"))
            && Directory.Exists(Path.Combine(checkout.FullName, "src", "Daoris.Desktop"));
    }
}
