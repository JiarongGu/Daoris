namespace Daoris.Driver;

/// <summary>
/// The accounts a held start passed because they are not signed in (TOOL6g): whose, and which, in the order the walk tried
/// them. Machine-local, as every account name is: the page says them in the reader's language, and the log by name.
/// </summary>
/// <param name="Agent">Whose accounts: the owner a door runs as (AGT7), which `daoris agent login` takes.</param>
public sealed record SignedOutAccounts(string Agent, IReadOnlyList<string> Accounts)
{
    /// <summary>
    /// The name the person gave each of <see cref="Accounts"/> (ACCT2b), in the same order, null where one has none: read when
    /// the hold was said, so the page says each by it. The ids stay the facts a sign-in and the log take.
    /// </summary>
    public IReadOnlyList<string?> Names { get; init; } = [];

    /// <summary>
    /// True where this is OWNSPACE1a's no-account hold (D175): no Daoris account of the agent's is signed in, so a start waits
    /// for a sign-in. The page says that hold from this fact, never from <see cref="Accounts"/> being empty (OWNSPACE1b-B).
    /// </summary>
    public bool NoAccount { get; init; }

    /// <summary>The accounts, each with the name the person gave it where <paramref name="names"/> holds one.</summary>
    public static SignedOutAccounts Of(string agent, IReadOnlyList<string> accounts, IReadOnlyDictionary<string, string>? names) =>
        new(agent, accounts)
        {
            Names = [.. accounts.Select(account => names is not null && names.TryGetValue(account, out var name) ? name : null)],
        };
}
