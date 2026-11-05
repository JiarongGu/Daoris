namespace Daoris.Driver;

/// <summary>One plugin's skills a start is handed, as plain values: which skills, from which installed folder.</summary>
/// <param name="Plugin">The plugin's id.</param>
/// <param name="Version">Its version, or empty where it states none.</param>
/// <param name="Folder">Its install folder, which holds <c>skills/&lt;name&gt;/</c> for each skill.</param>
/// <param name="Skills">The skills it hands this start, each already judged sound, in its manifest's order.</param>
public sealed record SkillSource(string Plugin, string Version, string Folder, IReadOnlyList<string> Skills);
