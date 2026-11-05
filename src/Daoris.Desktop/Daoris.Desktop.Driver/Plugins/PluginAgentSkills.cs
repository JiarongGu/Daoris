namespace Daoris.Driver;

/// <summary>The skills the contributing plugins hand one destination, as the plain values a provider's skill layout copies.</summary>
internal static class PluginAgentSkills
{
    /// <summary>
    /// Each contributing plugin with a sound skill for <paramref name="to"/>, in catalogue order: a skill whose file is missing, or that
    /// allows itself tools (D170 point 3, <see cref="SkillFiles.Judge"/>), is handed to neither destination.
    /// </summary>
    public static IReadOnlyList<SkillSource> Sources(PluginCatalog catalog, string to) =>
    [
        .. catalog.Contributing
            .Select(entry => new SkillSource(
                entry.Manifest.Id,
                entry.Manifest.Version,
                entry.Folder,
                [.. entry.Manifest.Skills
                    .Where(skill => skill.HandedTo(to) && SkillFiles.Judge(entry.Folder, skill.Name) is null)
                    .Select(skill => skill.Name)]))
            .Where(source => source.Skills.Count > 0),
    ];
}
