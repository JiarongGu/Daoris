namespace Daoris.Driver;

/// <summary>
/// The run's memory of a role's acts as the driver's starts read it, handed by the application that
/// holds the judge; null on a loop with none, whose starts no role's act caused and which honours no role's release (fail-closed).
/// </summary>
public interface IRoleStarts
{
    /// <summary>
    /// Whether a steward's act caused a start of <paramref name="quest"/> in <paramref name="repository"/>: the repository driven
    /// by the steward this run and not the owner's since, or the quest carried on by a steward's live release.
    /// </summary>
    bool Caused(string repository, string quest);

    /// <summary>Whether this run's memory holds <paramref name="release"/>'s operation at <c>released</c>: only then is it a release.</summary>
    bool Released(string quest, RoleRelease release);

    /// <summary>
    /// The start's take of the release (the execution cutoff): its operation moved from <c>released</c> to <c>executing</c>, its
    /// generation live and its quest not the owner's; false where a revoke or the owner's act cancelled it.
    /// </summary>
    bool Take(string quest, RoleRelease release);

    /// <summary>
    /// A start a steward's act caused is held for the owner (<c>held.checkout</c>): every pending operation on the quest ended,
    /// said on the role's line.
    /// </summary>
    void HeldForOwner(string quest);
}
