namespace Daoris.Driver;

/// <summary>
/// One agent as last read, nothing asked (COMPANYVIEW1c, <see cref="HarnessRoster.LastRead"/>): its binary and the accounts a start of it
/// walks.
/// </summary>
/// <param name="Agent">The adapter's name, which its offer readings are kept under.</param>
/// <param name="Owner">Whose accounts it runs as (AGT7).</param>
/// <param name="Installed">Whether its binary is there by its last answer, or a pin's install; null where it was not asked this process.</param>
/// <param name="Version">The version that answer named; null where it named none or was not asked.</param>
/// <param name="Accounts">Each account a start walks, in its order; none where it has no account of Daoris's; one with no id for its own home.</param>
public sealed record AgentLastRead(string Agent, string Owner, bool? Installed, string? Version, IReadOnlyList<AccountLastRead> Accounts)
{
    /// <summary>The version a pin names for the workspace asked, where one does; an install missing at it reads as not installed.</summary>
    public string? Pinned { get; init; }
}

/// <summary>One account as a start's walk finds it before any question, beside its last sign-in reading.</summary>
/// <param name="Account">The account's id; null for the agent's own home, whose sign-in the walk never asks.</param>
/// <param name="State">Its cool-off or its provider's refusal, else ready.</param>
/// <param name="Read">Its last sign-in reading, with when; null where it was never read.</param>
public sealed record AccountLastRead(string? Account, AccountState State, AccountRead? Read);
