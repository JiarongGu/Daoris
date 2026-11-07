namespace Daoris.Driver;

/// <summary>The supervisor that answers a decision, or why the person does (<c>Held</c>, said); neither never.</summary>
public sealed record SupervisorFound(RoleRecord? Role, string? Held);

/// <summary>
/// Which supervisor answers a decision of a kind at a place, kind first: the narrowest scope's supervisor deciding the kind (the
/// repository's, the workspace's, the machine's), else the narrowest generic one, the machine's always standing. The supervisor found
/// answers under its grants or the person does, never a wider one: a grant left unticked is the person's word for that place.
/// </summary>
/// <remarks>
/// A place naming a repository and no workspace is placed by <see cref="RuleWiderPlaces"/>, the map the rules read in every process. A map
/// that did not read, or an unreadable appointed record whose scope covers the place (or does not read), makes the decision the person's,
/// since what that record or map would decide cannot be read.
/// </remarks>
public static class SupervisorResolution
{
    /// <summary>Who answers <paramref name="kind"/> at <paramref name="place"/> among <paramref name="slots"/>.</summary>
    public static SupervisorFound Find(IReadOnlyList<RoleSlot> slots, string kind, RolePlace place, RuleWiderPlaces? wider = null)
    {
        if (place is { Repository: { } repository, Workspace: null } && wider is not null)
        {
            if (wider.Error is { } error) return new(null, $"The repositories' workspaces do not read ({error}), so the decisions in a workspace or a repository are yours until they do.");
            place = place with { Workspace = wider.WorkspaceOf.GetValueOrDefault(repository) };
        }

        foreach (var slot in slots.Where(slot => slot.State == RoleState.Unreadable && slot.Appointed && slot.Id != AppointedRoles.Steward))
        {
            if (slot.Scope is null || slot.Scope.Covers(place))
            {
                return new(null, $"The role `{slot.Id}` {slot.Error}, so the decisions {(slot.Scope is null ? "of every supervisor" : "in its scope")} are yours until it reads or is deleted.");
            }
        }

        var covering = slots.Select(slot => slot.Role).OfType<RoleRecord>()
            .Where(role => role.Hold is { Kind: AppointedRoles.Supervisor } && role.Scope.Covers(place)).ToList();
        var found = Narrowest(covering.Where(role => role.Hold!.Decides.Contains(kind, StringComparer.Ordinal)))
            ?? Narrowest(covering.Where(role => role.Hold!.Generic));
        return found is null ? new(null, "No supervisor decides here, so the decision is yours.") : new(found, null);
    }

    /// <summary>
    /// What <paramref name="set"/> would share with <paramref name="other"/> in one scope: the steward's one place; for supervisors, the
    /// generic one's, or the first kind both decide. Null where the two may stand together.
    /// </summary>
    public static string? Shared(RoleHold set, RoleHold other)
    {
        if (set.Kind != other.Kind) return null;
        if (set.Kind != AppointedRoles.Supervisor) return $"is already the {set.Kind} in that scope: at most one role of a kind acts in one scope";
        if (set.Generic && other.Generic) return "is already the supervisor for every decision in that scope: at most one generic supervisor stands in one scope";
        return set.Decides.Intersect(other.Decides, StringComparer.Ordinal).FirstOrDefault() is { } kind
            ? $"already decides `{kind}` in that scope: at most one supervisor decides a kind in one scope"
            : null;
    }

    private static RoleRecord? Narrowest(IEnumerable<RoleRecord> roles) => roles.OrderByDescending(role => role.Scope.Rank).FirstOrDefault();
}
