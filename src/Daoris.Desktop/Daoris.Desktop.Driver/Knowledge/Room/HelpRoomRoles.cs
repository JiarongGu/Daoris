using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The working roles as the room says them (STAFFPOLICY1g, D226; the staff policy design §6.5): each role's choices, its policy in the
/// fold's words, the next new start the policy would take and the last seven days' starts and moves, read from the home's records, so
/// <i>why did this run on Opus?</i> is answered from them. The facts are read as the room is written
/// (<see cref="HelpRoleFacts.Read"/>, with the housekeeping), and a room built by hand says none.
/// </summary>
internal sealed class HelpRoomRoles : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        // A workspace's room leaves out the roles scoped to another workspace; the whole machine's room (no workspace) says them all.
        // A repository's role stays only where that repository is the workspace's.
        var roles = machine.Roles.Where(role => machine.Workspace is null
            || role.ScopeKind == HelpRole.WorkspaceScope && string.Equals(role.ScopeId, machine.Workspace, StringComparison.OrdinalIgnoreCase)
            || role.ScopeKind == HelpRole.RepositoryScope && machine.Repositories.Any(repository => string.Equals(repository.Name, role.ScopeId, StringComparison.OrdinalIgnoreCase))
            || role.ScopeKind is not (HelpRole.WorkspaceScope or HelpRole.RepositoryScope)).ToList();
        if (roles.Count == 0) return "";

        var text = new StringBuilder();
        text.Append("### Roles and their policies\n\n");
        // The starts and moves are the machine's records, not split by workspace, so a workspace's room says whose they are.
        if (machine.Workspace is not null) text.Append("The last 7 days' starts and moves below are the whole machine's, every workspace's, not this workspace's alone.\n\n");
        text.Append("Each working role with its choices (numbered as its policy numbers them), its policy in the words a card says it in, the next new\n");
        text.Append("start the policy would take and what the last 7 days did: answer *why did this run on that agent?* from these lines. A policy\n");
        text.Append("only orders the choices a role already lists; it is proposed with `role_propose`'s `policy` (a preset's name, `first`, `even` or\n");
        text.Append("`weighted`, or the record) and `policy_revision`, the number below, and it is the person's Apply alone, whatever access you have.\n\n");
        foreach (var role in roles) text.Append($"- {Line(role)}\n");
        text.Append('\n');
        return text.ToString();
    }

    private static string Line(HelpRole role)
    {
        var parts = new List<string>
        {
            $"`{role.Id}` ({role.Name}) takes {string.Join(", ", role.Takes)}{(role.ScopeKind == HelpRole.MachineScope ? "" : $", {role.ScopeKind} {role.ScopeId}")}; revision {role.Revision}",
            role.ChoiceState == "off" ? "off: it starts nothing"
            : role.ChoiceState == "unreadable" ? "its entry in `driver.json` does not read, so it starts nothing until it is set again"
            : role.Choices.Count == 0
                ? "no choices of its own, so this machine's defaults run"
                : "choices " + string.Join(", ", role.Choices.Select((choice, at) => $"{at + 1} {choice}")),
            role.PolicyState switch
            {
                HelpRole.Set => $"policy (revision {role.PolicyRevision}): {role.Policy}",
                HelpRole.Stale => $"policy (revision {role.PolicyRevision}) was set for other choices, so it runs them in order until it is saved again: {role.Why}",
                _ => $"policy (revision {role.PolicyRevision}): First that can run",
            },
        };
        if (role.Next is { Length: > 0 } next) parts.Add($"next new start: {next}");
        parts.Add(role.Week);
        return string.Join("; ", parts) + ".";
    }
}

/// <summary>One working role as the room tells it: its choices, its policy's state and words, its next new start and its last seven days.</summary>
/// <param name="ScopeKind">The role's scope: <c>machine</c>, <c>workspace</c> or <c>repository</c>.</param>
/// <param name="Revision">The role's own revision, the catalogue's; a policy has its own, <see cref="PolicyRevision"/>.</param>
public sealed record HelpRole(string Id, string Name, IReadOnlyList<string> Takes, string ScopeKind, string? ScopeId, long Revision)
{
    public const string MachineScope = "machine";

    public const string WorkspaceScope = "workspace";

    public const string RepositoryScope = "repository";

    /// <summary>The policy is set and in use.</summary>
    public const string Set = "set";

    /// <summary>The policy was set for other choices than the role has now, so the role runs in order.</summary>
    public const string Stale = "stale";

    /// <summary>The choices have not been set, so this machine's defaults run.</summary>
    public const string Before = "before";

    /// <summary>The role's choices as the driver's file holds them: <see cref="Before"/>, <c>set</c>, <c>off</c> or <c>unreadable</c> (<c>ActorView.State</c>).</summary>
    public string ChoiceState { get; init; } = Before;

    /// <summary>The role's own choices, each as its agent, model and effort; none where it has none of its own.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary><see cref="Set"/>, <see cref="Stale"/> or <c>default</c>.</summary>
    public string PolicyState { get; init; } = "default";

    /// <summary>The policy's revision, which <c>role_propose</c>'s <c>policy_revision</c> carries back.</summary>
    public long PolicyRevision { get; init; }

    /// <summary>The policy in the fold's words; <i>First that can run</i> where none is set.</summary>
    public string Policy { get; init; } = "First that can run";

    /// <summary>Why a stale policy is stale.</summary>
    public string? Why { get; init; }

    /// <summary>The next new start as the policy would take it, or null where it is not probed.</summary>
    public string? Next { get; init; }

    /// <summary>The last seven days in a sentence (<c>StaffWeekRole.Said</c>).</summary>
    public string Week { get; init; } = "last 7 days: no new start";
}
