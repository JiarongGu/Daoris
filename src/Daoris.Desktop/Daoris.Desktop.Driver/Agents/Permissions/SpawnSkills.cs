using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The skills plugins hand one session (HELPPLUG1b, D170 point 4): for each contributing plugin with skills for the session's
/// destination, a harness plugin of its own under the home's <c>spawn/&lt;session&gt;.plugins/&lt;id&gt;/</c>, holding a copy of
/// each skill's folder, and the harness pointed at it by its session-only door. Removed when the session ends.
/// </summary>
/// <remarks>
/// <para><b>The tier, measured</b> (D170's HELPPLUG1b note): Claude Code 2.1.299, in an untrusted folder with a fresh config
/// directory, loads a <c>--plugin-dir</c> plugin's skills as <c>&lt;plugin&gt;:&lt;skill&gt;</c>, and a dotted plugin name with them.
/// It also loads the folder's own <c>.claude/skills</c> and an <c>--add-dir</c>'s, but the first is a repository's checkout for a
/// session (<c>repository-owns-its-work</c>) and the second grants its files too; the plugin door is the one for a spawn.</para>
///
/// <para><b>A copy, never the plugin's folder.</b> The harness would load all of a folder it is pointed at, skills it was not
/// declared for and anything else its own plugin shape carries; a copy holds the declared skills for this destination alone.</para>
///
/// <para><b>Never a skill that would allow itself tools</b> (D170 point 3): a skill's <c>allowed-tools</c> pre-approves its tools
/// for the turn that invokes it, untrusted too (the development documents design §2 point 3), so a plugin's skill declaring it
/// is handed to neither destination, and <see cref="Judge"/> says why.</para>
/// </remarks>
public static class SpawnSkills
{
    /// <summary>The file a skill's folder holds, which the harness reads.</summary>
    public const string SkillFile = "SKILL.md";

    /// <summary>The harness plugin's manifest, inside each composed plugin.</summary>
    private static readonly string[] HarnessManifest = [".claude-plugin", "plugin.json"];

    /// <summary>Why a declared skill is not handed, or null where it is: its file is there, and it allows itself no tool.</summary>
    /// <param name="pluginFolder">The plugin's install folder.</param>
    public static string? Judge(string pluginFolder, string skill)
    {
        var file = Path.Combine(pluginFolder, "skills", skill, SkillFile);
        if (!File.Exists(file)) return $"its folder holds no `skills/{skill}/{SkillFile}`, so it is not handed.";
        return AllowsItselfTools(File.ReadAllText(file))
            ? $"its `{SkillFile}` declares `allowed-tools`, which would let it run tools nobody allowed, so Daoris hands it nowhere (D170)."
            : null;
    }

    /// <summary>Whether a skill's front matter, the lines between its opening <c>---</c> and the next, declares <c>allowed-tools</c>.</summary>
    private static bool AllowsItselfTools(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---") return false;
        foreach (var line in lines.Skip(1))
        {
            if (line.Trim() == "---") return false;
            if (line.TrimStart().StartsWith("allowed-tools", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// Compose one session's plugins for <paramref name="to"/>: the folder under the home, and each plugin in it, in catalogue
    /// order; a null folder and none where no contributing plugin hands a sound skill there.
    /// </summary>
    public static (string? Folder, IReadOnlyList<string> Plugins) Write(string home, string sessionId, PluginCatalog catalog, string to)
    {
        var folder = Path.Combine(home, SpawnServers.Folder, $"{sessionId}.plugins");
        var plugins = new List<string>();
        foreach (var entry in catalog.Contributing)
        {
            var skills = entry.Manifest.Skills.Where(skill => skill.HandedTo(to) && Judge(entry.Folder, skill.Name) is null).ToList();
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

    /// <summary>
    /// Compose them and hand a pipe-door harness its plugins by its own door; the protocol door carries them in its
    /// <c>session/new</c> meta instead (<see cref="ISessionAdapter.AcpSessionMeta(string, IReadOnlyList{string})"/>), so the
    /// plugins are answered for the caller to put there.
    /// </summary>
    public static (string? Folder, IReadOnlyList<string> Plugins) Hand(
        ISessionAdapter adapter, ProcessStartInfo info, string home, string sessionId, PluginCatalog catalog, string to)
    {
        var (folder, plugins) = Write(home, sessionId, catalog, to);
        if (plugins.Count > 0 && adapter.Wire == SessionWire.Pipe) adapter.HandPlugins(info, plugins);
        return (folder, plugins);
    }

    /// <summary>The folder is the session's; it goes when the session does.</summary>
    public static void Remove(string? folder)
    {
        if (folder is null) return;
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file something still holds open is left for the next start of the same session to replace.
        }
    }
}
