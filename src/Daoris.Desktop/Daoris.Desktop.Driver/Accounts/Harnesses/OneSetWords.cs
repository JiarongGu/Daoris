namespace Daoris.Driver;

/// <summary>
/// What a door that wrote a scope's list or a default says now that an agent's accounts are one set (POOL1b, D197 point 6; the
/// account-pool design §5.1 and §5.3): the terminal's verbs and Ask Daoris's doors refuse in these words, so neither says it its own way.
/// A flag that no longer means anything is refused rather than ignored, so a script never believes it set something (D50).
/// </summary>
public static class OneSetWords
{
    /// <summary><c>profile default</c>, and Ask Daoris's retired <c>default</c>: the order says where starts begin.</summary>
    public static string Default(string agent, string? account = null) =>
        "every account of Daoris's serves every workspace (D197), and the order says where starts begin — "
        + $"`daoris agent profile order {agent} {(account is null ? "<account>" : ShellWord.Of(account, ShellWord.Account))}` puts one first";

    /// <summary><c>profile join</c>, and <c>login</c>'s <c>--join</c> and <c>--join-machine</c>: the account joins by itself.</summary>
    public const string Join = "there is nothing to join: every account signed in for Daoris serves every workspace (D197)";

    /// <summary>
    /// What a new account's sign-in says at its end (D197 point 4, the account-pool design §4.4's English): the window's
    /// <c>HARNESS_ENDED</c> and the terminal's <c>login</c> alike; a sign-back-in says nothing of it.
    /// </summary>
    public static string Joined(string account) => $"`{account}` joined Daoris's accounts: every workspace's work may run on it.";

    /// <summary>A workspace named on <c>order</c> or <c>use</c>, the terminal's <c>--workspace</c> or Ask Daoris's <c>workspace</c>.</summary>
    public static string Workspace(string flag) => $"Daoris's accounts serve every workspace (D197), so `{flag}` names nothing here";

    /// <summary>A stored Ask Daoris <c>use</c> or <c>order</c> proposal written before the <c>set</c> mark, refused at its Apply.</summary>
    public const string Unmarked = "written for a list that is gone (D197): propose it again";
}
