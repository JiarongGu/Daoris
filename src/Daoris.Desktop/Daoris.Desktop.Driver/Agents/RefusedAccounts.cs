namespace Daoris.Driver;

/// <summary>
/// What an account its agent or provider refused says, and the one reading of a refused sign-in (AGT3b, ROSTER1b): the
/// lines a start's conclusion, a conversation's refused turn and a later held start carry. Accounts', not the loop's
/// (LOOPOUT1): a conversation reads a refused sign-in as a driven start does, and no module names the loop.
/// </summary>
internal static class RefusedAccounts
{
    /// <summary>
    /// The one reading of a door's failure for a refused sign-in (ROSTER1b), a driven start's conclusion's and a conversation's
    /// refused turn's (SIGNIN1b): where the adapter's table recognises <paramref name="failure"/>, the account reads signed out,
    /// the tool's own sign-in is held until a person looks again, and the line for the record comes back. Null where the table
    /// does not recognise it, and nothing is written.
    /// </summary>
    /// <param name="failure">What the door said refusing the call (ACPEND1), never the transcript (AGT3c).</param>
    internal static Noted? SignInRefused(HarnessRoster harnesses, ISessionAdapter adapter, string? profile, string? failure)
    {
        if (failure is not { Length: > 0 } || adapter.Toolchain is not { } toolchain
            || !SignInRefusals.Read(harnesses.SignInOf(adapter.Name), failure))
        {
            return null;
        }

        var owner = toolchain.Owner(adapter.Name);
        harnesses.SignedOut(adapter.Name, profile);
        if (profile is null)
        {
            var hold = SignedOutHold(owner);
            harnesses.Refuse(adapter.Name, null, hold.Note, hold);
        }

        return SignedOutNote(owner, profile);
    }

    /// <summary>
    /// What a refused sign-in says (ROSTER1b): the account by its owner and name with its sign-in, both doors named (TOOL6g's
    /// words, D50), or the tool's own sign-in, which the person signs in to at their own terminal. The command's id is spelled for
    /// any shell (ACCTQUOTE1b).
    /// </summary>
    private static string SignedOutReason(string owner, string? profile) => profile is { } named
        ? $"the agent refused the `{owner}` account `{named}` for its sign-in, so it reads signed out and Daoris starts nothing "
          + $"more on it until it is signed in: `daoris agent login {owner} --profile {ShellWord.Of(named, ShellWord.Account)}`, "
          + "or Agents → the agent's page → Accounts."
        : $"the agent refused `{owner}`'s own sign-in, so Daoris starts nothing more on it until you sign in again at your "
          + "terminal and read it again on the agent's page in Agents.";

    /// <summary>
    /// The refused sign-in's line on a record (ROSTER1b): the English names the account, which the cleaner cuts where the note
    /// travels; the values carry only its owner, never its name, as AGT3b's line does.
    /// </summary>
    internal static Noted SignedOutNote(string owner, string? profile)
    {
        var reason = SignedOutReason(owner, profile);
        return Noted.Of(
            profile is null ? NoteCodes.AccountSignedOutOwn : NoteCodes.AccountSignedOut,
            $"{char.ToUpperInvariant(reason[0])}{reason[1..]}", ("owner", owner));
    }

    /// <summary>
    /// What a refused credential says (AGT3b): the account by its owner and name, or the tool's own sign-in, which is the person's,
    /// outside Daoris's accounts (POOL1c, D197 point 3; the account-pool design §4.4), so it is signed in again with the tool itself
    /// at their terminal, never through Daoris. The catalogues' `note.account.refused-own` and `note.held.refused-own` say the same.
    /// </summary>
    private static string RefusedReason(string owner, string? profile) => profile is { } named
        ? $"its provider refused the `{owner}` account `{named}` (401). Replace the key or sign in again — "
          + "on Settings, or `daoris agent` — and Daoris will start sessions on it again."
        : $"its provider refused `{owner}`'s own sign-in (401). It is your own, outside Daoris's accounts: sign it in again with the "
          + "tool itself, at your terminal.";

    /// <summary>
    /// What a later start held on a refused credential says (AGT3b, ROSTERREFUSE2), by code beside its English: the account by its
    /// owner and id, never the name <paramref name="named"/> the English says, which the page words.
    /// </summary>
    internal static Noted RefusedHold(string owner, string? profile, string? named) => profile is { } account
        ? Noted.Of(NoteCodes.HeldRefusedAccount, $"an earlier session found that {RefusedReason(owner, named)}", ("owner", owner), ("account", account))
        : Noted.Of(NoteCodes.HeldRefusedOwn, $"an earlier session found that {RefusedReason(owner, named)}", ("owner", owner));

    /// <summary>What a later start held on the tool's own sign-in, refused (ROSTER1b), says by code beside its English.</summary>
    internal static Noted SignedOutHold(string owner) =>
        Noted.Of(NoteCodes.HeldSignInRefusedOwn, $"an earlier session found that {SignedOutReason(owner, null)}", ("owner", owner));

    /// <summary>
    /// The refused credential's line on a record (LANG1a, the language design §4 rows 23–24): the English names the account,
    /// which the cleaner cuts where the note travels; the values carry only its owner, never its name.
    /// </summary>
    internal static Noted RefusedNote(string owner, string? profile)
    {
        var reason = RefusedReason(owner, profile);
        return Noted.Of(
            profile is null ? NoteCodes.AccountRefusedOwn : NoteCodes.AccountRefused,
            $"{char.ToUpperInvariant(reason[0])}{reason[1..]}", ("owner", owner));
    }
}
