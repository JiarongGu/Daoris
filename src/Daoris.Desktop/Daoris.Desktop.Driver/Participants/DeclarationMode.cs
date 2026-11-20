namespace Daoris.Driver;

/// <summary>
/// Whether a repository's <c>daoris.gates.json</c> waits for the owner, decided by its access mode alone: the one reader the check
/// (<see cref="CheckDeclaration"/>), the line's acceptance (<see cref="IntegrationOwed.ByMode"/>, <see cref="DeclarationAcceptance"/>), the
/// held checks' rerun (<see cref="ChangedChecks"/>) and the window ask. <see cref="Full"/> where the repository's permission prompts resolve
/// unrestricted (full access gives that, at whichever level): its agents run any command in their own sessions, so a work's check runs the
/// declaration it committed and the line's is accepted as it lands, said in one line, never asked. <see cref="Restricted"/> otherwise:
/// a check runs the commands the owner accepted and a command change waits for their press.
/// </summary>
public static class DeclarationMode
{
    /// <summary>Nothing waits: a work's own declaration runs, and the line's is taken as it lands.</summary>
    public const string Full = "full";

    /// <summary>A command change waits for the owner's acceptance.</summary>
    public const string Restricted = "restricted";

    /// <summary>The mode of <paramref name="repository"/> under <paramref name="config"/>, its levels resolved as its sessions' own setup (<see cref="AccessRules.Resolve"/>).</summary>
    public static string Of(DriverConfig config, string repository, string? workspace) =>
        AccessRules.Resolve(config, repository, workspace).Value(AccessRules.Permissions) == AccessRules.Unrestricted ? Full : Restricted;

    /// <summary>Whether a command change of <paramref name="repository"/>'s declaration waits for the owner under <paramref name="config"/>.</summary>
    public static bool Waits(DriverConfig config, string repository, string? workspace) => Of(config, repository, workspace) == Restricted;

    /// <summary>
    /// The mode of <paramref name="repository"/> as <paramref name="home"/>'s choices stand now, <paramref name="workspace"/> the one the work
    /// is bound to (the registry's, never one a checkout path guesses, so a verification tree outside a checkout reads as the line does);
    /// restricted where the choices do not read, so a file that does not read never loosens.
    /// </summary>
    internal static string Of(string home, string repository, string? workspace)
    {
        DriverConfig config;
        try
        {
            config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        }
        catch (DriverConfigUnreadableException)
        {
            return Restricted;
        }

        return Of(config, repository, workspace);
    }

    /// <summary>Which mode decided, in the driver's words: <c>full access: taken without asking</c> or <c>restricted: waits for you</c>.</summary>
    public static string Said(string mode) => mode == Full ? "full access: taken without asking" : "restricted: waits for you";
}
