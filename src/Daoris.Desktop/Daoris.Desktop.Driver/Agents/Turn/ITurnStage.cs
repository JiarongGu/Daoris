namespace Daoris.Driver;

/// <summary>
/// One stage of the protocol door's reading of a frame, registered with <see cref="TurnStageAttribute"/> and built once per session
/// with its wiring: a public constructor taking <see cref="ITurnWiring"/>.
/// </summary>
/// <remarks>
/// 🔴 The reader dispatches each frame before it reads the next, so a stage that waits holds every frame after it: the agent's
/// updates, a cancel's answer, the stream's end. What waits on the person or a clock goes on beside the reader, and the stage returns.
/// </remarks>
internal interface ITurnStage
{
    /// <summary>
    /// True where the stage took the frame: no stage after it in its phase sees it, and a request it took is its own to answer.
    /// </summary>
    ValueTask<bool> RunAsync(TurnFrame frame);
}
