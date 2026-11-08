namespace Daoris.Driver;

/// <summary>The parts of a session before the one being started, as that start's instruction hands them.</summary>
internal static class PartsHanded
{
    /// <summary>
    /// What a further part of a session is handed of the parts before it: each one's record, its tree's name where the tree stands
    /// and its branch now, or the branch its work landed on where the tree is gone; and how its own tree stands to theirs. Only the
    /// newest the instruction names are read from git; a running part's note is its opening, so none is said.
    /// </summary>
    /// <param name="joinedIn">The tree it went into, standing; null where it did not.</param>
    /// <param name="entryTree">The tree the planner found free, gone or not; null where it found none.</param>
    public static async Task<SessionParts> ReadAsync(
        LoopContext context, EntryParts entry, string? joinedIn, string? entryTree, TreeOpened? opened, string repository, CancellationToken ct)
    {
        var earlier = new List<EarlierPart>();
        var places = new LinePlaces(context.Home);
        foreach (var part in entry.Parts)
        {
            var named = earlier.Count < TargetPrompt.PartsShown;
            var tree = named && part.Tree is { Length: > 0 } path && places.Of(path).Stands ? path : null;
            var branch = tree is not null
                ? await context.Trees.BranchNowAsync(tree, ct).ConfigureAwait(false) ?? $"daoris/{TreeName(tree)}"
                : named && context.Trees.Recorded.Landing(part.Session) is { GoneAt: null } landing
                        && string.Equals(landing.Repository, repository, StringComparison.OrdinalIgnoreCase)
                    ? landing.Branch
                    : null;
            earlier.Add(new EarlierPart(part.Session, part.Quest, part.State)
            {
                Note = part.Running ? null : part.Note,
                Tree = tree is null ? null : TreeName(tree),
                Branch = branch,
            });
        }

        var regrown = entryTree is not null && joinedIn is null;
        return new SessionParts(earlier)
        {
            Into = joinedIn is not null ? entry.Into?.Session : null,
            HeldBy = entry.HeldBy,
            Gone = regrown ? entry.Into?.Session : null,
            GrewFrom = regrown ? opened?.GrewFrom : null,
        };

        static string TreeName(string tree) => Path.GetFileName(tree.TrimEnd('/', '\\'));
    }
}
