namespace Daoris.Driver;

/// <summary>Plugins composes its declared skills for one spawn; Agents hands and removes the resulting session folder.</summary>
internal static class PluginAgentSkills
{
    /// <summary>The harness plugin's manifest, inside each composed plugin.</summary>
    private static readonly string[] HarnessManifest = [".claude-plugin", "plugin.json"];

    public static (string? Folder, IReadOnlyList<string> Plugins) Write(string home, string sessionId, PluginCatalog catalog, string to)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Spawn, $"{sessionId}.plugins");
        var plugins = new List<string>();
        foreach (var entry in catalog.Contributing)
        {
            var skills = entry.Manifest.Skills.Where(skill => skill.HandedTo(to) && SkillFiles.Judge(entry.Folder, skill.Name) is null).ToList();
            if (skills.Count == 0) continue;

            var plugin = Path.Combine(folder, entry.Manifest.Id);
            if (Directory.Exists(plugin)) Directory.Delete(plugin, recursive: true);
            foreach (var skill in skills)
            {
                PluginFolders.CopyTree(Path.Combine(entry.Folder, "skills", skill.Name), Path.Combine(plugin, "skills", skill.Name));
            }

            Directory.CreateDirectory(Path.Combine(plugin, HarnessManifest[0]));
            JsonFile.Write(Path.Combine([plugin, .. HarnessManifest]), writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("name", entry.Manifest.Id);
                if (entry.Manifest.Version.Length > 0) writer.WriteString("version", entry.Manifest.Version);
                writer.WriteString("description", $"The skills plugin `{entry.Manifest.Id}` hands this session, as Daoris composed them for it.");
                writer.WriteEndObject();
            });
            plugins.Add(plugin);
        }

        return plugins.Count > 0 ? (folder, plugins) : (null, []);
    }
}
