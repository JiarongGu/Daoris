using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The skills plugins hand one session: the plugins' sources for the session's destination, laid out by the
/// door's provider (<see cref="ISkillLayout"/>) under the home and handed by the door's own way. Removed when the session ends.
/// </summary>
/// <remarks>
/// <b>Never a skill that would allow itself tools</b>: a skill's <c>allowed-tools</c> pre-approves its tools for the
/// turn that invokes it, untrusted too, so a plugin's skill declaring it is handed to
/// neither destination, and <see cref="Judge"/> says why.
/// </remarks>
public static class SpawnSkills
{
    /// <summary>The file a skill's folder holds, which the harness reads.</summary>
    public const string SkillFile = SkillFiles.SkillFile;

    /// <summary>Why a declared skill is not handed, or null where it is: its file is there, and it allows itself no tool (<see cref="SkillFiles.Judge"/>).</summary>
    /// <param name="pluginFolder">The plugin's install folder.</param>
    public static string? Judge(string pluginFolder, string skill) => SkillFiles.Judge(pluginFolder, skill);

    /// <summary>
    /// Lay out the skills <paramref name="catalog"/> hands <paramref name="to"/> in the door's provider's layout and hand them by the
    /// door's spawn; a protocol door carries them in its <c>session/new</c> meta instead
    /// (<see cref="AgentDoor.AcpSessionMeta(string, IReadOnlyList{string})"/>), so the plugins are answered for the caller to put
    /// there. A door whose provider lays out none is handed none. What the person's own tool home holds (<paramref name="own"/>) is laid
    /// out after, in the same folder, on a door its provider owns; a plugin's door, its program unmeasured, is handed none of it.
    /// </summary>
    public static (string? Folder, IReadOnlyList<string> Plugins) Hand(
        ProviderRegistry adapters, AgentDoor adapter, ProcessStartInfo info, string home, string sessionId, IAgentPlugins catalog, string to,
        OwnTools? own = null)
    {
        if (adapters.SkillLayout(adapter) is not { } layout) return (null, []);
        var (folder, plugins) = layout.Write(home, sessionId, catalog.Skills(to));
        var (ownFolder, ownPlugins) = own is null || adapter.Declared ? (null, []) : layout.Own(home, sessionId, own);
        IReadOnlyList<string> all = [.. plugins, .. ownPlugins];
        if (all.Count > 0) adapter.Spawned().HandPlugins(info, all);
        return (folder ?? ownFolder, all);
    }
}
