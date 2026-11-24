namespace Daoris.Driver;

/// <summary>
/// The one reader of an account's sign-in state (<see cref="SignInState"/>), from its last reading (<see cref="AccountReads"/>): what the walk ranks by
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
        { Login: LoginState.Out } => new SignInState(SignInStanding.Out, read.At, read.Since ?? read.At, read.Before == LoginState.In, read.By),
        _ => new SignInState(SignInStanding.Unanswered, read.At, read.Since ?? read.At),
    };

    /// <summary>Whether a start ranks the account last: its check is asked and did not answer.</summary>
    public static bool RanksLast(SignInState? state) => state is { Standing: SignInStanding.Unanswered };
}
