namespace Daoris.Driver;

/// <summary>
/// Whether a repository's <c>daoris.gates.json</c> waits for the owner, as the risk policy answers it: the judge's verdict on the act
/// <c>command.change</c> at the repository's place, the one reader the check (<see cref="CheckDeclaration"/>), the line's
/// acceptance (<see cref="IntegrationOwed.Passed"/>, <see cref="DeclarationAcceptance"/>), the held checks' rerun (<see cref="ChangedChecks"/>)
/// and the window ask. <see cref="Full"/> where the verdict passes (by the seed, under full access: its agents run any command in their own
/// sessions, so a work's check runs the declaration it committed and the line's is accepted as it lands, said in one line, never asked).
/// <see cref="Restricted"/> otherwise: a check runs the commands the owner accepted and a command change waits for the yes the verdict names.
/// The words are the wire's and the owner's, whatever row decided.
/// </summary>
public static class DeclarationMode
{
    /// <summary>Nothing waits: a work's own declaration runs, and the line's is taken as it lands.</summary>
    public const string Full = "full";

    /// <summary>A command change waits for the yes the policy names.</summary>
    public const string Restricted = "restricted";

    /// <summary>The verdict on a command change of <paramref name="repository"/>'s declaration at its place under <paramref name="config"/>, its levels resolved as its sessions' own setup (<see cref="AccessRules.Resolve"/>).</summary>
    public static RiskVerdict Verdict(DriverConfig config, string repository, string? workspace, string subject) =>
        DeclarationRisk.Current.Verdict(config, repository, workspace, subject);

    /// <summary>The mode of <paramref name="repository"/> under <paramref name="config"/>: <see cref="Full"/> where the policy passes a command change there.</summary>
    public static string Of(DriverConfig config, string repository, string? workspace) =>
        Verdict(config, repository, workspace, $"{repository}'s declaration").Outcome == RiskVerdict.Pass ? Full : Restricted;

    /// <summary>Whether a command change of <paramref name="repository"/>'s declaration waits for a yes under <paramref name="config"/>.</summary>
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

    /// <summary>Which mode decided, in the driver's words: <c>the risk policy passes it: taken without asking</c> or <c>the risk policy asks for it: waits for you</c>.</summary>
    public static string Said(string mode) => mode == Full ? "the risk policy passes it: taken without asking" : "the risk policy asks for it: waits for you";
}

/// <summary>What a declaration's acceptance asks of the risk policy, which Workflows keeps: the verdict on <c>command.change</c> at a repository's place. Workflows installs it when the driver loads.</summary>
public interface IDeclarationRisk
{
    RiskVerdict Verdict(DriverConfig config, string repository, string? workspace, string subject);
}

/// <summary>The Workflows operation; unwired, it reads no judge, so a command change waits on the owner.</summary>
public static class DeclarationRisk
{
    private static IDeclarationRisk? _current;

    public static IDeclarationRisk Current
    {
        get => _current ?? Unwired;
        set => _current = value;
    }

    private static IDeclarationRisk Unwired { get; } = new UnwiredRisk();

    private sealed class UnwiredRisk : IDeclarationRisk
    {
        public RiskVerdict Verdict(DriverConfig config, string repository, string? workspace, string subject) =>
            new(RiskActs.CommandChange, null, RiskVerdict.OwnerOutcome, RiskClasses.Owner, null, RiskVerdict.Unreadable, "unwired",
                "no judge of the risk policy is wired in this process, so it reads at the owner");
    }
}
