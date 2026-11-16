namespace Daoris.Knowledge;

/// <summary>
/// How an ask's parts are integrated (the canon's scope mode, METHODS1): <c>per-part</c>, each part's landing on the ask's branch
/// gated by its own checks, as a plan written before reads; or <c>one-scope</c>, the parts merged into the ask's branch with no gate
/// each, and the full set and two reviews (wide and close) run once on the branch before it lands. The ask's own plan names it;
/// a revision that names none keeps the standing one.
/// </summary>
public static class TeamIntegration
{
    public const string PerPart = "per-part";
    public const string OneScope = "one-scope";

    public static bool IsOneScope(TeamPlan? plan) => plan?.Integration == OneScope;

    /// <summary>Why a writer's integration cannot be saved, or null where it can: a known one, on the ask's own plan.</summary>
    public static string? Refusal(string? integration, string? scope) =>
        integration switch
        {
            null => null,
            PerPart or OneScope when scope is not null => "A part's split names no integration: how the parts land is the ask's own plan's to say.",
            PerPart or OneScope => null,
            _ => $"An integration is `{OneScope}` or `{PerPart}`; `{integration}` is neither.",
        };

    /// <summary>The integration a revision stands under: the writer's, else the standing plan's.</summary>
    public static string? Carried(string? written, TeamPlan? standing) => written ?? standing?.Integration;
}
