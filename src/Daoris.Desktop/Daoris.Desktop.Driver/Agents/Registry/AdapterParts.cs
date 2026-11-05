namespace Daoris.Driver;

/// <summary>
/// A door's rule language, skill layout and server handing as today's readers ask them of an adapter (rows o and p): a contract
/// door's from its provider and its spawn, stated absent where they state none; a declared door (a plugin's) or a test's adapter, which declares none of
/// them, takes what every such adapter took before them: Claude Code's language where it takes settings, Claude Code's layout, and its
/// servers file on a door whose servers do not ride the wire. Row t ends the second reading with the adapters.
/// </summary>
public static class AdapterParts
{
    /// <summary>The language the door's rules are written in, or null where the person's rules reach none of its sessions.</summary>
    public static IRuleLanguage? RuleLanguage(this ISessionAdapter adapter) => adapter is LegacyDoor { Declared: false } legacy
        ? legacy.Provider.Rules.TryGet(out var language) ? language : null
        : adapter.TakesSettings ? ClaudeRules.Language : null;

    /// <summary>The door's own home: the variable naming it, whether it starts in the person's own, and the trust file it keeps.</summary>
    public static DoorHome Home(this ISessionAdapter adapter) => adapter is LegacyDoor legacy
        ? legacy.Door.Home
        : DoorHome.Of(adapter.Toolchain);

    /// <summary>The layout the door's skills are copied in, or null where it is handed none.</summary>
    public static ISkillLayout? SkillLayout(this ISessionAdapter adapter) => adapter is LegacyDoor { Declared: false } legacy
        ? legacy.Provider.Skills.TryGet(out var layout) ? layout : null
        : ClaudeSkills.Layout;

    /// <summary>How the door takes a start's servers at its spawn, or null where they ride its wire or it takes none there.</summary>
    public static IServerHanding? ServerHanding(this ISessionAdapter adapter) => adapter is LegacyDoor legacy
        ? legacy.Door.Spawn.TryGet(out var spawn) ? spawn as IServerHanding : null
        : adapter.Abilities().ServersOnWire ? null : new AdapterServers(adapter);

    /// <summary>A plugin's or a test's pipe adapter: the servers file, named by the adapter's own door.</summary>
    private sealed class AdapterServers(ISessionAdapter adapter) : IServerHanding
    {
        public string? Hand(System.Diagnostics.ProcessStartInfo info, string home, ConnectorPlan plan)
        {
            var file = ClaudeServers.Write(home, plan);
            if (file is not null) adapter.HandServers(info, file);
            return file;
        }
    }
}
