namespace Daoris.Driver;

/// <summary>
/// A brainstorm's own minutes, read from what it asks over the policy's: a design pass's generators twice the policy's pass, a question
/// naming sources whole half as long again, any other the policy's. The request's minutes grow so the generators' window holds a whole
/// pass, the synthesis's and the challenge's kept as the policy has them.
/// </summary>
public static class BrainstormDeadlines
{
    public static BrainstormMinutes Of(BrainstormMinutes policy, BrainstormRequest request)
    {
        var pass = DesignPasses.Is(request) ? 2 * policy.Pass
            : request.Sources is { Count: > 0 } ? (int)Math.Ceiling(policy.Pass * 1.5)
            : policy.Pass;
        return pass == policy.Pass ? policy
            : policy with { Pass = pass, Request = Math.Max(policy.Request, pass + policy.Synthesis + policy.Challenge) };
    }
}
