namespace Daoris.Driver;

/// <summary>
/// Claude Code's skill layout: for each plugin handing skills, a harness plugin of its own under the home's
/// <c>spawn/&lt;session&gt;.plugins/&lt;id&gt;/</c>, holding a copy of each skill's folder and the harness plugin's manifest. Its native door
/// names each by <c>--plugin-dir</c>, its protocol door in its <c>session/new</c> meta; the folder goes when the session does.
/// </summary>
/// <remarks>
/// <para><b>The tier, measured</b>: Claude Code 2.1.299, in an untrusted folder with a fresh config
/// directory, loads a <c>--plugin-dir</c> plugin's skills as <c>&lt;plugin&gt;:&lt;skill&gt;</c>, and a dotted plugin name with them.
/// It also loads the folder's own <c>.claude/skills</c> and an <c>--add-dir</c>'s, but the first is a repository's checkout for a
/// session (<c>repository-owns-its-work</c>) and the second grants its files too; the plugin door is the one for a spawn.</para>
///
/// <para><b>A copy, never the plugin's folder.</b> The harness would load all of a folder it is pointed at, skills it is not
/// declared for and anything else its own plugin shape carries; a copy holds the declared skills for this start alone.</para>
/// </remarks>
public sealed class ClaudeSkills : ISkillLayout
{
    /// <summary>The one layout Claude Code's doors read.</summary>
    public static ClaudeSkills Layout { get; } = new();

    /// <summary>The harness plugin's manifest, inside each composed plugin.</summary>
    private static readonly string[] HarnessManifest = [".claude-plugin", "plugin.json"];

    /// <inheritdoc/>
    public (string? Folder, IReadOnlyList<string> Plugins) Write(string home, string sessionId, IReadOnlyList<SkillSource> sources)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Spawn, $"{sessionId}.plugins");
        var plugins = new List<string>();
        foreach (var source in sources.Where(source => source.Skills.Count > 0))
        {
            var plugin = Path.Combine(folder, source.Plugin);
            if (Directory.Exists(plugin)) Directory.Delete(plugin, recursive: true);
            foreach (var skill in source.Skills)
            {
                TreeCopy.Copy(Path.Combine(source.Folder, "skills", skill), Path.Combine(plugin, "skills", skill));
            }

            Directory.CreateDirectory(Path.Combine(plugin, HarnessManifest[0]));
            JsonFile.Write(Path.Combine([plugin, .. HarnessManifest]), writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("name", source.Plugin);
                if (source.Version.Length > 0) writer.WriteString("version", source.Version);
                writer.WriteString("description", $"The skills plugin `{source.Plugin}` hands this session, as Daoris composed them for it.");
                writer.WriteEndObject();
            });
            plugins.Add(plugin);
        }

        return plugins.Count > 0 ? (folder, plugins) : (null, []);
    }

    /// <inheritdoc/>
    public (string? Folder, IReadOnlyList<string> Plugins) Own(string home, string sessionId, OwnTools tools) =>
        ClaudeOwnHome.Write(home, sessionId, tools);
}
