namespace Daoris.Driver;

/// <summary>
/// What an account its agent or provider refused says, and the one reading of a refused sign-in: the
/// lines a start's conclusion, a conversation's refused turn and a later held start carry. Accounts', not the loop's:
/// a conversation reads a refused sign-in as a driven start does, and no module names the loop.
/// </summary>
internal static class RefusedAccounts
{
    /// <summary>
    /// The one reading of a door's failure for a refused sign-in, a driven start's conclusion's and a conversation's
    /// refused turn's: where the adapter's table recognises <paramref name="failure"/>, the account reads signed out,
    /// the tool's own sign-in is held until a person looks again, and the line for the record comes back. Null where the table
    /// does not recognise it, and nothing is written.
    /// </summary>
    /// <param name="failure">What the door said refusing the call, never the transcript.</param>
    /// <param name="byCode">The door's own code read it as access: its sign-in, whatever the words.</param>
    internal static Noted? SignInRefused(HarnessRoster harnesses, AgentDoor adapter, string? profile, string? failure, bool byCode = false)
    {
        if (harnesses.Adapters.AgentNamed(adapter.Id.Value) is null
            || (!byCode && (failure is not { Length: > 0 } || !SignInRefusals.Read(harnesses.SignInOf(adapter.Id.Value), failure))))
        {
            return null;
        }

        return SignOut(harnesses, adapter, profile);
    }

    /// <summary>
    /// <see cref="SignInRefused"/>'s writes, reading no words: the account reads signed out, the
    /// tool's own sign-in is held until a person looks again, and the line comes back. The trusted entry a bound kind's sign-in takes.
    /// </summary>
    internal static Noted SignOut(HarnessRoster harnesses, AgentDoor adapter, string? profile)
    {
        var owner = harnesses.Adapters.OwnerOfAgent(adapter.Id.Value).Value;
        harnesses.SignedOut(adapter.Id.Value, profile);
        if (profile is null)
        {
            var hold = SignedOutHold(owner);
            harnesses.Refuse(adapter.Id.Value, null, hold.Note, hold);
        }

        return SignedOutNote(owner, profile);
    }

    /// <summary>
    /// What a refused sign-in says: the account by its owner and name with its sign-in, both doors named, or the tool's own sign-in,
    /// which the person signs in to at their own terminal. The command's id is spelled for
    /// any shell.
    /// </summary>
    private static string SignedOutReason(string owner, string? profile) => profile is { } named
        ? $"the agent refused the `{owner}` account `{named}` for its sign-in, so it reads signed out and Daoris starts nothing "
          + $"more on it until it is signed in: `daoris agent login {owner} --account {ShellWord.Of(named, ShellWord.Account)}`, "
          + "or Agents → the agent's page → Accounts."
        : $"the agent refused `{owner}`'s own sign-in, so Daoris starts nothing more on it until you sign in again at your "
          + "terminal and read it again on the agent's page in Agents.";

    /// <summary>
    /// The refused sign-in's line on a record: the English names the account, which the cleaner cuts where the note
    /// travels; the values carry only its owner, never its name, as the refused credential's line does.
    /// </summary>
    internal static Noted SignedOutNote(string owner, string? profile)
    {
        var reason = SignedOutReason(owner, profile);
        return Noted.Of(
            profile is null ? AccountNoteCodes.AccountSignedOutOwn : AccountNoteCodes.AccountSignedOut,
            $"{char.ToUpperInvariant(reason[0])}{reason[1..]}", ("owner", owner));
    }

    /// <summary>
    /// What a refused credential says: the account by its owner and name, or the tool's own sign-in, which is the person's,
    /// outside Daoris's accounts, so it is signed in again with the tool itself
    /// at their terminal, never through Daoris. The catalogues' `note.account.refused-own` and `note.held.refused-own` say the same.
    /// </summary>
    private static string RefusedReason(string owner, string? profile) => profile is { } named
        ? $"its provider refused the `{owner}` account `{named}` (401). Replace the key or sign in again — "
          + "on Settings, or `daoris agent` — and Daoris will start sessions on it again."
        : $"its provider refused `{owner}`'s own sign-in (401). It is your own, outside Daoris's accounts: sign it in again with the "
          + "tool itself, at your terminal.";

    /// <summary>
    /// What a later start held on a refused credential says, by code beside its English: the account by its
    /// owner and id, never the name <paramref name="named"/> the English says, which the page words.
    /// </summary>
    internal static Noted RefusedHold(string owner, string? profile, string? named) => profile is { } account
        ? Noted.Of(HeldNoteCodes.HeldRefusedAccount, $"an earlier session found that {RefusedReason(owner, named)}", ("owner", owner), ("account", account))
        : Noted.Of(HeldNoteCodes.HeldRefusedOwn, $"an earlier session found that {RefusedReason(owner, named)}", ("owner", owner));

    /// <summary>What a later start held on the tool's own sign-in, refused, says by code beside its English.</summary>
    internal static Noted SignedOutHold(string owner) =>
        Noted.Of(HeldNoteCodes.HeldSignInRefusedOwn, $"an earlier session found that {SignedOutReason(owner, null)}", ("owner", owner));

    /// <summary>
    /// The refused credential's line on a record: the English names the account,
    /// which the cleaner cuts where the note travels; the values carry only its owner, never its name.
    /// </summary>
    internal static Noted RefusedNote(string owner, string? profile)
    {
        var reason = RefusedReason(owner, profile);
        return Noted.Of(
            profile is null ? AccountNoteCodes.AccountRefusedOwn : AccountNoteCodes.AccountRefused,
            $"{char.ToUpperInvariant(reason[0])}{reason[1..]}", ("owner", owner));
    }
}
