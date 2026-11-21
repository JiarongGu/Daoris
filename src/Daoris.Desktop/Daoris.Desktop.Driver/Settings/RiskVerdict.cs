namespace Daoris.Driver;

/// <summary>
/// The judge's answer for one act: <see cref="Pass"/>, a <see cref="Gate"/> on a role (<see cref="By"/>), or the
/// <see cref="OwnerOutcome"/>; the class it read, the rule that decided (<see cref="Source"/>: a place's row, the seed, the fixed root, an
/// unreadable record, or a level carried from <c>placeRisk</c>), at the policy's revision, and why in a sentence.
/// </summary>
public sealed record RiskVerdict(string Kind, string? Qualifier, string Outcome, string Class, string? By, string Source, string Revision, string Reason)
{
    public const string Pass = "pass";
    public const string Gate = "gate";
    public const string OwnerOutcome = "owner";

    /// <summary>The source of a verdict the seed decided.</summary>
    public const string Seed = "seed";

    /// <summary>The source of <c>policy.change</c>'s verdict: the governance root, fixed at the owner.</summary>
    public const string Fixed = "fixed";

    /// <summary>The source of every verdict while the record does not read.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>What a hold the policy passed says first.</summary>
    public const string Passes = "no risk is declared for it here, so it passes";

    /// <summary>Why it stands, with the rule that decided at its revision.</summary>
    public string Why => $"{Reason} (rule {Source} at {Revision})";

    /// <summary>The words a hold the policy passed carries: <see cref="Passes"/> and why.</summary>
    public string Passed => $"{Passes}: {Why}";

    /// <summary>What a class gives: none passes, a role gates, the owner holds.</summary>
    public static string OutcomeOf(string riskClass) => riskClass switch
    {
        RiskClasses.None => Pass,
        RiskClasses.Role => Gate,
        _ => OwnerOutcome,
    };
}
