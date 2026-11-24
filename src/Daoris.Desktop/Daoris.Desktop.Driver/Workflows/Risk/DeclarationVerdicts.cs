using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>One changed unit of a declaration (<see cref="DeclarationUnits"/>), the digest the line holds for it and the judge's verdict on the act <c>command.change</c> on that digest.</summary>
/// <param name="Digest">The unit's digest at the line's tip; empty where the line dropped the unit, or no unit is read and the whole file is the subject.</param>
public sealed record DeclarationUnitVerdict(string Unit, string Digest, RiskVerdict Verdict);

/// <summary>
/// The judge's verdicts on a changed declaration: one <c>command.change</c> per changed unit at the repository's
/// place, the subject the unit and its digest, so a policy that passes a change takes every unit it names and one that does not holds the
/// change for the yes it names. With no unit read (nothing accepted, a text that does not read) the declaration is the one subject. The same
/// judge answers the check's mode (<see cref="DeclarationMode"/>), so the acceptance and the runs never answer apart.
/// </summary>
public static class DeclarationVerdicts
{
    /// <summary>The act every changed unit is.</summary>
    public static readonly RiskAct Act = new(RiskActs.CommandChange, null);

    /// <summary>The verdict on a command change at <paramref name="repository"/>'s place under <paramref name="config"/>, <paramref name="subject"/> what changes.</summary>
    public static RiskVerdict Judge(DriverConfig config, string repository, string? workspace, string actor, string subject) =>
        RiskGate.Judge(RiskPolicy.Of(config), Act, actor, Place(config, workspace, repository), subject);

    /// <summary>The place a command change is judged at: read by its reach, the permission prompts.</summary>
    public static RiskPlace Place(DriverConfig config, string? workspace, string? repository) => RiskPlace.Of(config, workspace, repository).Reaching();

    /// <summary>
    /// The verdicts on <paramref name="changed"/>, each with its digest in <paramref name="digests"/> (the line's units), asked of the policy
    /// <paramref name="config"/> holds; a declaration with no unit read has one verdict, on the whole file at <paramref name="blob"/>.
    /// </summary>
    public static IReadOnlyList<DeclarationUnitVerdict> Of(
        DriverConfig config, string repository, string? workspace, string line, string blob, IReadOnlyList<string> changed, IReadOnlyDictionary<string, string>? digests)
    {
        var actor = $"line:{line}";
        if (changed.Count == 0)
        {
            return [new("", "", Judge(config, repository, workspace, actor, $"{repository}'s daoris.gates.json at {Short(blob)}"))];
        }

        return
        [
            .. changed.Select(unit =>
            {
                var digest = digests is not null && digests.TryGetValue(unit, out var found) ? found : "";
                return new DeclarationUnitVerdict(unit, digest, Judge(config, repository, workspace, actor, $"{repository}'s {DeclarationUnits.Said(unit)} at {Short(digest.Length > 0 ? digest : blob)}"));
            }),
        ];
    }

    /// <summary>Whether the policy passes every verdict: nothing waits for a yes.</summary>
    public static bool Passes(IReadOnlyList<DeclarationUnitVerdict> verdicts) => verdicts.All(each => each.Verdict.Outcome == RiskVerdict.Pass);

    /// <summary>The first verdict that holds the change, or else the first: the one a line says.</summary>
    public static RiskVerdict Deciding(IReadOnlyList<DeclarationUnitVerdict> verdicts) =>
        (verdicts.FirstOrDefault(each => each.Verdict.Outcome != RiskVerdict.Pass) ?? verdicts[0]).Verdict;

    private static string Short(string text) => text[..Math.Min(12, text.Length)];
}

/// <summary>The judge a declaration's mode reads (<see cref="IDeclarationRisk"/>), installed when the driver loads.</summary>
internal sealed class DeclarationRiskOperations : IDeclarationRisk
{
    [ModuleInitializer]
    internal static void Install() => DeclarationRisk.Current = new DeclarationRiskOperations();

    public RiskVerdict Verdict(DriverConfig config, string repository, string? workspace, string subject) =>
        DeclarationVerdicts.Judge(config, repository, workspace, "declaration", subject);
}
