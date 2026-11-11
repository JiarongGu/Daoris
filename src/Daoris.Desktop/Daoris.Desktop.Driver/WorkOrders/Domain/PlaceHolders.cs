namespace Daoris.Driver;

/// <summary>
/// Who holds each repository's own place at a look, as the ledger's writer's lease reads it: an active record in the place itself
/// (its tree the root, or unsaid) unless it is parked with no uncommitted paths of its own. A record in a tree of its own, a pass and an
/// intake hold none. The queue behind it is the planner's order; nothing stores it.
/// </summary>
internal static class PlaceHolders
{
    /// <summary>The record holding <paramref name="repository"/>'s place at <paramref name="root"/>, or null; <paramref name="except"/> holds nothing against itself.</summary>
    public static SessionView? Of(Snapshot snapshot, string repository, string? root, Func<SessionView, bool>? except = null) =>
        snapshot.Active.FirstOrDefault(session =>
            string.Equals(session.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && (session.Tree is null || root is null || Planner.SameTree(session.Tree, root))
            && Leases(session)
            && except?.Invoke(session) != true);

    public static bool Leases(SessionView session) =>
        session is { Ask: null, Opinion: null, Serves: null, Decides: null }
        && (!SessionStates.IsParked(session.State) || session.HoldsPlace is { Count: > 0 });

    /// <summary>The planner's sentence for a start sitting behind <paramref name="holder"/>.</summary>
    public static string Said(SessionView holder, string repository) =>
        holder.HoldsPlace is { Count: > 0 } paths && SessionStates.IsParked(holder.State)
            ? $"`{holder.Id}` holds the checkout of {repository}: {paths.Count} uncommitted path{(paths.Count == 1 ? "" : "s")} of its own; "
              + "this starts once they are committed, discarded, or the person lets others work here."
            : $"`{holder.Id}` is working in {repository}; this starts when its turn ends.";
}
