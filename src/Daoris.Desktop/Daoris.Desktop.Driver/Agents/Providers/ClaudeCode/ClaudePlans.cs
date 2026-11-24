namespace Daoris.Driver;

/// <summary>
/// How Claude Code says an account's plan, and what each word's size is: read from <c>auth status</c> (measured on 2.1.331) and
/// <c>get_usage</c>, never a credential. See docs/2026-11-24-account-plan-evidence.md.
/// </summary>
internal static class ClaudePlans
{
    /// <summary>
    /// The plan in the status's own words: the tier where it names one (<c>max_5x</c>, <c>max_20x</c>; it says <c>other</c> of the
    /// rest, which is then no tier), else the type (<c>team</c>, <c>max</c>). The tier is tried first wherever it stands in the answer.
    /// </summary>
    public const string Plan =
        @"^(?=[\s\S]*""planTier""\s*:\s*""(?<1>(?!other"")[^""]+)"")|""subscriptionType""\s*:\s*""(?<1>[^""]+)""";

    /// <summary>
    /// The plans whose word states a size: a Max tier names its multiple of the base plan, <c>pro</c>. A bare <c>max</c> (the
    /// usage reading's word), <c>team</c> and any other state none, so they have none declared and weigh as the base.
    /// </summary>
    public static IReadOnlyDictionary<string, double> Sizes { get; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["pro"] = 1, ["max_5x"] = 5, ["max_20x"] = 20 };
}
