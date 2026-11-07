namespace Daoris.Driver;

/// <summary>
/// One kind's answer at a place: the supervisor that decides it, its scope as a place, its holder and the acts of the kind its grants
/// cover, or why the decision is the person's.
/// </summary>
public sealed record DeciderLine(string Kind, string? Role, string? Scope, string? Holder, IReadOnlyList<string> Granted, string? Held);

/// <summary>A place's answer, one line per kind in the vocabulary's order.</summary>
public sealed record DeciderPlace(string Place, IReadOnlyList<DeciderLine> Kinds);

/// <summary>
/// Every named place's answer, and what a repository no place names reads: a workspace no place names reads as the machine does,
/// since no supervisor is placed there.
/// </summary>
public sealed record Deciders(IReadOnlyList<DeciderPlace> Places, IReadOnlyList<DeciderLine> Elsewhere);

/// <summary>
/// Who decides each kind at each place a supervisor's scope or the repositories' map names, read by <see cref="SupervisorResolution.Find"/>
/// alone, so a place's *Who decides here* says what the admission would answer.
/// </summary>
public static class SupervisorDeciders
{
    private const string Machine = "machine";

    // No repository is named by the empty id, so it reads the chain every unnamed repository reads.
    private const string Unnamed = "";

    public static Deciders Of(IReadOnlyList<RoleSlot> slots, RuleWiderPlaces wider)
    {
        var scopes = slots.Where(slot => slot.Appointed || slot.Role?.Hold is not null)
            .Select(slot => slot.Role?.Scope ?? slot.Scope).OfType<RoleScope>().Where(scope => scope.Rank > 0)
            .Select(scope => (scope.Kind, Id: scope.Id!))
            .Concat(wider.WorkspaceOf.SelectMany(pair => new[] { (Kind: RoleScope.Repository, Id: pair.Key), (Kind: RoleScope.Workspace, Id: pair.Value) }))
            .Distinct().OrderBy(scope => scope.Kind == RoleScope.Workspace ? 0 : 1).ThenBy(scope => scope.Id, StringComparer.Ordinal);
        var places = scopes.Select(scope => new DeciderPlace($"{scope.Kind}:{scope.Id}",
            Lines(slots, scope.Kind == RoleScope.Workspace ? new RolePlace(Workspace: scope.Id) : new RolePlace(Repository: scope.Id), wider)));
        return new([new DeciderPlace(Machine, Lines(slots, new RolePlace(), wider)), .. places], Lines(slots, new RolePlace(Repository: Unnamed), wider));
    }

    private static IReadOnlyList<DeciderLine> Lines(IReadOnlyList<RoleSlot> slots, RolePlace place, RuleWiderPlaces wider) =>
        [.. SupervisorKinds.All.Select(kind => Line(SupervisorResolution.Find(slots, kind, place, wider), kind))];

    private static DeciderLine Line(SupervisorFound found, string kind) => found.Role is { Hold: { } hold } role
        ? new(kind, role.Id, PlaceOf(role.Scope), hold.Holder, [.. SupervisorKinds.ActsOf(kind).Where(act => hold.Acts.Contains(act, StringComparer.Ordinal))], null)
        : new(kind, null, null, null, [], found.Held);

    private static string PlaceOf(RoleScope scope) => scope.Rank == 0 ? Machine : $"{scope.Kind}:{scope.Id}";
}
