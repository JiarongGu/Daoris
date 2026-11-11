namespace Daoris.Driver;

/// <summary>Who holds each repository's own place at a look: an active record in the place itself (its tree the root, or unsaid) that
/// holds its lease (<see cref="PlaceLeases"/>); a record in a tree of its own, a pass and an intake hold none. The planner orders the
/// queue behind it; nothing stores it.</summary>
internal static class PlaceHolders
{
    /// <summary>The record holding <paramref name="repository"/>'s place at <paramref name="root"/>, or null; <paramref name="except"/> holds nothing against itself.</summary>
    public static SessionView? Of(Snapshot snapshot, string repository, string? root, Func<SessionView, bool>? except = null) =>
        snapshot.Active.FirstOrDefault(session =>
            string.Equals(session.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && (session.Tree is null || root is null || Planner.SameTree(session.Tree, root))
            && PlaceLeases.Holds(session)
            && except?.Invoke(session) != true);

    /// <summary>The planner's sentence for a start sitting behind <paramref name="holder"/>.</summary>
    public static string Said(SessionView holder, string repository) =>
        holder.HoldsPlace is { Count: > 0 } paths && SessionStates.IsParked(holder.State)
            ? $"`{holder.Id}` holds the checkout of {repository}: {paths.Count} uncommitted path{(paths.Count == 1 ? "" : "s")} of its own; "
              + "this starts once they are committed, discarded, or the person lets others work here."
            : $"`{holder.Id}` is working in {repository}; this starts when its turn ends.";
}
