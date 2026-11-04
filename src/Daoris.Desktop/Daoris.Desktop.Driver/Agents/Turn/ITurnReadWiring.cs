using Daoris.Contracts.Parts;

namespace Daoris.Driver;

/// <summary>
/// What a protocol session hands its reader (<see cref="AcpReader"/>): the turn's stages it runs each frame through, and what the
/// stream's end closes.
/// </summary>
internal interface ITurnReadWiring : ITurnEndWiring
{
    /// <summary>The turn's stages, ordered and built once per session.</summary>
    PartSet<ITurnStage> Stages { get; }

    /// <summary>The stream is over: nothing said from now changes how it ended.</summary>
    void StreamIsOver();

    /// <summary>What the session runs beside itself ended, saying so.</summary>
    Task EndStreamsAsync();
}
