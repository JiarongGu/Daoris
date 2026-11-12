namespace Daoris.Driver;

/// <summary>
/// What a step's session works from, composed from its run as the step's plugin would be told it (<see cref="NodeTelling.RunTold"/>): the
/// place and its checkout, the branch, the line and the commit of the frame, origin's address read from the checkout, and the landing the
/// frame names as its evidence. Null where the run names no frame for the attempt.
/// </summary>
public static class StepAsks
{
    /// <summary>The ask of <paramref name="attempt"/> of <paramref name="node"/> in <paramref name="unit"/>, told <paramref name="told"/>.</summary>
    public static async Task<StepAgentAsk?> OfAsync(
        string unit, NodeAttempt attempt, NodeDeclaration node, WatchedUnit? told, LandedBranch? landing, CancellationToken ct)
    {
        if (told is null || node.Goal is not { } goal) return null;
        var slash = node.Kind.IndexOf('/');
        var step = slash > 0 ? node.Kind[(slash + 1)..] : node.Kind;
        var origin = told.Root.Length > 0 ? await GitRemotes.AddressOf(told.Root, "origin", ct: ct).ConfigureAwait(false) : null;
        var tip = attempt.Call.SubjectRevision is { Length: > 0 } revision ? revision : told.Tip;
        return new StepAgentAsk(
            attempt.Call.Invocation, unit, node.Id, attempt.Attempt, attempt.Plugin, step, goal.AsWritten, told.Repository, told.Workspace,
            told.Branch.Length > 0 ? told.Branch : null, origin, told.Line, tip)
        {
            Outcomes = node.Outcomes,
            Outputs = node.Outputs,
            Options = node.Options,
            Evidence = node.Evidence,
            Root = told.Root.Length > 0 ? told.Root : null,
            Landing = landing is null ? told.PullRequest : Landed(landing),
        };
    }

    /// <summary>The landing as its evidence says it: the branch landed, from which session and quest, onto which line, and its pull request.</summary>
    public static string Landed(LandedBranch landing) =>
        $"`{landing.Branch}` landed at {landing.Tip} onto `{landing.Line ?? "(its line)"}` from session `{landing.Session}`"
        + (landing.Quest is { Length: > 0 } quest ? $" for quest `#{quest}`" : "")
        + (landing.Title is { Length: > 0 } title ? $" ({title})" : "")
        + (landing.PullRequest is { Length: > 0 } pull ? $", its pull request {pull}" : "");
}
