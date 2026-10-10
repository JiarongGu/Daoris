namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>repository</c> proposal (ENTRY1d1, D161's ENTRY1d note): a registered repository moved to a workspace on
/// this machine. Its door is <c>wire</c>, its target the repository as the registry names it, its value the workspace. It is
/// judged against the registry as the service answers it, and applied through the local host's
/// <c>POST /api/registry/{repository}/workspace</c>, the door the Manage drawer's *Move to workspace* uses.
/// </summary>
/// <remarks>
/// <para><b>A move needs no path</b> (D48 §7, the workspace design §7): it edits one field of the registry's row, locally and
/// at once, and touches no file. So the proposal names only the repository and the workspace. Adding or importing one needs a
/// folder, which stays the person's pick: a go opens Repositories' Add or Import with the workspace filled (ENTRY1d2a,
/// <see cref="HelpGoProposals"/>).</para>
///
/// <para><b>A new workspace is allowed</b>, as the drawer's free text allows it: a move to a name no repository is in yet
/// starts that workspace here, and the card says so. A move to the workspace it is already in is refused, since nothing would
/// change.</para>
///
/// <para><b>Any registered repository</b>, adopted or not, as the route takes it. The drawer is offered to adopters only
/// because its other half writes the repository's declaration (INT3c); the move is not that half.</para>
///
/// <para><b>No terminal twin.</b> No command edits only this field: <c>daoris connect --workspace</c> registers the whole row
/// again from the declaration, from inside the checkout, and refuses a repository that declares nothing. So the card shows
/// none, as a go shows none.</para>
/// </remarks>
internal sealed class HelpRepositoryProposals : IHelpProposalKind
{
    public string Kind => "repository";

    // ENTRY1d1: a move to a workspace, by name; adding or importing needs a folder and is never this.
    public string Tool => "repository_propose";

    public IReadOnlyList<string> Doors { get; } = ["wire"];

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        static HelpPlan Refused(string why) => new(why, "", "", null);
        if (proposal.Door != "wire") return Refused($"`{proposal.Door}` is not a repository's change — `wire`, a move to a workspace.");

        var named = proposal.Target?.Trim() ?? "";
        if (named.Length == 0) return Refused("a move names the repository, as Repositories lists it.");
        if (facts.Repositories.FirstOrDefault(each => string.Equals(each, named, StringComparison.OrdinalIgnoreCase)) is not { } repository)
        {
            return Refused($"`{named}` is not registered on this machine — use a repository's name as Repositories lists it.");
        }

        if (string.IsNullOrWhiteSpace(proposal.Value)) return Refused($"a move names the workspace to move `{repository}` to.");

        // The service's own `Workspaces.Normalize` and `Workspaces.Same`, as the route stores and the registry compares a name.
        var workspace = RemoteTarget.Workspace(proposal.Value);
        var row = facts.Registered.FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase));
        var from = row.Repository is null ? null : RemoteTarget.Workspace(row.Workspace);
        if (from is not null && string.Equals(from, workspace, StringComparison.OrdinalIgnoreCase))
        {
            return Refused($"`{repository}` is already in workspace `{from}`, so there is nothing to move.");
        }

        var starts = !facts.Registered.Any(each => string.Equals(RemoteTarget.Workspace(each.Workspace), workspace, StringComparison.OrdinalIgnoreCase));
        return new HelpPlan(null,
            $"Move `{repository}` {(from is null ? "" : $"from workspace `{from}` ")}to workspace `{workspace}` on this machine: one row "
            + "of the registry changes, at once and only here, and no file is touched."
            + (starts ? $" No repository is in `{workspace}` yet, so the move starts it here." : ""),
            "", null)
        {
            Move = new HelpRepositoryMove(repository, from, workspace),
        };
    }

    public async Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var move = applying.Plan.Move!;
        var (ok, message) = await applying.Doors.WireRepositoryAsync(move.Repository, move.Workspace, ct).ConfigureAwait(false);
        return applying.Settled(ok, $"{(ok ? "Applied" : "Not applied")}: `#{applying.Id}` — {message}", message);
    }
}

/// <summary>
/// A move the route takes (ENTRY1d1): the repository as the registry spells it, the workspace it is in (null where the registry
/// answered none for it), and the workspace it moves to, as the route stores a name.
/// </summary>
public sealed record HelpRepositoryMove(string Repository, string? From, string Workspace);

public sealed partial record HelpPlan
{
    /// <summary>For a move the route takes (ENTRY1d1): what moves, and where to; null for every other kind.</summary>
    public HelpRepositoryMove? Move { get; init; }
}

public partial interface IHelpDoors
{
    /// <summary>
    /// The local host's <c>POST /api/registry/{repository}/workspace</c> (ENTRY1d1), the Manage drawer's *Move to workspace*:
    /// one field of the registry's row. The service's answer comes back as its sentence, a refusal included.
    /// </summary>
    Task<(bool Ok, string Message)> WireRepositoryAsync(string repository, string workspace, CancellationToken ct);
}
