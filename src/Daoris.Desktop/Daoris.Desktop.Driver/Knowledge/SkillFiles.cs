namespace Daoris.Driver;

/// <summary>
/// The judge of a plugin's declared skill (D170 point 3; FOUNDATION1 mod-viii, the design §14.3): whether its file is there and it allows
/// itself no tool. The spawn composes only the skills this passes (<see cref="SpawnSkills"/>, which answers through it), and the room says
/// the same skills it would be handed, so the judge is one.
/// </summary>
public static class SkillFiles
{
    /// <summary>The file a skill's folder holds, which the harness reads.</summary>
    public const string SkillFile = "SKILL.md";

    /// <summary>Why a declared skill is not handed, or null where it is: its file is there, and it allows itself no tool.</summary>
    /// <param name="pluginFolder">The plugin's install folder.</param>
    public static string? Judge(string pluginFolder, string skill)
    {
        var file = Path.Combine(pluginFolder, "skills", skill, SkillFile);
        if (!File.Exists(file)) return $"its folder holds no `skills/{skill}/{SkillFile}`, so it is not handed.";
        return AllowsItselfTools(File.ReadAllText(file)) // read-shares: a plugin's own SKILL.md, laid with its folder
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
}
