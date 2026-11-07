namespace Daoris.Driver;

/// <summary>
/// A repository's routing standing here: whether a quest published to its own
/// lanes would start on this machine, judged once. The standing block says it to a work that may publish (<see cref="StandingBlock"/>).
/// </summary>
/// <param name="Standing"><see cref="NotDriven"/>, <see cref="NoLanes"/>, <see cref="OneAtATime"/> or <see cref="Open"/>.</param>
/// <param name="Lanes">The lanes the registry declares for it, by their ids.</param>
/// <remarks>
/// <b>The judge</b>, the first that holds: not driven on this machine, whether or not it declares lanes; driven and no lanes declared,
/// whether or not it opens trees; driven with lanes and no trees of its own, so a quest waits for the session; else open. Open says the
/// way is available, never that an account or an agent can run the quest at once (the planner decides when it starts).
/// </remarks>
public sealed record RoutingStanding(string Standing, IReadOnlyList<string> Lanes)
{
    public const string NotDriven = "not-driven";
    public const string NoLanes = "no-lanes";
    public const string OneAtATime = "one-at-a-time";
    public const string Open = "open";

    /// <summary>The standing of <paramref name="repository"/> on this machine, from its drive and trees in <paramref name="config"/> and its lanes.</summary>
    public static RoutingStanding Of(DriverConfig config, RepoView repository)
    {
        var lanes = repository.Lanes;
        if (!config.Drivable.Contains(repository.Repository, StringComparer.OrdinalIgnoreCase)) return new(NotDriven, lanes);
        if (lanes.Count == 0) return new(NoLanes, lanes);
        return new(config.OpensOwnTree(repository.Repository) ? Open : OneAtATime, lanes);
    }

    /// <summary>
    /// The standing to tell a record of <paramref name="work"/>, or null: told to <c>build</c>, <c>design</c> and <c>chat</c> alone, the works
    /// that may publish to their own repository's lanes (the connector refuses the serving works the quest tools), and nothing for a
    /// repository the registry does not name.
    /// </summary>
    public static RoutingStanding? Told(string? work, DriverConfig config, RepoView? repository) =>
        repository is not null && work is ParticipantWork.Build or ParticipantWork.Design or ParticipantWork.Chat
            ? Of(config, repository)
            : null;
}
