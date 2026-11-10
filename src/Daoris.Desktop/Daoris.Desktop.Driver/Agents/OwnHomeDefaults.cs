namespace Daoris.Driver;

/// <summary>
/// The person's switches over what of their own tool home a session is handed: one default per family, listed and
/// switched with the rest (<see cref="PermissionRules.Defaults"/>), on until the person switches it off. Each adds and decides
/// nothing, so it holds no rule.
/// </summary>
public static class OwnHomeDefaults
{
    /// <summary>The skills, subagent definitions and commands of the person's own tool home.</summary>
    public const string SkillsId = "own-skills";

    /// <summary>The user-scope servers of the person's own tool home.</summary>
    public const string ServersId = "own-servers";

    /// <summary>The plugins the person's own tool home installed and enables.</summary>
    public const string PluginsId = "own-plugins";

    /// <summary>The Claude in Chrome switch of the person's own tool home.</summary>
    public const string ChromeId = "own-chrome";

    /// <summary>The defaults, in the order the rules list them after Daoris's own.</summary>
    public static IReadOnlyList<PermissionDefault> Defaults { get; } =
    [
        new(
            SkillsId, RuleList.Allow, [],
            "A session is handed the skills, subagents and commands of your own tool home, as your own run of the agent loads them."),
        new(
            ServersId, RuleList.Allow, [],
            "A session is handed the servers of your own tool home, but one named as Daoris's connector or carrying a Daoris key; "
            + "a review, helper or partner pass is handed none."),
        new(
            PluginsId, RuleList.Allow, [],
            "A session is handed the plugins your own tool home installed and enables, read where they are installed."),
        new(
            ChromeId, RuleList.Allow, [],
            "A session turns Claude in Chrome on where your own run of the agent turns it on by default."),
    ];

    /// <summary>The families <paramref name="rules"/> leave on.</summary>
    /// <param name="connectorOnly">A pass held to Daoris's connector alone, which is handed no server of the person's.</param>
    public static OwnFamilies Families(PermissionFile rules, bool connectorOnly)
    {
        var families = OwnFamilies.None;
        if (On(rules, SkillsId)) families |= OwnFamilies.Skills;
        if (!connectorOnly && On(rules, ServersId)) families |= OwnFamilies.Servers;
        if (On(rules, PluginsId)) families |= OwnFamilies.Plugins;
        if (On(rules, ChromeId)) families |= OwnFamilies.Chrome;
        return families;
    }

    private static bool On(PermissionFile rules, string id) => PermissionRules.IsOn(rules, Defaults.First(shipped => shipped.Id == id));
}
