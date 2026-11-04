namespace Daoris.Driver;

/// <summary>
/// What a door says now that an agent's accounts are one set (POOL1b, D197 point 6; the account-pool design §5.1 and §5.3): the
/// terminal's verbs and Ask Daoris's doors say it in these words, so neither says it its own way.
/// </summary>
public static class OneSetWords
{
    /// <summary>
    /// What a new account's sign-in says at its end (D197 point 4, the account-pool design §4.4's English): the window's
    /// <c>HARNESS_ENDED</c> and the terminal's <c>login</c> alike; a sign-back-in says nothing of it.
    /// </summary>
    public static string Joined(string account) => $"`{account}` joined Daoris's accounts: every workspace's work may run on it.";
}
