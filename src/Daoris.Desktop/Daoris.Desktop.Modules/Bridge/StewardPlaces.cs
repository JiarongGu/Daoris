using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The place an allow supervisor decides a steward's act at: the act's target read as <see cref="SupervisorDoor.ActAsync"/> reads it, a
/// session's resolved to its quest first, then the quest's repository and that repository's workspace. The machine is the place only for a
/// target whose kind names no repository (a desk request). A target that does not resolve is no place: the person's card follows.
/// </summary>
internal static class StewardPlaces
{
    public static async Task<RolePlace?> OfAsync(IOwnerScopes scopes, StandingConfirmation judge, RoleScope target, CancellationToken ct)
    {
        if (target.Kind == StewardDesk.Kind || target.Kind == StewardUpdateAct.Kind) return new RolePlace();
        var name = target.Name;
        if (target.Kind == OwnerKinds.Session)
        {
            if (await scopes.QuestOfSessionAsync(name, ct).ConfigureAwait(false) is not { } quest) return null;
            name = quest;
        }

        var repository = target.Kind == OwnerKinds.Repository ? name : await scopes.RepositoryOfQuestAsync(name, ct).ConfigureAwait(false);
        return repository is null ? null : judge.Placed(repository);
    }
}
