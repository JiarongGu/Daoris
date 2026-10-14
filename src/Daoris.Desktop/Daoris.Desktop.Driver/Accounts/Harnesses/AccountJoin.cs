namespace Daoris.Driver;

/// <summary>One scope an account runs in: a workspace, or null for this machine; whether its own list holds it, and whether its own default names it.</summary>
public sealed record AccountPlace(string? Workspace, bool List, bool Default);

/// <summary>Why an account cannot join a workspace's list (ACCT1): the workspace takes this machine's list, naming none of its own.</summary>
public sealed record JoinProblem(string Workspace)
{
    /// <summary>
    /// The refusal a person reads: where the workspace's starts run now, and the two ways on. The CLI's <c>joinRefusal</c> says it
    /// word for word, the workspace in its command spelled for any shell (ACCTQUOTE1b); <c>AccountJoinTwinTests</c> holds both.
    /// </summary>
    public string Sentence(string agent) =>
        $"`{Workspace}` names no `{agent}` account or list of its own, so its starts take this machine's list — join this "
        + $"machine's list, or give `{Workspace}` a list of its own first (`daoris agent profile order {agent} <account>… "
        + $"--workspace {ShellWord.Of(Workspace, ShellWord.Workspace)}`).";
}
