namespace Daoris.Driver;

/// <summary>An account's sign-in as Daoris last knows it.</summary>
public enum SignInStanding
{
    /// <summary>Never read: a start runs on it as on any account its agent says nothing of.</summary>
    Unread,

    /// <summary>Its agent said signed in.</summary>
    In,

    /// <summary>Its agent said signed out, or its key is gone: no start runs on it until a sign-in.</summary>
    Out,

    /// <summary>
    /// Its sign-in check is asked and did not answer (it timed out, or said nothing this build reads): ranked last until a check
    /// answers, since a check that hangs is what an expired sign-in the agent cannot renew looks like.
    /// </summary>
    Unanswered,
}

/// <summary>
/// One account's sign-in state: its standing, when it is read, since when it says so, and for a sign-out whether the person
/// made it.
/// </summary>
/// <param name="At">When the reading is made; null where it never is.</param>
/// <param name="Since">When it began saying so: a sign-out's moment, kept across readings that say the same.</param>
/// <param name="NotByPerson">
/// A sign-out after a reading that said signed in: Daoris never signs an account out and removing one deletes it, so the agent
/// did, as when a refresh of its sign-in is refused.
/// </param>
public sealed record SignInState(SignInStanding Standing, DateTimeOffset? At, DateTimeOffset? Since, bool NotByPerson = false)
{
    /// <summary>The state's word on a wire: <c>in</c>, <c>out</c>, <c>unanswered</c>, <c>unread</c>.</summary>
    public string Word => Standing switch
    {
        SignInStanding.In => "in",
        SignInStanding.Out => "out",
        SignInStanding.Unanswered => "unanswered",
        _ => "unread",
    };
}

/// <summary>
/// The one reader of an account's sign-in state, from its last reading (<see cref="AccountReads"/>): what the walk ranks by
/// (<see cref="AccountFacts.SignIn"/>), what the account page shows (<see cref="ProfileReport.SignIn"/>) and what a sign-out's
/// notice tells (<see cref="SignOutNotices"/>). Pure.
/// </summary>
public static class SignInStates
{
    /// <summary>The state <paramref name="read"/> says; never read where it is null.</summary>
    public static SignInState Of(AccountRead? read) => read switch
    {
        null => new SignInState(SignInStanding.Unread, null, null),
        { Login: LoginState.In } => new SignInState(SignInStanding.In, read.At, read.Since ?? read.At),
        { Login: LoginState.Out } => new SignInState(SignInStanding.Out, read.At, read.Since ?? read.At, read.Before == LoginState.In),
        _ => new SignInState(SignInStanding.Unanswered, read.At, read.Since ?? read.At),
    };

    /// <summary>Whether a start ranks the account last: its check is asked and did not answer.</summary>
    public static bool RanksLast(SignInState? state) => state is { Standing: SignInStanding.Unanswered };
}
