namespace Daoris.Driver;

/// <summary>
/// Where Ask Daoris's conversation runs, and under which access: its place (<see cref="In"/>), the place under the access its start got
/// (<see cref="Under"/>) and under the mode the person chose (<see cref="UnderMode"/>).
/// </summary>
internal static class HelpPlacement
{
    /// <summary>
    /// Where Ask Daoris's conversation runs: its room, the room's allows and a read of its own files folder, and the
    /// checkouts reading across lets it read; the agent's own asking mode; its tools up front.
    /// </summary>
    /// <remarks>
    /// <para><b>Its conversation is kept</b>: the person goes back to an earlier one from its history, and words written to
    /// one that ended go on in its own harness conversation by the id kept for it, as a repository's chat does.</para>
    ///
    /// <para><b>A plugin's server reaches it only where the plugin declares it for Ask Daoris</b>, and
    /// only once the person approved its exact tools (<see cref="HelpGrants"/>): its reads and the writes they
    /// accepted are allowed, any other write asked, which its mode refuses. It is handed none, since a browser brought up for
    /// a tool it may not use is a window for nothing; a server approved for it names the tools it may use.</para>
    ///
    /// <para>Its tools default to its room's workspace (<see cref="HelpCircle.Of"/>): its connector's circle, and the
    /// person's denies for that workspace in its rules beside the checkouts its room grants, every workspace's.</para>
    /// </remarks>
    /// <param name="workspace">The workspace its record keeps: what setting it aside is judged by; null is <c>default</c>.</param>
    internal static ChatPlace In(string home, string room, IReadOnlyList<HelpRead> reads, string? workspace = null, HelpFocus? focus = null)
    {
        var circle = focus is null ? HelpCircle.Of(home, room, workspace) : HelpCircle.OfFocus(focus);
        var access = ConfiguredAccess.Read(home);
        // The readable checkouts the person left on, in any workspace, read through `checkout_read`, which each call judges again.
        IReadOnlyList<HelpRead> handed = [.. reads.Where(read => access.Reads(read.Name))];
        return new(
            HelpRoom.Repository, room,
            (file, id) =>
            {
                var grants = HelpGrants.Read(home, PluginCatalog.Load(home, ProviderRegistry.Built().Names));
                return HelpRoom.Rules(file, ChatFiles.Folder(home, id), handed, grants.Allow, grants.Ask, circle);
            },
            Plugins: true, ConnectorOnPipe: true, Posture: HelpRoom.Posture)
        {
            Keeps = true,
            ServersTo = PluginServerTo.Help,
            Workspace = RemoteTarget.Workspace(workspace),
            Circle = circle,
            Reads = handed,
            CheckoutsOff = access.CheckoutsOff,
            OwnWithheld = OwnFamilies.Servers | OwnFamilies.Plugins | OwnFamilies.Chrome,
            // In no repository, its standing block is the workspace's part alone, its room's circle or the whole machine, and its focus,
            // which the room its workspace's other conversations read never says: by name, a start that read the machine adding its facts.
            Block = new StandingFacts { Workspace = circle, Focus = HelpFocusTold.Words(null, workspace, focus ?? HelpFocus.Home(workspace)) },
        };
    }

    /// <summary>
    /// Ask Daoris's place under the access its start got. Restricted is the baseline place. Full
    /// asks nothing and allows nothing by list, its harness's mode doing neither; keeps every deny the person's rules compose; is
    /// handed every plugin server here, approved or not; and is handed no tree guard, which would keep its
    /// file writes inside the room when Daoris's own housekeeping, its trees and its home, is outside it. Its circle and its readable
    /// checkouts stay its room's, so its connector's searches default there though its shell reaches anything.
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
        };

    /// <summary>
    /// A conversation's place under Ask Daoris's mode as the person chose it, read at this start, a conversation going on
    /// among them; a repository's conversation's is its own, and says nothing. With it, what the start got, for the first line of
    /// its transcript, and the mode its harness runs in, the effective one, which its record keeps.
    /// </summary>
    /// <param name="ran">
    /// The mode a conversation going on last ran under, as its record says; its first line says the change first where
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
