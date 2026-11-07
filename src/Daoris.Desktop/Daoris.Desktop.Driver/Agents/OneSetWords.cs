namespace Daoris.Driver;

/// <summary>
/// What a door says where an agent's accounts are one set: the
/// terminal's verbs and Ask Daoris's doors say it in these words, so neither says it its own way.
/// </summary>
public static class OneSetWords
{
    /// <summary>
    /// What a new account's sign-in says at its end: the window's
    /// <c>HARNESS_ENDED</c> and the terminal's <c>login</c> alike; a sign-back-in says nothing of it.
    /// </summary>
    public static string Joined(string account) => $"`{account}` joined Daoris's accounts: every workspace's work may run on it.";
}
