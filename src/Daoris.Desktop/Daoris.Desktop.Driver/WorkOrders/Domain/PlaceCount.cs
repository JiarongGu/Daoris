namespace Daoris.Driver;

/// <summary>
/// How many of a place's sessions work apart at once, against its <c>isolate</c> step's <c>at-once</c>: those active in a tree of the
/// repository, those still opening there, and those started at this look. A start going back into its own tree is already counted.
/// </summary>
internal static class PlaceCount
{
    /// <summary>The step's count where the place's sessions apart already fill it, else null.</summary>
    public static int? Full(PlanRound round, IsolateStep step, string repository, Func<SessionView, bool> itself)
    {
        if (step.AtOnce is not { } atOnce) return null;
        var working = round.Snapshot.Active.Count(session => session.Tree is { Length: > 0 }
            && string.Equals(session.Repository, repository, StringComparison.OrdinalIgnoreCase) && !itself(session));
        var opening = round.Opening.Count(start => string.Equals(start.Repository, repository, StringComparison.OrdinalIgnoreCase));
        var started = round.StartsThisLook.Count(quest => string.Equals(quest.To, repository, StringComparison.OrdinalIgnoreCase));
        return working + opening + started >= atOnce ? atOnce : null;
    }
}
