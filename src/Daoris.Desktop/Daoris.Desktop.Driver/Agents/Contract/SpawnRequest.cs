using System.Collections.ObjectModel;

namespace Daoris.Driver;

/// <summary>
/// What a start asks of a door's spawn: where it runs, the machine's command, the choice its actor asked,
/// the mode it names, the conversation it goes on in, and whose work it serves. Plain values: a door's <see cref="ISpawn"/> reads the first
/// four for its recipe, and the transports' one shell (<see cref="Shell"/>) the rest to complete it.
/// </summary>
/// <param name="Repository">Whose agent the session is, set as <c>DAORIS_REPOSITORY</c>.</param>
/// <param name="Root">The working tree it runs in.</param>
/// <param name="ServiceUrl">Where the service is, set as <c>DAORIS_SERVICE_URL</c>.</param>
public sealed record SpawnRequest(string Repository, string Root, string ServiceUrl)
{
    /// <summary>The machine's command for the door (<c>driver.json</c>'s <c>commands</c>), or null for the door's own program.</summary>
    public IReadOnlyList<string>? Command { get; init; }

    /// <summary>The choice the start's actor asked, which a door that carries it on its command line carries; null asks none.</summary>
    public ActorAsked? Asked { get; init; }

    /// <summary>The permission mode the start names, as its repository's setup or the person's setting resolved it; null keeps the door's own.</summary>
    public string? Mode { get; init; }

    /// <summary>The agent's own conversation the start goes on in, by the id kept for it; null starts a new one.</summary>
    public string? Resume { get; init; }

    /// <summary>The home the start's tools are read from; null reads this process's.</summary>
    public string? Home { get; init; }

    /// <summary>
    /// Whose work the start serves, each value under a name of <see cref="Spawning.Roles"/> and set in this order after the
    /// session's git; a null value takes the name away (a brainstorm's pass names no repository).
    /// </summary>
    public IReadOnlyDictionary<string, string?> Identity { get; init; } = ReadOnlyDictionary<string, string?>.Empty;
}
