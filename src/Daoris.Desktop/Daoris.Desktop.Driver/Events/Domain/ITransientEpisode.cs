namespace Daoris.Driver;

/// <summary>
/// One transient episode as the conclusion of a run past its bound reads it (FOUNDATION1 mod-vii, the design §14.3): the tries made, the
/// time actually waited between them and the provider's own sentence as the first failure said it. The agents' episode answers this port, so
/// <see cref="Observation.Transient"/> names no type above the events.
/// </summary>
public interface ITransientEpisode
{
    /// <summary>The tries made so far, a failed settling turn among them.</summary>
    int Tries { get; }

    /// <summary>The time actually waited between tries.</summary>
    TimeSpan Waited { get; }

    /// <summary>The provider's own sentence, as the first failure said it; null where it said none.</summary>
    string? Words { get; }
}
