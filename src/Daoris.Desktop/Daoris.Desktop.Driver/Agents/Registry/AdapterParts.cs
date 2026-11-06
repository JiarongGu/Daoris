namespace Daoris.Driver;

/// <summary>
/// A door's rule language, skill layout and server handing as today's readers ask them of an adapter (rows o and p): a built door's
/// from its provider and its spawn, stated absent where they state none; a declared door (a plugin's), which declares none of them,
/// takes Claude Code's language where it takes settings and Claude Code's layout. Row t ends this reading with the adapters.
/// </summary>
public static class AdapterParts
{
    /// <summary>The language the door's rules are written in, or null where the person's rules reach none of its sessions.</summary>
    public static IRuleLanguage? RuleLanguage(this ISessionAdapter adapter) => adapter is LegacyDoor { Declared: false } legacy
        ? legacy.Provider.Rules.TryGet(out var language) ? language : null
        : adapter.TakesSettings ? ClaudeRules.Language : null;

    /// <summary>The door's own home: the variable naming it, whether it starts in the person's own, and the trust file it keeps.</summary>
    public static DoorHome Home(this ISessionAdapter adapter) => LegacyDoor.Of(adapter).Door.Home;

    /// <summary>The layout the door's skills are copied in, or null where it is handed none.</summary>
    public static ISkillLayout? SkillLayout(this ISessionAdapter adapter) => adapter is LegacyDoor { Declared: false } legacy
        ? legacy.Provider.Skills.TryGet(out var layout) ? layout : null
        : ClaudeSkills.Layout;

    /// <summary>How the door takes a start's servers at its spawn, or null where they ride its wire or it takes none there.</summary>
    public static IServerHanding? ServerHanding(this ISessionAdapter adapter) =>
        LegacyDoor.Of(adapter).Door.Spawn.TryGet(out var spawn) ? spawn as IServerHanding : null;
}
