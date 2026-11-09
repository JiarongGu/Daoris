namespace Daoris.Driver;

/// <summary>
/// What a start's hold (<see cref="TreeLock.TryStarting"/>) starts in, so the hold reads the tree's hand-off inside it: a tree that an open
/// hand-off reserves gives no hold but to the start carrying that hand-off's ask key.
/// </summary>
/// <param name="Tree">The tree the session starts in; null for one the start opens, or for a hold that starts no session.</param>
/// <param name="Key">The hand-off's ask key the start carries (<see cref="TreeHandOff.Ask"/>); null for every other start.</param>
public sealed record StartIn(string? Tree, string? Key)
{
    /// <summary>A start in <paramref name="tree"/>, an existing tree or the checkout itself.</summary>
    public static StartIn In(string tree, string? key = null) => new(tree, key);

    /// <summary>A start in a tree it opens itself, which no hand-off can have reserved.</summary>
    public static StartIn Fresh { get; } = new(null, null);

    /// <summary>A hold that starts no session, as the together press's.</summary>
    public static StartIn None { get; } = new(null, null);
}
