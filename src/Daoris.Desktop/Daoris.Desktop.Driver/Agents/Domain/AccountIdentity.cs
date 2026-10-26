namespace Daoris.Driver;

/// <summary>The account identity rule shared by starts that carry on a session (TOOL4f, WALKAGENT1).</summary>
internal static class AccountIdentity
{
    /// <summary>
    /// Whether a start runs on another account than the session it carries on ran on (TOOL4f): the record's account
    /// against the selection's, compared as the wiring compares names, null being the tool's own home. A start on another
    /// <paramref name="agent"/> than the record ran on is on another account whatever the two are named (WALKAGENT1): an account
    /// belongs to its agent, and two agents may each have one of that name.
    /// </summary>
    internal static bool OnAnotherAccount(PriorSession prior, string? profile, string? agent = null) =>
        !string.Equals(prior.Profile ?? "", profile ?? "", StringComparison.OrdinalIgnoreCase)
        || (agent is { Length: > 0 } && prior.Adapter is { Length: > 0 } ran && !string.Equals(ran, agent, StringComparison.OrdinalIgnoreCase));
}
