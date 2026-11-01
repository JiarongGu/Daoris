namespace Daoris.Driver;

/// <summary>
/// A repository's routing standing here (COWORKVIEW1e-i; the cowork view design §R11.2 point 4, §6.4): whether a quest published to its own
/// lanes would start on this machine, judged once, with the fixed words an agent of a work that may publish is told at its start.
/// </summary>
/// <param name="Standing"><see cref="NotDriven"/>, <see cref="NoLanes"/>, <see cref="OneAtATime"/> or <see cref="Open"/>.</param>
/// <param name="Words">Daoris's one-copy English to the agent (D142 point 6), naming the lanes by their addresses where there are some.</param>
/// <remarks>
/// <b>The judge</b>, the first that holds: not driven on this machine, whether or not it declares lanes; driven and no lanes declared,
/// whether or not it opens trees; driven with lanes and no trees of its own, so a quest waits for the session; else open. Open says the
/// way is available, never that an account or an agent can run the quest at once (the planner decides when it starts).
/// </remarks>
public sealed record RoutingStanding(string Standing, string Words)
{
    public const string NotDriven = "not-driven";
    public const string NoLanes = "no-lanes";
    public const string OneAtATime = "one-at-a-time";
    public const string Open = "open";

    /// <summary>The standing of <paramref name="repository"/> on this machine, from its drive and trees in <paramref name="config"/> and its lanes.</summary>
    public static RoutingStanding Of(DriverConfig config, RepoView repository)
    {
        var lanes = repository.Lanes;
        var addresses = string.Join(", ", lanes.Select(lane => $"`{repository.Repository}:{lane}`"));
        if (!config.Drivable.Contains(repository.Repository, StringComparer.OrdinalIgnoreCase))
        {
            return new(NotDriven, Line(
                "not driven",
                "This repository is not driven on this machine, so a quest published to "
                + (lanes.Count > 0 ? $"its lanes ({addresses})" : "it") + " would not start here. Work your rows yourself."));
        }

        if (lanes.Count == 0)
        {
            return new(NoLanes, Line(
                "no lanes", "This repository declares no lanes, so there is no lane to publish a quest to. Work your rows yourself."));
        }

        if (!config.OpensOwnTree(repository.Repository))
        {
            return new(OneAtATime, Line(
                "one at a time",
                $"This repository is driven on this machine and declares the lanes {addresses}, but opens no trees of its own, "
                + "so a quest published to one of them starts only after your work ends."));
        }

        return new(Open, Line(
            "open",
            $"This repository is driven on this machine, declares the lanes {addresses} and opens trees of its own, "
            + "so a quest published to one of them can start beside your work."));
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

    /// <summary>
    /// What the record keeps of the line told (the note beside <c>chat.told-browser</c>): the lead by its code with the standing a fact,
    /// then the words as the program's, so the person sees what the agent was told.
    /// </summary>
    public Noted Note() =>
        Noted.Of(NoteCodes.ChatToldRouting, $"told the agent this repository's routing standing, `{Standing}`:", ("standing", Standing))
            .Then(" ", Noted.Said(Words, NoteBy.Program));

    private static string Line(string standing, string sentences) => $"Routing standing here: {standing}. {sentences}";
}
