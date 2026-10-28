namespace Daoris.Driver;

/// <summary>
/// What held a reviewer read-only (XAGENT1d, D155 point 5; design §5.2), as the opinion's record says it: the posture is the
/// adapter's, in its own words, and only where measured (D53). A mode's name is never relied on: one called <c>read-only</c>
/// runs a workspace-write sandbox (ACP3's reading of <c>codex-acp</c>).
/// </summary>
public static class OpinionPosture
{
    /// <summary>The agent takes Daoris's rules (Claude Code, by either door), so the opinion's rules held it, beside its copy.</summary>
    public const string RulesAndCopy = "rules-and-copy";

    /// <summary>Its copy alone held it: an agent whose posture as a reviewer nobody has measured is held by the floor only.</summary>
    public const string CopyAlone = "copy-alone";

    /// <summary>What held a reviewer on <paramref name="adapter"/>: its rules where it takes them, else its copy alone.</summary>
    public static string Of(ISessionAdapter adapter) => adapter.TakesSettings ? RulesAndCopy : CopyAlone;

    /// <summary>The posture as the record says it (design §5.2).</summary>
    public static string Said(string posture) => posture == RulesAndCopy
        ? "read-only by its agent's rules and its copy"
        : "read-only by its copy alone";
}
