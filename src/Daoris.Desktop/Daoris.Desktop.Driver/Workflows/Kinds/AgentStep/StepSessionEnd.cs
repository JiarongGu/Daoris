namespace Daoris.Driver;

/// <summary>
/// How a step's session ended for the accounts it may run on (<see cref="IStepSessions"/>): the account its agent refused for its sign-in,
/// which reads signed out by then, so the walk passes it; or, where no account could take the step, the accounts the walk passed signed
/// out. <see cref="None"/> for every other end, which the step's answer alone says.
/// </summary>
/// <param name="Agent">Whose accounts: the owner a door runs as, which <c>daoris agent login</c> takes; null for <see cref="None"/>.</param>
/// <param name="RefusedOn">The account the session ran on and its agent refused for its sign-in; null where it was not.</param>
/// <param name="SignedOut">The accounts the walk passed not signed in where none could take the step, in the order it tried them.</param>
public sealed record StepSessionEnd(string? Agent, string? RefusedOn, IReadOnlyList<string> SignedOut)
{
    /// <summary>An end that names no sign-in.</summary>
    public static StepSessionEnd None { get; } = new(null, null, []);

    /// <summary>The session ran on <paramref name="account"/> and its agent refused it for its sign-in.</summary>
    public static StepSessionEnd Refused(string agent, string account) => new(agent, account, []);

    /// <summary>No account could take the step: those of <paramref name="agent"/>'s it may use are not signed in.</summary>
    public static StepSessionEnd NoAccount(string agent, IReadOnlyList<string> accounts) => new(agent, null, accounts);

    /// <summary>
    /// A staffing walk that chose nobody: with no account signed in, its code says so (<see cref="ReviewerUnavailable.SignedOut"/>), the
    /// first agent passed for it names its accounts; held for anything else, <see cref="None"/>.
    /// </summary>
    public static StepSessionEnd Held(ReviewerChoice choice) =>
        choice is { Chosen: false, Code: ReviewerUnavailable.SignedOut }
        && choice.Tried.FirstOrDefault(tried => tried.SignedOut is { Accounts.Count: > 0 }) is { SignedOut: { } passed }
            ? NoAccount(passed.Agent, passed.Accounts)
            : None;
}
