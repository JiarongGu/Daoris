namespace Daoris.Driver;

/// <summary>
/// What a start's walk reads of an account held by an API key alone, with no question to ask the tool: no key reads signed out at once,
/// by a file read; a key the provider can check and nobody has yet is owed one cheap read; any other reading kept stands.
/// </summary>
internal static class KeyOnlyWalk
{
    /// <summary>
    /// The reading the walk acts on, or null where the agent's accounts are signed in with the tool itself and the kept reading
    /// stands. <c>Unread</c> is a key never checked, asked once before the account is chosen.
    /// </summary>
    internal static (LoginState Login, bool Unread)? Reading(HarnessRoster roster, string owner, string account, AccountRead? kept)
    {
        if (roster.AgentOf(owner) is not { Accounts.KeyOnly: true } agent) return null;
        if (HarnessKeys.Of(roster.Home, owner, account) is null) return (LoginState.Out, false);
        return (kept?.Login ?? LoginState.Unknown, kept is null && agent.Accounts.KeyCheck is not null);
    }

    /// <summary>The sentence of an account that cannot run for want of a key the provider accepts.</summary>
    internal static string Refusal(string owner, string name, string profile) =>
        $"the `{owner}` account `{name}` has no API key its provider accepts, so a session would have nothing to run as — "
        + $"`daoris agent key {owner} --account {profile}` reads one from standard input, or Agents → the agent's page → Accounts.";
}
