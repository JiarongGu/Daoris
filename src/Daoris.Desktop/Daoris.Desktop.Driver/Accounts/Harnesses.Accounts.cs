namespace Daoris.Driver;

/// <summary>
/// An account put into a scope's list at a person's word, and where each account runs (ACCT1, D125's ACCT1 note; D130 §3.1):
/// a new account's sign-in ends by saying which lists and defaults hold it, both doors can add it to a list in the same step,
/// and an account no list and no default holds says so where it is listed.
/// </summary>
/// <remarks>
/// <para><b>A twin</b> of the CLI's <c>rotation.ts</c> (<c>joinProblem</c>, <c>withJoined</c>, <c>placesOf</c>);
/// <c>AccountJoinTwinTests</c> and the CLI's <c>account-join.test.ts</c> hold the same tables, row for row. Accounts compare
/// exactly, as the wiring compares a default or a list's names with a folder.</para>
/// <para>🔴 Found on the install, 2026-10-04: a person signed in to re-enable a signed-out account, got a new account no list
/// held, and the work kept starting on the empty one. A new account in no list is now said at its sign-in's end, with the
/// list it may join, and listed as running nowhere.</para>
/// </remarks>
public sealed partial record HarnessSettings
{
    /// <summary>
    /// Why an account cannot join <paramref name="workspace"/>'s list, or null where it can: a workspace that names no default
    /// and no list of its own for the agent takes this machine's scope (D130 §2 rule 1), so its list is this machine's, and a
    /// list of its own would move its starts off every account the machine's holds. Null is this machine's list, which any
    /// account may join.
    /// </summary>
    public JoinProblem? JoinProblemOf(string agent, string? workspace) =>
        workspace?.Trim() is { Length: > 0 } circle && ResolveScope(agent, circle).From != ChoiceFrom.Workspace
            ? new JoinProblem(circle)
            : null;

    /// <summary>
    /// The wiring with <paramref name="account"/> in the scope's own list (D130 §3.1): appended where the list lacks it, a list
    /// begun at the scope's default where it has none, or of the account alone where the scope names nobody. The list's
    /// settings stay. Whether the scope may be joined is <see cref="JoinProblemOf"/>'s question, which a door asks first.
    /// </summary>
    public HarnessSettings WithJoined(string agent, string account, string? workspace)
    {
        var scope = ResolveScope(agent, workspace);
        IReadOnlyList<string> list = scope.List.Count > 0
            ? scope.List.Contains(account, StringComparer.Ordinal) ? scope.List : [.. scope.List, account]
            : scope.Default is { } begins && begins != account ? [begins, account] : [account];
        return WithRotation(agent, list, workspace?.Trim() is { Length: > 0 } circle ? circle : null);
    }

    /// <summary>
    /// Where <paramref name="account"/> runs: each scope whose own list holds it or whose own default names it, this machine
    /// first and then each workspace by name. None is an account no start runs on, which both doors say.
    /// </summary>
    public IReadOnlyList<AccountPlace> PlacesOf(string agent, string account)
    {
        var places = new List<AccountPlace>();
        Add(null, Rotation.GetValueOrDefault(agent), Defaults.GetValueOrDefault(agent));

        foreach (var workspace in Workspaces.Keys.Concat(WorkspaceRotation.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            Add(
                workspace,
                WorkspaceRotation.TryGetValue(workspace, out var orders) ? orders.GetValueOrDefault(agent) : null,
                Workspaces.TryGetValue(workspace, out var defaults) ? defaults.GetValueOrDefault(agent) : null);
        }

        return places;

        void Add(string? workspace, IReadOnlyList<string>? list, string? named)
        {
            var listed = list?.Contains(account, StringComparer.Ordinal) == true;
            var defaulted = named?.Trim() == account;
            if (listed || defaulted) places.Add(new AccountPlace(workspace, listed, defaulted));
        }
    }
}

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
