namespace Daoris.Driver;

/// <summary>What a conversation's watch knows of it beyond its process.</summary>
/// <param name="Adapter">The adapter it runs on, whose conversation id is kept for it.</param>
/// <param name="Keeps">Whether its conversation's id is kept, so it can go on once it ended (<see cref="ChatPlace.Keeps"/>).</param>
/// <param name="GoOn">The ended chat this run goes on with, or null for a new conversation.</param>
internal sealed record ChatWatched(string Adapter, bool Keeps, GoingOn? GoOn)
{
    /// <summary>The plugins' skills composed for it (<see cref="SpawnSkills"/>), which go when the conversation does.</summary>
    public string? Skills { get; init; }

    /// <summary>A fact about how this run is set up, its transcript's first line on either door: the mode Ask Daoris's start got. Null says nothing.</summary>
    public string? Preamble { get; init; }
}
