namespace Daoris.Driver;

/// <summary>
/// Where Ask Daoris's conversation runs, and under which access: its place (<see cref="In"/>), the place under the access its start got
/// (<see cref="Under"/>) and under the mode the person chose (<see cref="UnderMode"/>).
/// </summary>
internal static class HelpPlacement
{
    /// <summary>
    /// Where Ask Daoris's conversation runs (HELP1a): its room, the room's allows and a read of its own files folder, and the
    /// checkouts reading across lets it read (D107); the agent's own asking mode; its tools up front.
    /// </summary>
    /// <remarks>
    /// <para><b>Its conversation is kept</b> (ASKHIST1): the person goes back to an earlier one from its history, and words written to
    /// one that ended go on in its own harness conversation by the id kept for it (D137 §2.2), as a repository's chat does.</para>
    ///
    /// <para><b>A plugin's server reaches it only where the plugin declares it for Ask Daoris</b> (HELPPLUG1a, D170 point 3), and
    /// only once the person approved its exact tools (<see cref="HelpGrants"/>, D170's note): its reads and the writes they
    /// accepted are allowed, any other write asked, which its mode refuses. HELP1a handed it none, since a browser brought up for
    /// a tool it may not use is a window for nothing; a server approved for it names the tools it may use.</para>
    ///
    /// <para>ASKSCOPE1c (D158's ASKSCOPE1 note point 5): its tools are bound to its room's workspace (<see cref="HelpCircle.Of"/>): its
    /// connector's circle, and the person's denies for that workspace in its rules beside the checkouts its room grants.</para>
    /// </remarks>
    /// <param name="workspace">The workspace its record keeps (ASKSCOPE1a): what setting it aside is judged by; null is <c>default</c>.</param>
    internal static ChatPlace In(string home, string room, IReadOnlyList<HelpRead> reads, string? workspace = null)
    {
        var circle = HelpCircle.Of(home, room, workspace);
        // HELPMODE1c2a (D183's note): the housekeeping the person chose, and since HELPMODE1c3 the settings, read once for this start,
        // so its allow-list and its receipt say the same; each tool's call is judged again by the record then (HelpHousekeeping,
        // HelpSettings).
        var access = ConfiguredAccess.Read(home);
        var housekeeping = access.Housekeeping;
        // HELPMODE1c4: the readable checkouts the person left on, read through `checkout_read`, which each call judges again.
        IReadOnlyList<HelpRead> handed = [.. reads.Where(read => access.Reads(read.Name))];
        return new(
            HelpRoom.Repository, room,
            (file, id) =>
            {
                var grants = HelpGrants.Read(home, PluginCatalog.Load(home, AdapterSet.Built().Names));
                return HelpRoom.Rules(file, ChatFiles.Folder(home, id), handed, grants.Allow, grants.Ask, circle, housekeeping, access.Settings);
            },
            Plugins: true, ConnectorOnPipe: true, Posture: HelpRoom.Posture)
        {
            Keeps = true,
            ServersTo = PluginServerTo.Help,
            Workspace = RemoteTarget.Workspace(workspace),
            Circle = circle,
            Reads = handed,
            Housekeeping = housekeeping,
            Settings = access.Settings,
            CheckoutsOff = access.CheckoutsOff,
            // CONTEXTONE1c: in no repository, its standing block is the workspace's part alone, its room's circle or the whole machine.
            Block = new StandingFacts { Workspace = circle },
        };
    }

    /// <summary>
    /// Ask Daoris's place under the access its start got (HELPMODE1a1, D183 point 1). Restricted is the place as HELP1a made it. Full
    /// asks nothing and allows nothing by list, its harness's mode doing neither; keeps every deny the person's rules compose; is
    /// handed every plugin server here, approved or not (D170 as D183 amends it); and is handed no tree guard, which would keep its
    /// file writes inside the room when Daoris's own housekeeping, its trees and its home, is outside it (D183 point 5). Its circle
    /// stays its room's workspace (ASKSCOPE1c), so its connector's searches stay scoped though its shell reaches anything.
    /// </summary>
    internal static ChatPlace Under(ChatPlace place, HelpAccess access) => access is not { Full: true, Refusal: null }
        ? place
        : place with
        {
            Rules = (file, _) => HelpRoom.FullRules(file, place.Circle),
            Posture = access.Posture,
            PermissionMode = access.PermissionMode,
            EveryServer = true,
            Guarded = false,
            Reads = [],
        };

    /// <summary>
    /// A conversation's place under Ask Daoris's mode as the person chose it, read at this start (HELPMODE1a1), a conversation going on
    /// among them; a repository's conversation's is its own, and says nothing. With it, what the start got, for the first line of
    /// its transcript (D183 point 3), and the mode its harness runs in, the effective one, which its record keeps (HELPMODE1a2).
    /// </summary>
    /// <param name="ran">
    /// The mode a conversation going on last ran under, as its record says (HELPMODE1a2); its first line says the change first where
    /// it now goes on under another. Null for a new conversation.
    /// </param>
    /// <exception cref="DriverException">Full access on an agent with no mode that asks nothing: it ends saying so.</exception>
    internal static (ChatPlace Place, string? Said, string? Mode) UnderMode(
        ChatPlace place, ChatAgent agent, DriverConfig config, string? ran = null)
    {
        if (!string.Equals(place.Name, HelpRoom.Repository, StringComparison.Ordinal)) return (place, null, null);
        var access = HelpAccess.For(config.HelpMode, agent);
        if (access.Refusal is { } refused) throw new DriverException(refused);
        var changed = ran is null ? null : HelpModes.ChangedSince(ran, access.Mode);
        return (Under(place, access), changed is null ? $"— {access.Said}" : $"— {changed} {access.Said}", access.Mode);
    }
}
