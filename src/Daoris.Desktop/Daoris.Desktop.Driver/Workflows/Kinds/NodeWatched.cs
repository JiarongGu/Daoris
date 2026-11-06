namespace Daoris.Driver;

/// <summary>What a watched step reports (<see cref="IWatchable.Read"/>): its settled state, or none yet, in a clause.</summary>
/// <param name="State">One of the step's states once it settled; null while it is open or unanswered.</param>
/// <param name="Said">What its records say, in a clause.</param>
public sealed record NodeWatched(string? State, string Said)
{
    /// <summary>Why nothing of it is read on this unit (<see cref="NodeRefusal.NotThisUnit"/>); null where it reads.</summary>
    public string? Refused { get; init; }

    /// <summary>Whether nothing of it can be said: no record names its unit.</summary>
    public bool Unknown { get; init; }

    /// <summary>The commit the record that settled it names.</summary>
    public string? Revision { get; init; }

    /// <summary>The record that settled it, and the entry in it: the monitor's evidence.</summary>
    public string Record { get; init; } = "";

    /// <inheritdoc cref="Record"/>
    public string Artifact { get; init; } = "";
}
