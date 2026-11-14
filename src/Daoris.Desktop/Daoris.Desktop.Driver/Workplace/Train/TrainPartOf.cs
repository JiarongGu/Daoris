namespace Daoris.Driver;

/// <summary>
/// The ask and part a quest is the worker's of, so its landing is tagged for the train. The quest's publish key names the pair
/// (<c>&lt;ask&gt;/&lt;part&gt;/&lt;n&gt;</c>), and the ask's own plan must give that part to this very quest: a key shaped alike by another
/// request node, or a plan not read, names no part and the landing stays an ordinary one's.
/// </summary>
public static class TrainPartOf
{
    public static async Task<TrainRef?> ReadAsync(string quest, string? publishKey, ITrainPlans? plans, CancellationToken ct)
    {
        if (plans is null || publishKey is null) return null;
        var pieces = publishKey.Split('/');
        if (pieces.Length != 3 || pieces[0].Length == 0 || pieces[1].Length == 0 || !int.TryParse(pieces[2], out _)) return null;
        if (await plans.OfAsync(pieces[0], ct).ConfigureAwait(false) is not { } plan) return null;
        return plan.Parts.Any(part => part.Id == pieces[1] && part.Quest == quest) ? new TrainRef(pieces[0], pieces[1]) : null;
    }
}
