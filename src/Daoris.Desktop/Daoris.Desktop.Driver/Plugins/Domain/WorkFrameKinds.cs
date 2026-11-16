namespace Daoris.Driver;

/// <summary>
/// The answer kinds a plugin sends at <c>work/step</c> (<see cref="WorkStates"/>), and the one fact every reader that counts answers takes
/// from a kind: whether it completes its attempt. A progress completes nothing: it is kept on the attempt and the attempt stays out. The
/// frame census (<c>WorkFrameCensusTests</c>) holds each reader to it, so a kind added to the wire reaches every reader before it ships.
/// </summary>
public static class WorkFrameKinds
{
    /// <summary>Every kind, completions first.</summary>
    public static IReadOnlyList<string> All { get; } = [.. WorkStates.All, WorkStates.Progress];

    /// <summary>Whether a frame of <paramref name="kind"/> completes the attempt it names: every kind but a progress.</summary>
    public static bool Completes(string kind) => kind != WorkStates.Progress;
}
