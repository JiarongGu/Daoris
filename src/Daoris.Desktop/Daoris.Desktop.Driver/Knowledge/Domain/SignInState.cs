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
/// made it. Kept beside the profile report that carries it (<see cref="ProfileReport.SignIn"/>), read by <see cref="SignInStates"/>.
/// </summary>
/// <param name="At">When the reading is made; null where it never is.</param>
/// <param name="Since">When it began saying so: a sign-out's moment, kept across readings that say the same.</param>
/// <param name="NotByPerson">
/// A sign-out after a reading that said signed in: Daoris never signs an account out and removing one deletes it, so the agent
/// did, as when a refresh of its sign-in is refused.
/// </param>
/// <param name="By">Who held the account when a write of its store made this reading (<see cref="SignInCause"/>); null where none saw it.</param>
public sealed record SignInState(SignInStanding Standing, DateTimeOffset? At, DateTimeOffset? Since, bool NotByPerson = false, string? By = null)
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
