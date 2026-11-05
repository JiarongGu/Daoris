namespace Daoris.Desktop;

/// <summary>The pipe's bounds (the terminal RPC design §4.1): the design's choices, none measured; a test narrows them.</summary>
public sealed class PipeLimits
{
    /// <summary>
    /// How many connections at once: more than <see cref="Waiting"/> (CONFIRMWAIT1a-i, the confirm-wait design §9.2), since for a verb's
    /// first thirty minutes each press waiting on its line holds one, and the pipe's own requests (<c>RESULT</c>, <c>WITHDRAW</c>, a
    /// hello) need room beside the ten.
    /// </summary>
    public int Connections { get; init; } = 16;

    /// <summary>The longest line read, in bytes: a connection that sends a longer one is closed.</summary>
    public int LineBytes { get; init; } = 1024 * 1024;

    /// <summary>How long a connection has to say its hello.</summary>
    public TimeSpan Hello { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a connection with nothing under way may say nothing before it is closed.</summary>
    public TimeSpan Idle { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>How long a request's lines are kept (§6.1).</summary>
    public TimeSpan Kept { get; init; } = TimeSpan.FromHours(24);

    /// <summary>How many of a connection's requests may be under way at once: past it, one is answered <see cref="InstancePipe.Busy"/>.</summary>
    public int InFlight { get; init; } = 16;

    /// <summary>
    /// How many lines a connection may send ahead of the instance's taking them: past it the connection is closed, since lines
    /// pile up only where a terminal sends on without reading what it was answered, and an answer to them could not be read either.
    /// </summary>
    public int Queued { get; init; } = 64;

    /// <summary>
    /// How many of a held conversation's lines may wait for its terminal (RPC1g1, the terminal-conversation design §3.2): past it the oldest
    /// is dropped and a gap told, so the bus never waits on a connection.
    /// </summary>
    public int Forwarded { get; init; } = 256;

    /// <summary>
    /// How many of the person's presses may wait for the window at once (CONFIRMWAIT1a-i's ten, the confirm-wait design §9.2; RPC1c's
    /// five lasted at most twelve minutes): past it, one is refused. Fewer than <see cref="Connections"/>, which a test holds.
    /// </summary>
    public int Waiting { get; init; } = PipeConfirmations.DefaultBound;
}
